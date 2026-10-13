using System;
using System.Collections.Generic;
using System.Reflection;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
    /// <summary>The game modes from the overview's Game Modes table.</summary>
    public enum GameMode
    {
        Title = 0,
        Playing = 1,
        Paused = 2,
        Extinguished = 3,
        Won = 4
    }

    /// <summary>Why the lantern last went out. Recorded by the playtest logger.</summary>
    public enum ExtinguishCause
    {
        None = 0,
        /// <summary>The candle burned down by itself.</summary>
        Drained = 1,
        /// <summary>The candle ran out while the flare was held.</summary>
        FlaredOut = 2,
        /// <summary>A ghost hit took the last of the candle.</summary>
        GhostHit = 3,
        /// <summary>The player fell below Fall Death Height.</summary>
        Fell = 4
    }

    /// <summary>
    /// Game flow: an explicit game-mode FSM (Title, Playing, Paused, Extinguished, Won) that owns
    /// time scale, cursor lock, player input, respawning, play time and the death count.
    /// The overview's "Exploration", "Evasion" and "Puzzle" modes are all Playing; Evasion is
    /// exposed as <see cref="IsEvasion"/> (any ghost in Notice or Chase) for music in Step 5.
    /// Setup: one per scene on an empty object (singleton, <see cref="Instance"/>). Assign Input
    /// Actions (LanternKeeper.inputactions, map "Game": Start, Pause, Restart); without it the
    /// same keys are created in code. It finds the player through CandleManager.Instance.
    /// It runs before other scripts and calls GameEvents.Clear() in Awake, so subscribe to
    /// GameEvents in OnEnable or Start, never in a field initialiser or static constructor.
    /// The scene order (tutorial, level 1, level 2) and the run clock are in <see cref="LevelFlow"/>;
    /// this class reads the open scene's place from there, so there is nothing to wire for it.
    /// <code>
    ///   scene load ----------------------> TITLE     (Start At Title, first load)
    ///   scene load after Restart() ------> PLAYING   (title skipped)
    ///   scene load after NextLevel() ----> PLAYING   (title skipped)
    ///
    ///   TITLE        -- Start (any key / click) after titleInputDelay --> PLAYING
    ///   PLAYING      -- Pause (Esc) ------------------------------------> PAUSED
    ///   PAUSED       -- Pause (Esc) or Resume button -------------------> PLAYING
    ///   PLAYING      -- candle reaches 0, or fell below fallDeathHeight -> EXTINGUISHED
    ///   EXTINGUISHED -- respawnDelay elapsed (respawn at checkpoint) ---> PLAYING
    ///   PLAYING      -- GoalTrigger reached with the candle lit --------> WON
    ///   PAUSED       -- Restart button ---------------------------------> scene reload
    ///   WON          -- Restart (R) ------------------------------------> scene reload
    ///   WON          -- Continue button (when a level follows) ---------> next scene
    ///   WON          -- Play again button (end of the run) -------------> level 1, new run
    ///   TITLE/PAUSED -- Skip (Tab on the title, button when paused),
    ///                   tutorial only ----------------------------------> level 1
    /// </code>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static bool s_skipTitleOnce;

        // ThirdPersonController keeps the camera angles and its fall speed in private fields and
        // offers no way to set them. They are reached by reflection so the Starter Assets script
        // stays untouched (and survives a re-import of the package).
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo s_cameraYawField = typeof(ThirdPersonController).GetField("_cinemachineTargetYaw", PrivateInstance);
        private static readonly FieldInfo s_cameraPitchField = typeof(ThirdPersonController).GetField("_cinemachineTargetPitch", PrivateInstance);
        private static readonly FieldInfo s_verticalVelocityField = typeof(ThirdPersonController).GetField("_verticalVelocity", PrivateInstance);

        public static GameManager Instance { get; private set; }

        [Header("Flow")]
        [SerializeField, Tooltip("Show the title screen when the scene first loads. Restart always skips it.")]
        private bool startAtTitle = true;
        [SerializeField, Min(0f), Tooltip("Seconds the title ignores input after loading, so a key held from the last screen doesn't skip it.")]
        private float titleInputDelay = 0.3f;
        [SerializeField, Min(0f), Tooltip("Seconds between the lantern going out and the respawn.")]
        private float respawnDelay = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Candle fraction given on respawn when no checkpoint has been reached. Overview: at least 60%.")]
        private float defaultRespawnCandle = 0.6f;
        [SerializeField, Tooltip("Respawn point used before any checkpoint is reached. Empty = where the player started.")]
        private Transform startPoint;
        [SerializeField, Tooltip("HUD message while the lantern is out.")]
        private string extinguishedMessage = "Your lantern went out...";
        [SerializeField, Tooltip("If the player falls below this height the lantern goes out (they respawn at the checkpoint).")]
        private float fallDeathHeight = -25f;
        [SerializeField, Range(-30f, 70f), Tooltip("Camera pitch after a respawn (degrees, positive looks down). The yaw comes from the checkpoint's Respawn Point.")]
        private float respawnCameraPitch = 10f;

        [Header("Input")]
        [SerializeField, Tooltip("Actions asset with the 'Game' map. Empty = use the player's PlayerInput asset, or built-in keys.")]
        private InputActionAsset inputActions;
        [SerializeField, Tooltip("Action map holding Start, Pause and Restart.")]
        private string gameMapName = "Game";

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when the game is paused or resumed.")]
        private AudioClip pauseClip;
        [SerializeField, Tooltip("Played when the player respawns.")]
        private AudioClip respawnClip;
        [SerializeField, Tooltip("Played when the player wins.")]
        private AudioClip winClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for game-flow sounds.")]
        private float volume = 1f;

        private readonly List<InputAction> _ownedActions = new List<InputAction>(4);
        private InputActionMap _gameMap;
        private InputAction _startAction;
        private InputAction _pauseAction;
        private InputAction _restartAction;
        private InputAction _skipAction;

        private CandleManager _candle;
        private Transform _player;
        private ThirdPersonController _mover;
        private StarterAssetsInputs _starterInputs;
        private PlayerInput _playerInput;
        private CharacterController _controller;
        private PlayerKnockback _knockback;
        private LanternController _lantern;
        private Animator _playerAnimator;
        private bool _hasSpeedParam;

        private GameMode _mode = GameMode.Title;
        private float _modeTime;
        private float _playTime;
        private int _deaths;
        private Checkpoint _checkpoint;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private int _enableInputFrame = -1;
        private bool _evasion;
        private bool _fell;
        private int _lastHitFrame = -1;
        private bool _warnedCameraFields;
        private string _sceneName = string.Empty;
        private int _runIndex = -1;

        /// <summary>Raised on every mode change. Arguments: previous mode, next mode.</summary>
        public event Action<GameMode, GameMode> OnModeChanged;
        /// <summary>Raised when the first ghost starts noticing/chasing (true) or the last one stops (false).</summary>
        public event Action<bool> OnEvasionChanged;

        public GameMode Mode => _mode;
        /// <summary>Seconds played, not counting Title, Paused or Won.</summary>
        public float PlayTime => _playTime;
        /// <summary>How many times the lantern has gone out.</summary>
        public int Deaths => _deaths;
        /// <summary>True in the tutorial scene: untimed, and it can be skipped.</summary>
        public bool IsTutorial => LevelFlow.IsTutorial(_sceneName);
        /// <summary>True in a level of the timed run.</summary>
        public bool IsTimedLevel => _runIndex >= 0;
        /// <summary>Position of this scene in the timed run (0 = level 1), or -1.</summary>
        public int RunIndex => _runIndex;
        /// <summary>True if winning here leads on to another scene.</summary>
        public bool HasNextLevel => LevelFlow.NextScene(_sceneName) != null;
        /// <summary>Seconds spent on this level, restarts included. The same as PlayTime outside the run.</summary>
        public float LevelTime => LevelFlow.Carried(_runIndex) + _playTime;
        /// <summary>Seconds since level 1 started: the finished levels plus this one.</summary>
        public float RunTime => LevelFlow.TimeBefore(_runIndex) + LevelTime;
        /// <summary>Times the lantern has gone out on this level, restarts included.</summary>
        public int LevelDeaths => LevelFlow.CarriedDeaths(_runIndex) + _deaths;
        /// <summary>Times the lantern has gone out since level 1 started.</summary>
        public int RunDeaths => LevelFlow.DeathsBefore(_runIndex) + LevelDeaths;
        public Checkpoint CurrentCheckpoint => _checkpoint;
        /// <summary>True while playing and at least one ghost is in Notice or Chase.</summary>
        public bool IsEvasion => _evasion;
        /// <summary>Candle left, 0..1 (for the win screen).</summary>
        public float CandleLeft => _candle != null ? _candle.Normalized : 0f;
        /// <summary>Why the lantern last went out. Set before the mode changes to Extinguished.</summary>
        public ExtinguishCause LastExtinguishCause { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate instance destroyed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            GameEvents.Clear(); // drop subscribers left over from the previous scene
            Time.timeScale = 1f;

            _sceneName = SceneManager.GetActiveScene().name;
            _runIndex = LevelFlow.RunIndex(_sceneName);
            LevelFlow.EnterLevel(_runIndex);
        }

        private void Start()
        {
            _candle = CandleManager.Instance;
            if (_candle != null)
            {
                _player = _candle.transform;
                _mover = _candle.GetComponent<ThirdPersonController>();
                _starterInputs = _candle.GetComponent<StarterAssetsInputs>();
                _playerInput = _candle.GetComponent<PlayerInput>();
                _controller = _candle.GetComponent<CharacterController>();
                _knockback = _candle.GetComponent<PlayerKnockback>();
                _lantern = _candle.GetComponent<LanternController>();
                _playerAnimator = _candle.GetComponent<Animator>();
                _hasSpeedParam = AnimatorParams.Has(_playerAnimator, SpeedParam, AnimatorControllerParameterType.Float);
                _startPosition = _player.position;
                _startRotation = _player.rotation;
                _candle.OnExtinguished += HandleExtinguished;
                GameEvents.PlayerHit += HandlePlayerHit;
            }
            else
            {
                Debug.LogWarning("[GameManager] No CandleManager (player) in the scene; respawn and input control are disabled.", this);
            }

            SetUpActions();

            bool title = startAtTitle && !s_skipTitleOnce;
            s_skipTitleOnce = false;
            _mode = title ? GameMode.Title : GameMode.Playing;
            EnterMode(_mode);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (_candle != null) _candle.OnExtinguished -= HandleExtinguished;
            GameEvents.PlayerHit -= HandlePlayerHit;
            if (_gameMap != null) _gameMap.Disable();
            for (int i = 0; i < _ownedActions.Count; i++) _ownedActions[i].Dispose();
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Title -> Playing.</summary>
        public void StartGame()
        {
            if (_mode == GameMode.Title) SetMode(GameMode.Playing);
        }

        /// <summary>Playing -> Paused.</summary>
        public void Pause()
        {
            if (_mode != GameMode.Playing) return;
            Sfx.Play2D(pauseClip, volume);
            SetMode(GameMode.Paused);
        }

        /// <summary>Paused -> Playing.</summary>
        public void Resume()
        {
            if (_mode != GameMode.Paused) return;
            Sfx.Play2D(pauseClip, volume);
            SetMode(GameMode.Playing);
        }

        /// <summary>Playing -> Won. Returns false if the game is not being played or the candle is out.</summary>
        public bool Win()
        {
            if (_mode != GameMode.Playing) return false;
            if (_candle != null && !_candle.IsLit) return false;
            Sfx.Play2D(winClip, volume);
            // Before the mode changes, so the win screen reads the finished split.
            if (IsTimedLevel) LevelFlow.Finish(_runIndex, LevelTime, LevelDeaths);
            SetMode(GameMode.Won);
            GameEvents.RaiseLevelCompleted();
            return true;
        }

        /// <summary>
        /// Reloads the scene and goes straight to Playing. In level 1 that is a new run; in a
        /// later level the run clock keeps the time already spent there.
        /// </summary>
        public void Restart()
        {
            if (_runIndex > 0) LevelFlow.KeepTime(_runIndex, LevelTime, LevelDeaths);
            s_skipTitleOnce = true;
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        /// <summary>Won -> the next scene, straight to Playing. Returns false if nothing follows this scene.</summary>
        public bool NextLevel()
        {
            if (_mode != GameMode.Won) return false;
            return LoadFlowScene(LevelFlow.NextScene(_sceneName));
        }

        /// <summary>Won at the end of the run -> level 1 and a new run.</summary>
        public bool NewRun()
        {
            if (_mode != GameMode.Won) return false;
            return LoadFlowScene(LevelFlow.FirstRunScene);
        }

        /// <summary>Tutorial only, from the title or the pause screen: straight to level 1.</summary>
        public bool SkipTutorial()
        {
            if (!IsTutorial || (_mode != GameMode.Title && _mode != GameMode.Paused)) return false;
            return LoadFlowScene(LevelFlow.FirstRunScene);
        }

        private bool LoadFlowScene(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"[GameManager] Scene '{scene}' is not in the build's scene list (File > Build Profiles), so it cannot be loaded.", this);
                return false;
            }
            s_skipTitleOnce = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
            return true;
        }

        /// <summary>Quits the game (stops Play mode in the Editor).</summary>
        public void Quit()
        {
            GameEvents.RaiseQuitRequested();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Called by Checkpoint. The last checkpoint touched is where the player respawns.</summary>
        public void SetCheckpoint(Checkpoint checkpoint) => _checkpoint = checkpoint;

        // ------------------------------------------------------------------ FSM

        private void Update()
        {
            if (_enableInputFrame >= 0 && Time.frameCount >= _enableInputFrame && _mode == GameMode.Playing)
            {
                _enableInputFrame = -1;
                SetGameplayInput(true);
            }

            float unscaled = Time.unscaledDeltaTime;
            // The clock does not count a frame that hung (a scene loading in, a hitch): the
            // run time starts at zero when level 1 appears, not when it started loading.
            float played = Mathf.Min(unscaled, Time.maximumDeltaTime);
            switch (_mode)
            {
                case GameMode.Title:
                    _modeTime += unscaled;
                    if (_modeTime < titleInputDelay) break;
                    // Skip is checked first: Start is bound to any key, Tab included.
                    if (IsTutorial && _skipAction.WasPressedThisFrame() && SkipTutorial()) break;
                    if (_startAction.WasPressedThisFrame()) StartGame();
                    break;

                case GameMode.Playing:
                    _playTime += played;
                    if (GhostAI.AnyAlert != _evasion) SetEvasion(GhostAI.AnyAlert);
                    if (_player != null && _player.position.y < fallDeathHeight && _candle.IsLit)
                    {
                        _fell = true;
                        _candle.SetTo(0f);
                    }
                    if (_pauseAction.WasPressedThisFrame()) Pause();
                    break;

                case GameMode.Paused:
                    if (_pauseAction.WasPressedThisFrame()) Resume();
                    break;

                case GameMode.Extinguished:
                    _playTime += played;
                    _modeTime += Time.deltaTime;
                    if (_modeTime >= respawnDelay) Respawn();
                    break;

                case GameMode.Won:
                    if (_restartAction.WasPressedThisFrame()) Restart();
                    break;
            }
        }

        private void HandlePlayerHit(Vector3 sourcePosition) => _lastHitFrame = Time.frameCount;

        private void HandleExtinguished()
        {
            bool fell = _fell;
            _fell = false;
            if (_mode != GameMode.Playing) return;

            // A ghost hit raises PlayerHit and takes the candle in the same frame. The lantern
            // still reports the flare here: it only drops it on its next Update.
            if (fell) LastExtinguishCause = ExtinguishCause.Fell;
            else if (_lastHitFrame == Time.frameCount) LastExtinguishCause = ExtinguishCause.GhostHit;
            else if (_lantern != null && _lantern.IsFlaring) LastExtinguishCause = ExtinguishCause.FlaredOut;
            else LastExtinguishCause = ExtinguishCause.Drained;

            _deaths++;
            SetMode(GameMode.Extinguished);
        }

        private void SetMode(GameMode next)
        {
            GameMode previous = _mode;
            if (previous == next) return;
            _mode = next;
            EnterMode(next);
            OnModeChanged?.Invoke(previous, next);
        }

        private void EnterMode(GameMode mode)
        {
            _modeTime = 0f;
            switch (mode)
            {
                case GameMode.Title:
                    Time.timeScale = 0f;
                    SetCandlePaused(true);
                    SetGameplayInput(false);
                    SetCursorLocked(false);
                    break;

                case GameMode.Playing:
                    Time.timeScale = 1f;
                    SetCandlePaused(false);
                    SetCursorLocked(true);
                    // One frame later, so the key or click that started/resumed the game is never
                    // seen by the Throw or Flare actions.
                    _enableInputFrame = Time.frameCount + 1;
                    break;

                case GameMode.Paused:
                    Time.timeScale = 0f;
                    SetCandlePaused(true);
                    SetGameplayInput(false);
                    SetCursorLocked(false);
                    break;

                case GameMode.Extinguished:
                    Time.timeScale = 1f;
                    // Input goes off but ThirdPersonController keeps running on zeroed input, so
                    // its own gravity, ground check and landing finish a jump or a fall. Disabling
                    // it would leave the player hanging in the air until the respawn.
                    SetGameplayInput(false, true);
                    SetCursorLocked(true);
                    SetEvasion(false);
                    if (HUD.Instance != null) HUD.Instance.ShowPrompt(extinguishedMessage, respawnDelay);
                    break;

                case GameMode.Won:
                    Time.timeScale = 0f;
                    SetCandlePaused(true);
                    SetGameplayInput(false);
                    SetCursorLocked(false);
                    SetEvasion(false);
                    break;
            }
        }

        private void Respawn()
        {
            Vector3 position = _startPosition;
            Quaternion rotation = _startRotation;
            float candleFraction = defaultRespawnCandle;
            if (_checkpoint != null)
            {
                position = _checkpoint.RespawnPosition;
                rotation = _checkpoint.RespawnRotation;
                candleFraction = _checkpoint.MinCandleOnRespawn;
            }
            else if (startPoint != null)
            {
                position = startPoint.position;
                rotation = startPoint.rotation;
            }

            if (_player != null)
            {
                // A CharacterController overrides transform changes unless it is disabled first.
                float yaw = rotation.eulerAngles.y;
                if (_controller != null) _controller.enabled = false;
                _player.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                if (_controller != null) _controller.enabled = true;
                SnapCameraBehindPlayer(yaw);
            }

            if (_candle != null) _candle.SetTo(Mathf.Max(_candle.Current, candleFraction * _candle.Max));
            if (_knockback != null) _knockback.ResetState();

            // Candy first (thrown or landed), so a ghost cannot re-claim one that is on its way out.
            Candy.ClearAll();
            IReadOnlyList<GhostAI> ghosts = GhostAI.All;
            for (int i = 0; i < ghosts.Count; i++) ghosts[i].ResetToPatrol();

            // Everyone has just been moved; the plates and gates below read the physics scene.
            Physics.SyncTransforms();
            IReadOnlyList<PressurePlate> plates = PressurePlate.All;
            for (int i = 0; i < plates.Count; i++) plates[i].ResetForRespawn();
            IReadOnlyList<Gate> gates = Gate.All;
            for (int i = 0; i < gates.Count; i++) gates[i].ResetForRespawn();

            Sfx.Play2D(respawnClip, volume);
            SetMode(GameMode.Playing);
            GameEvents.RaisePlayerRespawned();
        }

        /// <summary>
        /// Points the follow camera along <paramref name="yaw"/> and cuts to it. Starter Assets
        /// rebuilds PlayerCameraRoot's rotation from two private angles every LateUpdate, so
        /// those are set as well as the rotation itself; the Cinemachine camera is then told
        /// its previous state is invalid, which skips the damping for one frame instead of
        /// letting the camera glide across the map.
        /// </summary>
        private void SnapCameraBehindPlayer(float yaw)
        {
            if (_mover == null || _mover.CinemachineCameraTarget == null) return;

            if (s_cameraYawField != null && s_cameraPitchField != null)
            {
                s_cameraYawField.SetValue(_mover, yaw);
                s_cameraPitchField.SetValue(_mover, respawnCameraPitch);
            }
            else if (!_warnedCameraFields)
            {
                _warnedCameraFields = true;
                Debug.LogWarning("[GameManager] ThirdPersonController's camera angle fields were not found; the camera keeps its old direction after a respawn.", this);
            }
            // The fall speed built up before the respawn must not carry over.
            if (s_verticalVelocityField != null) s_verticalVelocityField.SetValue(_mover, 0f);

            Transform target = _mover.CinemachineCameraTarget.transform;
            target.rotation = Quaternion.Euler(respawnCameraPitch + _mover.CameraAngleOverride, yaw, 0f);

            for (int i = 0; i < CinemachineCore.VirtualCameraCount; i++)
            {
                CinemachineVirtualCameraBase vcam = CinemachineCore.GetVirtualCamera(i);
                if (vcam != null && vcam.Follow == target) vcam.PreviousStateIsValid = false;
            }
        }

        private void SetEvasion(bool evasion)
        {
            if (_evasion == evasion) return;
            _evasion = evasion;
            OnEvasionChanged?.Invoke(evasion);
        }

        // ------------------------------------------------------------------ player control

        // keepMoving: leave ThirdPersonController running (on zeroed input) while input is off.
        private void SetGameplayInput(bool enabled, bool keepMoving = false)
        {
            if (!enabled) _enableInputFrame = -1;
            if (_mover != null) _mover.enabled = enabled || keepMoving;
            if (_playerInput != null)
            {
                if (enabled) _playerInput.ActivateInput();
                else _playerInput.DeactivateInput();
            }
            if (enabled) return;

            // StarterAssetsInputs only changes on input messages, so a key held when input is
            // switched off would otherwise stay "held".
            if (_starterInputs != null)
            {
                _starterInputs.move = Vector2.zero;
                _starterInputs.look = Vector2.zero;
                _starterInputs.jump = false;
                _starterInputs.sprint = false;
            }
            if (_hasSpeedParam) _playerAnimator.SetFloat(SpeedParam, 0f);
        }

        private void SetCandlePaused(bool paused)
        {
            if (_candle != null) _candle.Paused = paused;
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (_starterInputs == null) return;
            // StarterAssetsInputs re-applies its own flag whenever the window regains focus.
            _starterInputs.cursorLocked = locked;
            _starterInputs.cursorInputForLook = locked;
        }

        // ------------------------------------------------------------------ input

        private void SetUpActions()
        {
            InputActionAsset asset = inputActions;
            if (asset == null && _playerInput != null) asset = _playerInput.actions;
            _gameMap = asset != null ? asset.FindActionMap(gameMapName) : null;

            _startAction = ResolveAction("Start", "<Keyboard>/anyKey", "<Mouse>/leftButton");
            _pauseAction = ResolveAction("Pause", "<Keyboard>/escape", null);
            _restartAction = ResolveAction("Restart", "<Keyboard>/r", null);
            _skipAction = ResolveAction("SkipTutorial", "<Keyboard>/tab", null);
            if (_gameMap != null) _gameMap.Enable();
        }

        private InputAction ResolveAction(string actionName, string binding, string secondBinding)
        {
            InputAction action = _gameMap != null ? _gameMap.FindAction(actionName) : null;
            if (action != null) return action;

            action = new InputAction(actionName, InputActionType.Button);
            action.AddBinding(binding);
            if (secondBinding != null) action.AddBinding(secondBinding);
            action.Enable();
            _ownedActions.Add(action);
            return action;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            s_skipTitleOnce = false;
        }
    }
}

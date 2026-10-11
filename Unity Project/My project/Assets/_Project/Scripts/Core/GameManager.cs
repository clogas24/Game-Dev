using System;
using System.Collections.Generic;
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
    /// <code>
    ///   scene load ----------------------> TITLE     (Start At Title, first load)
    ///   scene load after Restart() ------> PLAYING   (title skipped)
    ///
    ///   TITLE        -- Start (any key / click) after titleInputDelay --> PLAYING
    ///   PLAYING      -- Pause (Esc) ------------------------------------> PAUSED
    ///   PAUSED       -- Pause (Esc) or Resume button -------------------> PLAYING
    ///   PLAYING      -- candle reaches 0 -------------------------------> EXTINGUISHED
    ///   EXTINGUISHED -- respawnDelay elapsed (respawn at checkpoint) ---> PLAYING
    ///   PLAYING      -- GoalTrigger reached with the candle lit --------> WON
    ///   PAUSED       -- Restart button ---------------------------------> scene reload
    ///   WON          -- Restart (R) or Restart button ------------------> scene reload
    /// </code>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static bool s_skipTitleOnce;

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

        private readonly List<InputAction> _ownedActions = new List<InputAction>(3);
        private InputActionMap _gameMap;
        private InputAction _startAction;
        private InputAction _pauseAction;
        private InputAction _restartAction;

        private CandleManager _candle;
        private Transform _player;
        private ThirdPersonController _mover;
        private StarterAssetsInputs _starterInputs;
        private PlayerInput _playerInput;
        private CharacterController _controller;
        private PlayerKnockback _knockback;
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

        /// <summary>Raised on every mode change. Arguments: previous mode, next mode.</summary>
        public event Action<GameMode, GameMode> OnModeChanged;
        /// <summary>Raised when the first ghost starts noticing/chasing (true) or the last one stops (false).</summary>
        public event Action<bool> OnEvasionChanged;

        public GameMode Mode => _mode;
        /// <summary>Seconds played, not counting Title, Paused or Won.</summary>
        public float PlayTime => _playTime;
        /// <summary>How many times the lantern has gone out.</summary>
        public int Deaths => _deaths;
        public Checkpoint CurrentCheckpoint => _checkpoint;
        /// <summary>True while playing and at least one ghost is in Notice or Chase.</summary>
        public bool IsEvasion => _evasion;
        /// <summary>Candle left, 0..1 (for the win screen).</summary>
        public float CandleLeft => _candle != null ? _candle.Normalized : 0f;

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
                _playerAnimator = _candle.GetComponent<Animator>();
                _hasSpeedParam = AnimatorParams.Has(_playerAnimator, SpeedParam, AnimatorControllerParameterType.Float);
                _startPosition = _player.position;
                _startRotation = _player.rotation;
                _candle.OnExtinguished += HandleExtinguished;
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
            SetMode(GameMode.Won);
            GameEvents.RaiseLevelCompleted();
            return true;
        }

        /// <summary>Reloads the scene and goes straight to Playing.</summary>
        public void Restart()
        {
            s_skipTitleOnce = true;
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        /// <summary>Quits the game (stops Play mode in the Editor).</summary>
        public void Quit()
        {
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
            switch (_mode)
            {
                case GameMode.Title:
                    _modeTime += unscaled;
                    if (_modeTime >= titleInputDelay && _startAction.WasPressedThisFrame()) StartGame();
                    break;

                case GameMode.Playing:
                    _playTime += unscaled;
                    if (GhostAI.AnyAlert != _evasion) SetEvasion(GhostAI.AnyAlert);
                    if (_player != null && _player.position.y < fallDeathHeight && _candle.IsLit) _candle.SetTo(0f);
                    if (_pauseAction.WasPressedThisFrame()) Pause();
                    break;

                case GameMode.Paused:
                    if (_pauseAction.WasPressedThisFrame()) Resume();
                    break;

                case GameMode.Extinguished:
                    _playTime += unscaled;
                    _modeTime += Time.deltaTime;
                    if (_modeTime >= respawnDelay) Respawn();
                    break;

                case GameMode.Won:
                    if (_restartAction.WasPressedThisFrame()) Restart();
                    break;
            }
        }

        private void HandleExtinguished()
        {
            if (_mode != GameMode.Playing) return;
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
                    SetGameplayInput(false);
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
                Vector3 before = _player.position;
                if (_controller != null) _controller.enabled = false;
                _player.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
                if (_controller != null) _controller.enabled = true;
                if (_mover != null && _mover.CinemachineCameraTarget != null)
                {
                    CinemachineCore.OnTargetObjectWarped(_mover.CinemachineCameraTarget.transform, _player.position - before);
                }
            }

            if (_candle != null) _candle.SetTo(Mathf.Max(_candle.Current, candleFraction * _candle.Max));
            if (_knockback != null) _knockback.ResetState();

            IReadOnlyList<GhostAI> ghosts = GhostAI.All;
            for (int i = 0; i < ghosts.Count; i++) ghosts[i].ResetToPatrol();

            IReadOnlyList<Candy> candy = Candy.Landed;
            for (int i = candy.Count - 1; i >= 0; i--) Destroy(candy[i].gameObject);

            Sfx.Play2D(respawnClip, volume);
            SetMode(GameMode.Playing);
            GameEvents.RaisePlayerRespawned();
        }

        private void SetEvasion(bool evasion)
        {
            if (_evasion == evasion) return;
            _evasion = evasion;
            OnEvasionChanged?.Invoke(evasion);
        }

        // ------------------------------------------------------------------ player control

        private void SetGameplayInput(bool enabled)
        {
            if (!enabled) _enableInputFrame = -1;
            if (_mover != null) _mover.enabled = enabled;
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

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LanternKeeper
{
    /// <summary>
    /// Title, Pause and Win panels. Shows the panel that matches GameManager.Mode, builds the
    /// title's controls list from the real input bindings, wires the pause buttons, and ties the
    /// sensitivity slider to KeyboardCameraLook.Sensitivity (saved in PlayerPrefs). The music
    /// and sound sliders set GameAudio.MusicVolume and GameAudio.SfxVolume (also saved).
    /// Menus work with the mouse and with the keyboard alone (arrows / WASD + Enter), because
    /// the first button of each panel is selected when it opens.
    /// The texts follow the scene's place in LevelFlow: the tutorial's title and pause screens
    /// offer to skip it, and the win screen's button is Continue while a level follows, with
    /// the level time, and at the end of the run the splits and the run time.
    /// Setup: on a Screen Space Overlay canvas with a GraphicRaycaster, plus an EventSystem with
    /// an InputSystemUIInputModule in the scene. Wire the three panels and their widgets. It
    /// finds GameManager.Instance and the player by itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameMenus : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField, Tooltip("Shown in Title mode.")]
        private GameObject titlePanel;
        [SerializeField, Tooltip("Shown in Paused mode.")]
        private GameObject pausePanel;
        [SerializeField, Tooltip("Shown in Won mode.")]
        private GameObject winPanel;

        [Header("Title")]
        [SerializeField, Tooltip("Text filled with the controls list, built from the input bindings.")]
        private TMP_Text controlsLabel;
        [SerializeField, Tooltip("The 'press any key' line. In the tutorial it also says how to skip. Optional.")]
        private TMP_Text startHintLabel;

        [Header("Pause")]
        [SerializeField, Tooltip("Resumes the game.")]
        private Button resumeButton;
        [SerializeField, Tooltip("Restarts the level.")]
        private Button restartButton;
        [SerializeField, Tooltip("Quits the game.")]
        private Button quitButton;
        [SerializeField, Tooltip("Goes straight to level 1. Only shown in the tutorial. Optional.")]
        private Button skipTutorialButton;
        [SerializeField, Tooltip("Camera sensitivity slider (0.1 to 3).")]
        private Slider sensitivitySlider;
        [SerializeField, Tooltip("Text showing the current sensitivity value.")]
        private TMP_Text sensitivityValueLabel;
        [SerializeField, Tooltip("Music volume slider (0 to 1).")]
        private Slider musicSlider;
        [SerializeField, Tooltip("Text showing the music volume.")]
        private TMP_Text musicValueLabel;
        [SerializeField, Tooltip("Sound effect and ambience volume slider (0 to 1).")]
        private Slider sfxSlider;
        [SerializeField, Tooltip("Text showing the sound volume.")]
        private TMP_Text sfxValueLabel;

        [Header("Win")]
        [SerializeField, Tooltip("Text filled with time, times extinguished and candle left.")]
        private TMP_Text winStatsLabel;
        [SerializeField, Tooltip("The win screen's button: Continue while a level follows, New run at the end of the run, Play again elsewhere.")]
        private Button winRestartButton;
        [SerializeField, Tooltip("Heading of the win screen. Optional: without it the prefab's text stays.")]
        private TMP_Text winHeadingLabel;
        [SerializeField, Tooltip("Line under the win screen's button that names its keys. Optional.")]
        private TMP_Text winHintLabel;

        private GameManager _game;
        private KeyboardCameraLook _cameraLook;

        private void Start()
        {
            _game = GameManager.Instance;
            CandleManager candle = CandleManager.Instance;
            if (candle != null) candle.TryGetComponent(out _cameraLook);

            if (resumeButton != null) resumeButton.onClick.AddListener(HandleResume);
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
            if (winRestartButton != null) winRestartButton.onClick.AddListener(HandleWinButton);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
            if (skipTutorialButton != null)
            {
                skipTutorialButton.onClick.AddListener(HandleSkipTutorial);
                skipTutorialButton.gameObject.SetActive(_game != null && _game.IsTutorial);
            }
            if (startHintLabel != null && _game != null && _game.IsTutorial)
            {
                startHintLabel.text = "Press any key to start the tutorial  -  Tab skips it";
            }

            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = 0.1f;
                sensitivitySlider.maxValue = 3f;
                float saved = _cameraLook != null
                    ? _cameraLook.Sensitivity
                    : PlayerPrefs.GetFloat(KeyboardCameraLook.SensitivityPrefKey, 1f);
                sensitivitySlider.SetValueWithoutNotify(saved);
                ShowSensitivity(saved);
                sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);
            }

            SetUpVolumeSlider(musicSlider, musicValueLabel, GameAudio.MusicVolume, HandleMusicChanged);
            SetUpVolumeSlider(sfxSlider, sfxValueLabel, GameAudio.SfxVolume, HandleSfxChanged);

            if (controlsLabel != null) controlsLabel.text = BuildControlsText(candle);

            if (_game != null)
            {
                _game.OnModeChanged += HandleModeChanged;
                ShowMode(_game.Mode);
            }
            else
            {
                Debug.LogWarning("[GameMenus] No GameManager in the scene; menus stay hidden.", this);
                SetActive(titlePanel, false);
                SetActive(pausePanel, false);
                SetActive(winPanel, false);
            }
        }

        private void OnDestroy()
        {
            if (_game != null) _game.OnModeChanged -= HandleModeChanged;
        }

        private void HandleModeChanged(GameMode previous, GameMode next) => ShowMode(next);

        private void ShowMode(GameMode mode)
        {
            SetActive(titlePanel, mode == GameMode.Title);
            SetActive(pausePanel, mode == GameMode.Paused);
            SetActive(winPanel, mode == GameMode.Won);

            if (mode == GameMode.Paused)
            {
                Select(resumeButton);
            }
            else if (mode == GameMode.Won)
            {
                if (winStatsLabel != null) winStatsLabel.text = BuildWinText();
                ShowWinTexts();
                Select(winRestartButton);
            }
            else Select(null);
        }

        private void HandleResume()
        {
            if (_game != null) _game.Resume();
        }

        private void HandleRestart()
        {
            if (_game != null) _game.Restart();
        }

        private void HandleQuit()
        {
            if (_game != null) _game.Quit();
        }

        private void HandleSkipTutorial()
        {
            if (_game != null) _game.SkipTutorial();
        }

        // Continue while a level follows, a new run at the end of the run, a plain restart elsewhere.
        private void HandleWinButton()
        {
            if (_game == null) return;
            if (_game.HasNextLevel && _game.NextLevel()) return;
            if (_game.IsTimedLevel && !_game.HasNextLevel && _game.NewRun()) return;
            _game.Restart();
        }

        private void ShowWinTexts()
        {
            string heading = null;
            string button = "Play again";
            string hint = "Press R to play again";
            if (_game.IsTutorial)
            {
                heading = "Tutorial complete!";
                button = "Start level 1";
                hint = "Enter starts level 1, where the clock starts  -  R replays the tutorial";
            }
            else if (_game.IsTimedLevel && _game.HasNextLevel)
            {
                heading = LevelFlow.RunName(_game.RunIndex) + " cleared!";
                button = "Continue";
                hint = "Enter continues  -  R restarts this level";
            }
            else if (_game.IsTimedLevel)
            {
                heading = "Run complete!";
                button = "New run";
                hint = "Enter starts a new run  -  R restarts this level";
            }

            if (winHeadingLabel != null && heading != null) winHeadingLabel.text = heading;
            if (winHintLabel != null) winHintLabel.text = hint;
            if (winRestartButton != null)
            {
                TMP_Text label = winRestartButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = button;
            }
        }

        private void HandleSensitivityChanged(float value)
        {
            if (_cameraLook != null) _cameraLook.Sensitivity = value;
            else PlayerPrefs.SetFloat(KeyboardCameraLook.SensitivityPrefKey, value);
            ShowSensitivity(value);
        }

        private void ShowSensitivity(float value)
        {
            if (sensitivityValueLabel != null) sensitivityValueLabel.SetText("{0:1}", value);
        }

        private static void SetUpVolumeSlider(Slider slider, TMP_Text label, float saved, UnityEngine.Events.UnityAction<float> handler)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(saved);
            ShowPercent(label, saved);
            slider.onValueChanged.AddListener(handler);
        }

        private void HandleMusicChanged(float value)
        {
            GameAudio.MusicVolume = value;
            ShowPercent(musicValueLabel, value);
        }

        private void HandleSfxChanged(float value)
        {
            GameAudio.SfxVolume = value;
            ShowPercent(sfxValueLabel, value);
        }

        private static void ShowPercent(TMP_Text label, float value)
        {
            if (label != null) label.SetText("{0}%", Mathf.Round(value * 100f));
        }

        private string BuildWinText()
        {
            int candle = Mathf.CeilToInt(_game.CandleLeft * 100f);
            if (_game.IsTutorial)
            {
                return $"Lantern went out  {Times(_game.Deaths)}\n"
                     + $"Candle left  {candle}%";
            }
            if (!_game.IsTimedLevel)
            {
                return $"Time  {LevelFlow.FormatTime(_game.PlayTime)}\n"
                     + $"Lantern went out  {Times(_game.Deaths)}\n"
                     + $"Candle left  {candle}%";
            }

            int index = _game.RunIndex;
            bool whole = LevelFlow.RunIsWhole(index);
            var text = new StringBuilder(160);
            if (_game.HasNextLevel || !whole)
            {
                text.Append("Level time  ").Append(LevelFlow.FormatTime(_game.LevelTime)).Append('\n');
                if (whole && index > 0) text.Append("Run time so far  ").Append(LevelFlow.FormatTime(_game.RunTime)).Append('\n');
                text.Append("Lantern went out  ").Append(Times(_game.LevelDeaths)).Append('\n');
            }
            else
            {
                // End of the run: every split, then the total.
                for (int i = 0; i <= index; i++)
                {
                    text.Append(LevelFlow.RunName(i)).Append("  ").Append(LevelFlow.FormatTime(LevelFlow.Split(i))).Append('\n');
                }
                text.Append("Run time  ").Append(LevelFlow.FormatTime(_game.RunTime)).Append('\n');
                text.Append("Lantern went out  ").Append(Times(_game.RunDeaths)).Append('\n');
            }
            text.Append("Candle left  ").Append(candle).Append('%');
            return text.ToString();
        }

        private static string Times(int count) => count == 1 ? "1 time" : count + " times";

        // Built once from the bindings, so the list stays right if a key is rebound in the asset.
        private static string BuildControlsText(CandleManager player)
        {
            InputActionAsset actions = null;
            if (player != null && player.TryGetComponent(out PlayerInput playerInput)) actions = playerInput.actions;
            if (actions == null) return string.Empty;

            var text = new StringBuilder(256);
            AppendControl(text, actions, "Move", "Move", null);
            AppendControl(text, actions, "Turn camera", "LookKeys", "mouse");
            AppendControl(text, actions, "Jump", "Jump", null);
            AppendControl(text, actions, "Sprint (hold)", "Sprint", null);
            AppendControl(text, actions, "Flare (hold)", "Flare", null);
            AppendControl(text, actions, "Throw candy", "Throw", null);
            AppendControl(text, actions, "Drop candy", "Drop", null);
            AppendControl(text, actions, "Pause", "Pause", null);
            return text.ToString();
        }

        private static void AppendControl(StringBuilder text, InputActionAsset actions, string label, string actionName, string extra)
        {
            InputAction action = actions.FindAction(actionName);
            if (action == null) return;

            int shown = 0;
            if (text.Length > 0) text.Append('\n');
            text.Append(label).Append(":  ");
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].isPartOfComposite) continue; // the composite itself prints its parts, e.g. W/A/S/D
                string display = action.GetBindingDisplayString(i);
                if (string.IsNullOrEmpty(display)) continue;
                if (shown++ > 0) text.Append(" or ");
                text.Append(display);
            }
            if (extra != null)
            {
                if (shown > 0) text.Append(" or ");
                text.Append(extra);
            }
        }

        private static void SetActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active) panel.SetActive(active);
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
        }
    }
}

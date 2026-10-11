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
    /// sensitivity slider to KeyboardCameraLook.Sensitivity (saved in PlayerPrefs).
    /// Menus work with the mouse and with the keyboard alone (arrows / WASD + Enter), because
    /// the first button of each panel is selected when it opens.
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

        [Header("Pause")]
        [SerializeField, Tooltip("Resumes the game.")]
        private Button resumeButton;
        [SerializeField, Tooltip("Restarts the level.")]
        private Button restartButton;
        [SerializeField, Tooltip("Quits the game.")]
        private Button quitButton;
        [SerializeField, Tooltip("Camera sensitivity slider (0.1 to 3).")]
        private Slider sensitivitySlider;
        [SerializeField, Tooltip("Text showing the current sensitivity value.")]
        private TMP_Text sensitivityValueLabel;

        [Header("Win")]
        [SerializeField, Tooltip("Text filled with time, times extinguished and candle left.")]
        private TMP_Text winStatsLabel;
        [SerializeField, Tooltip("Restarts the level from the win screen.")]
        private Button winRestartButton;

        private GameManager _game;
        private KeyboardCameraLook _cameraLook;

        private void Start()
        {
            _game = GameManager.Instance;
            CandleManager candle = CandleManager.Instance;
            if (candle != null) candle.TryGetComponent(out _cameraLook);

            if (resumeButton != null) resumeButton.onClick.AddListener(HandleResume);
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
            if (winRestartButton != null) winRestartButton.onClick.AddListener(HandleRestart);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);

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

            if (mode == GameMode.Paused) Select(resumeButton);
            else if (mode == GameMode.Won)
            {
                if (winStatsLabel != null) winStatsLabel.text = BuildWinText();
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

        private string BuildWinText()
        {
            int seconds = Mathf.FloorToInt(_game.PlayTime);
            int candle = Mathf.CeilToInt(_game.CandleLeft * 100f);
            return $"Time  {seconds / 60}:{seconds % 60:00}\n"
                 + $"Lantern went out  {_game.Deaths} {(_game.Deaths == 1 ? "time" : "times")}\n"
                 + $"Candle left  {candle}%";
        }

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

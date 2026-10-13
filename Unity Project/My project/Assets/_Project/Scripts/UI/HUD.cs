using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LanternKeeper
{
    /// <summary>
    /// In-game HUD: candle meter (a candle that burns down, shifts orange to red and pulses when low), candy
    /// count, a centre prompt that fades in and out, and the run clock (GameManager.RunTime),
    /// which is only shown in the timed levels.
    /// Prompt text may contain {flare}, {throw} and {jump}; they are replaced with the name of
    /// the control bound to that action for the control scheme the player last used, read from
    /// the PlayerInput bindings, so prompts stay right if a binding or device changes.
    /// Setup: on a Screen Space Overlay canvas (singleton, <see cref="Instance"/>). Wire the
    /// fill RectTransform and Image, the three TMP texts and the prompt CanvasGroup. It finds
    /// the player through CandleManager.Instance, so there is nothing to wire to the player.
    /// </summary>
    [DisallowMultipleComponent]
    public class HUD : MonoBehaviour
    {
        public static HUD Instance { get; private set; }

        [Header("Candle meter")]
        [SerializeField, Tooltip("Bar that shrinks with the candle. Its anchorMax.x is set to the candle fraction.")]
        private RectTransform candleFill;
        [SerializeField, Tooltip("Image on the fill bar; tinted by candle level.")]
        private Image candleFillImage;
        [SerializeField, Tooltip("Flame drawn at the top of the candle meter; hidden while the lantern is out. Optional.")]
        private GameObject candleFlame;
        [SerializeField, Tooltip("Text showing the candle percentage.")]
        private TMP_Text candleLabel;
        [SerializeField, Tooltip("Bar colour at full candle.")]
        private Color fullColor = new Color(1f, 0.6f, 0.1f);
        [SerializeField, Tooltip("Bar colour at empty candle.")]
        private Color emptyColor = new Color(0.9f, 0.1f, 0.1f);
        [SerializeField, Min(0f), Tooltip("Pulses per second while the candle is low.")]
        private float pulseSpeed = 2.5f;
        [SerializeField, Range(0f, 1f), Tooltip("How far the bar brightens toward white at the top of each pulse.")]
        private float pulseStrength = 0.6f;

        [Header("Candy")]
        [SerializeField, Tooltip("Text showing candy held / max.")]
        private TMP_Text candyLabel;

        [Header("Run clock")]
        [SerializeField, Tooltip("Text showing the run time. Optional.")]
        private TMP_Text timerLabel;
        [SerializeField, Tooltip("The run clock's whole box (text and background). Hidden outside the timed levels. Empty = the text's own object.")]
        private GameObject timerRoot;

        [Header("Prompt")]
        [SerializeField, Tooltip("Centre-screen prompt text.")]
        private TMP_Text promptLabel;
        [SerializeField, Tooltip("CanvasGroup on the prompt, used for fading.")]
        private CanvasGroup promptGroup;
        [SerializeField, Min(0.01f), Tooltip("Seconds the prompt takes to fade in or out.")]
        private float promptFadeTime = 0.3f;
        [SerializeField, TextArea, Tooltip("Optional prompt shown when the scene starts. May use {flare}, {throw}, {jump}.")]
        private string startPrompt = "";
        [SerializeField, Min(0f), Tooltip("Seconds the start prompt stays up.")]
        private float startPromptSeconds = 6f;

        [Header("Fallback control names (used when no binding is found)")]
        [SerializeField, Tooltip("Shown for {flare}.")]
        private string flareFallback = "F";
        [SerializeField, Tooltip("Shown for {throw}.")]
        private string throwFallback = "E";
        [SerializeField, Tooltip("Shown for {drop}.")]
        private string dropFallback = "G";
        [SerializeField, Tooltip("Shown for {jump}.")]
        private string jumpFallback = "Space";

        private CandleManager _candle;
        private CandyThrower _thrower;
        private PlayerInput _playerInput;
        private float _normalized = 1f;
        private float _lowThreshold = 0.25f;
        private int _shownPercent = -1;
        private string _promptTemplate;
        private string _promptScheme;
        private float _promptTimer;
        private GameManager _game;
        private int _shownTenths = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[HUD] Duplicate instance destroyed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (promptGroup != null) promptGroup.alpha = 0f;
        }

        private void Start()
        {
            _candle = CandleManager.Instance;
            if (_candle != null)
            {
                _lowThreshold = _candle.LowThreshold;
                _candle.OnCandleChanged += HandleCandleChanged;
                HandleCandleChanged(_candle.Normalized);
                _candle.TryGetComponent(out _thrower);
                _candle.TryGetComponent(out _playerInput);
            }
            if (_thrower != null)
            {
                _thrower.OnCountChanged += HandleCandyChanged;
                HandleCandyChanged(_thrower.Count, _thrower.MaxCount);
            }
            else if (candyLabel != null)
            {
                candyLabel.SetText("Candy -");
            }

            _game = GameManager.Instance;
            if (timerRoot == null && timerLabel != null) timerRoot = timerLabel.gameObject;
            if (timerRoot != null) timerRoot.SetActive(_game != null && _game.IsTimedLevel);

            if (!string.IsNullOrEmpty(startPrompt)) ShowPrompt(startPrompt, startPromptSeconds);
        }

        private void OnDestroy()
        {
            if (_candle != null) _candle.OnCandleChanged -= HandleCandleChanged;
            if (_thrower != null) _thrower.OnCountChanged -= HandleCandyChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Shows a centre-screen prompt for a number of seconds, fading in and out.
        /// {flare}, {throw} and {jump} are replaced with the current control names.
        /// </summary>
        public void ShowPrompt(string text, float seconds)
        {
            if (promptLabel == null) return;
            _promptTemplate = text ?? string.Empty;
            _promptTimer = Mathf.Max(0f, seconds);
            RenderPrompt();
        }

        /// <summary>Fades the current prompt out now.</summary>
        public void HidePrompt() => _promptTimer = 0f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (candleFillImage != null && _normalized > 0f && _normalized < _lowThreshold)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);
                candleFillImage.color = Color.Lerp(BarColor(_normalized), Color.white, pulse * pulseStrength);
            }

            if (timerLabel != null && _game != null && _game.IsTimedLevel)
            {
                // The text only changes ten times a second, so that is how often it is rebuilt.
                int tenths = Mathf.FloorToInt(_game.RunTime * 10f);
                if (tenths != _shownTenths)
                {
                    _shownTenths = tenths;
                    timerLabel.text = LevelFlow.FormatTime(_game.RunTime);
                }
            }

            if (promptGroup == null) return;
            if (_promptTimer > 0f)
            {
                _promptTimer -= dt;
                // Re-render if the player switched device while the prompt is up.
                if (_playerInput != null && !ReferenceEquals(_playerInput.currentControlScheme, _promptScheme)) RenderPrompt();
            }
            float targetAlpha = _promptTimer > 0f ? 1f : 0f;
            if (!Mathf.Approximately(promptGroup.alpha, targetAlpha))
            {
                promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, targetAlpha, dt / promptFadeTime);
            }
        }

        private void HandleCandleChanged(float normalized)
        {
            _normalized = normalized;
            if (candleFill != null)
            {
                Vector2 max = candleFill.anchorMax;
                max.x = Mathf.Clamp01(normalized);
                candleFill.anchorMax = max;
            }
            if (candleFillImage != null) candleFillImage.color = BarColor(normalized);
            if (candleFlame != null && candleFlame.activeSelf != normalized > 0f) candleFlame.SetActive(normalized > 0f);

            int percent = Mathf.CeilToInt(normalized * 100f);
            if (candleLabel != null && percent != _shownPercent)
            {
                _shownPercent = percent;
                candleLabel.SetText("Candle {0}%", (float)percent);
            }
        }

        private void HandleCandyChanged(int count, int max)
        {
            if (candyLabel != null) candyLabel.SetText("Candy {0}/{1}", (float)count, (float)max);
        }

        private Color BarColor(float normalized) => Color.Lerp(emptyColor, fullColor, Mathf.Clamp01(normalized));

        private void RenderPrompt()
        {
            _promptScheme = _playerInput != null ? _playerInput.currentControlScheme : null;
            string text = _promptTemplate;
            if (text.IndexOf('{') >= 0)
            {
                text = text.Replace("{flare}", ControlName("Flare", flareFallback))
                           .Replace("{throw}", ControlName("Throw", throwFallback))
                           .Replace("{drop}", ControlName("Drop", dropFallback))
                           .Replace("{jump}", ControlName("Jump", jumpFallback));
            }
            promptLabel.text = text;
        }

        // Display name of the first binding of the action that belongs to the active control scheme.
        private string ControlName(string actionName, string fallback)
        {
            if (_playerInput == null || _playerInput.actions == null) return fallback;
            InputAction action = _playerInput.actions.FindAction(actionName);
            if (action == null) return fallback;

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;
                bool inScheme = string.IsNullOrEmpty(_promptScheme)
                                || (binding.groups != null && binding.groups.Contains(_promptScheme));
                if (!inScheme) continue;

                string display = action.GetBindingDisplayString(i);
                if (!string.IsNullOrEmpty(display)) return display;
            }
            return fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}

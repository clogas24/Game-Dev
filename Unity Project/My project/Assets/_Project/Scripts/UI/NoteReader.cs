using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
    /// <summary>
    /// Shows story notes on the HUD: a small "read the note" prompt while a StoryNote is in
    /// range, and the note itself on a parchment panel once the Throw key is pressed.
    /// Rules:
    /// - The Throw key opens the note in range, and closes an open note. Either way the press
    ///   is used up, so no candy is thrown. Out of range, Throw throws as always.
    /// - While a note is open the candle does not drain (CandleManager.Paused).
    /// - The note closes by itself when the player walks away, starts a flare, is hit, or the
    ///   game leaves Playing (pause, lantern out, win). Starting a flare closes it because a
    ///   paused candle would otherwise make the flare free.
    /// The world keeps running while a note is open; only the candle waits.
    /// Setup: on the HUD canvas (singleton). Wire the two CanvasGroups and the texts.
    /// </summary>
    [DisallowMultipleComponent]
    public class NoteReader : MonoBehaviour
    {
        public static NoteReader Instance { get; private set; }

        [Header("In-range prompt")]
        [SerializeField, Tooltip("CanvasGroup of the small prompt shown while a note is in range.")]
        private CanvasGroup promptGroup;
        [SerializeField, Tooltip("Text of the in-range prompt.")]
        private TMP_Text promptLabel;
        [SerializeField, Tooltip("Prompt text. {key} is replaced with the control bound to Throw.")]
        private string promptText = "{key}   Read the note";

        [Header("Note panel")]
        [SerializeField, Tooltip("CanvasGroup of the parchment panel.")]
        private CanvasGroup panelGroup;
        [SerializeField, Tooltip("Heading text.")]
        private TMP_Text titleLabel;
        [SerializeField, Tooltip("Body text.")]
        private TMP_Text bodyLabel;
        [SerializeField, Tooltip("Hint under the note.")]
        private TMP_Text hintLabel;
        [SerializeField, Tooltip("Hint text. {key} is replaced with the control bound to Throw.")]
        private string hintText = "{key} to put the note back   -   your candle waits while you read";
        [SerializeField, Min(0.01f), Tooltip("Seconds the prompt and the panel take to fade.")]
        private float fadeSeconds = 0.2f;

        [Header("Input")]
        [SerializeField, Tooltip("Action whose key reads notes. It is the Throw action: near a note it reads instead of throwing.")]
        private string interactActionName = "Throw";
        [SerializeField, Tooltip("Shown for {key} when no binding is found.")]
        private string keyFallback = "E";

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when a note is opened.")]
        private AudioClip openClip;
        [SerializeField, Tooltip("Played when a note is closed.")]
        private AudioClip closeClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for note sounds.")]
        private float volume = 0.8f;

        private CandleManager _candle;
        private LanternController _lantern;
        private PlayerInput _playerInput;
        private GameManager _game;
        private StoryNote _open;
        private StoryNote _prompted;
        private string _keyScheme;
        private string _keyName;

        /// <summary>The note being read, or null.</summary>
        public StoryNote OpenNote => _open;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (promptGroup != null) promptGroup.alpha = 0f;
            if (panelGroup != null) panelGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            if (Instance == this) GameEvents.PlayerHit += HandlePlayerHit;
        }

        private void OnDisable() => GameEvents.PlayerHit -= HandlePlayerHit;

        private void Start()
        {
            _game = GameManager.Instance;
            _candle = CandleManager.Instance;
            if (_candle == null) return;
            _candle.TryGetComponent(out _lantern);
            _candle.TryGetComponent(out _playerInput);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// The Throw key was pressed. Closes the open note, or opens the one in range. Returns
        /// true if the press was used, so the caller does not throw.
        /// </summary>
        public bool HandleInteract()
        {
            if (_open != null)
            {
                Close();
                return true;
            }

            StoryNote note = StoryNote.InRange;
            if (note == null || !CanRead()) return false;
            Open(note);
            return true;
        }

        private bool CanRead()
        {
            if (_game != null && _game.Mode != GameMode.Playing) return false;
            if (_candle != null && !_candle.IsLit) return false;
            return _lantern == null || !_lantern.IsFlaring;
        }

        private void Open(StoryNote note)
        {
            _open = note;
            if (titleLabel != null) titleLabel.text = note.Title;
            if (bodyLabel != null) bodyLabel.text = note.Body;
            if (hintLabel != null) hintLabel.text = hintText.Replace("{key}", KeyName());
            if (_candle != null) _candle.Paused = true;
            if (HUD.Instance != null) HUD.Instance.HidePrompt(); // the note covers the centre prompt
            Sfx.Play2D(openClip, volume);
            GameEvents.RaiseNoteRead(note.NoteId);
        }

        private void Close()
        {
            if (_open == null) return;
            _open = null;
            // Outside Playing the game manager owns the pause flag (title, pause and win all keep it set).
            if (_candle != null && (_game == null || _game.Mode == GameMode.Playing)) _candle.Paused = false;
            Sfx.Play2D(closeClip, volume);
        }

        private void HandlePlayerHit(Vector3 sourcePosition) => Close();

        private void Update()
        {
            if (_open != null)
            {
                bool playing = _game == null || _game.Mode == GameMode.Playing;
                bool flaring = _lantern != null && _lantern.IsFlaring;
                if (!playing || flaring || StoryNote.InRange != _open) Close();
                // Resuming from the pause menu clears the flag; a note that is still open sets it again.
                else if (_candle != null && !_candle.Paused) _candle.Paused = true;
            }

            StoryNote inRange = _open == null && CanRead() ? StoryNote.InRange : null;
            if (inRange != null && promptLabel != null)
            {
                string scheme = _playerInput != null ? _playerInput.currentControlScheme : null;
                if (inRange != _prompted || !ReferenceEquals(scheme, _keyScheme))
                {
                    _prompted = inRange;
                    promptLabel.text = promptText.Replace("{key}", KeyName());
                }
            }

            float step = Time.unscaledDeltaTime / fadeSeconds;
            if (promptGroup != null) promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, inRange != null ? 1f : 0f, step);
            if (panelGroup != null) panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, _open != null ? 1f : 0f, step);
        }

        // Display name of the first Throw binding that belongs to the control scheme in use.
        private string KeyName()
        {
            string scheme = _playerInput != null ? _playerInput.currentControlScheme : null;
            if (_keyName != null && ReferenceEquals(scheme, _keyScheme)) return _keyName;
            _keyScheme = scheme;
            _keyName = keyFallback;

            InputAction action = _playerInput != null && _playerInput.actions != null
                ? _playerInput.actions.FindAction(interactActionName)
                : null;
            if (action == null) return _keyName;

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;
                bool inScheme = string.IsNullOrEmpty(scheme) || (binding.groups != null && binding.groups.Contains(scheme));
                if (!inScheme) continue;
                string display = action.GetBindingDisplayString(i);
                if (string.IsNullOrEmpty(display)) continue;
                _keyName = display;
                break;
            }
            return _keyName;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}

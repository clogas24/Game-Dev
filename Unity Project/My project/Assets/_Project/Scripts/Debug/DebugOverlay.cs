using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
#endif

namespace LanternKeeper
{
    /// <summary>
    /// Developer overlay, toggled with F1 and hidden by default. Shows the game mode, the player
    /// FSM state, the candle, every ghost's name and FSM state, and FPS; optionally draws each
    /// ghost's state above its head. Useful for playtest notes and FSM screenshots.
    /// Setup: on any object in the scene (the GameManager object is fine). Nothing to wire.
    /// The whole body is compiled only in the Editor and in Development Builds; in a release
    /// build this component is an empty shell that does nothing.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("Show the overlay as soon as the scene starts.")]
        private bool visibleAtStart;
        [SerializeField, Tooltip("Also draw each ghost's state above its head.")]
        private bool labelGhosts = true;
        [SerializeField, Min(0.05f), Tooltip("Seconds between refreshes of the overlay text.")]
        private float refreshInterval = 0.25f;
        [SerializeField, Min(8), Tooltip("Overlay font size (pixels at 1080p; scales with screen height).")]
        private int fontSize = 18;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly StringBuilder _text = new StringBuilder(512);
        private InputAction _toggleAction;
        private PlayerStateTracker _playerState;
        private Camera _camera;
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private string _shownText = string.Empty;
        private bool _visible;
        private float _refreshTimer;
        private float _fpsTime;
        private int _fpsFrames;
        private float _fps;

        private void Awake()
        {
            _visible = visibleAtStart;
            _toggleAction = new InputAction("ToggleDebugOverlay", InputActionType.Button, "<Keyboard>/f1");
        }

        private void OnEnable() => _toggleAction.Enable();

        private void OnDisable() => _toggleAction.Disable();

        private void OnDestroy() => _toggleAction.Dispose();

        private void Start()
        {
            if (CandleManager.Instance != null) CandleManager.Instance.TryGetComponent(out _playerState);
        }

        private void Update()
        {
            if (_toggleAction.WasPressedThisFrame()) _visible = !_visible;

            _fpsFrames++;
            _fpsTime += Time.unscaledDeltaTime;
            if (_fpsTime >= 0.5f)
            {
                _fps = _fpsFrames / _fpsTime;
                _fpsFrames = 0;
                _fpsTime = 0f;
            }

            if (!_visible) return;
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = refreshInterval;
            RebuildText();
        }

        private void RebuildText()
        {
            _text.Clear();

            GameManager game = GameManager.Instance;
            _text.Append("Mode: ");
            if (game != null)
            {
                _text.Append(game.Mode);
                if (game.IsEvasion) _text.Append(" (Evasion)");
                _text.Append("   time ").Append(game.PlayTime.ToString("0.0")).Append(" s   deaths ").Append(game.Deaths);
            }
            else _text.Append("(no GameManager)");

            _text.Append("\nPlayer: ");
            if (_playerState != null) _text.Append(_playerState.Current);
            else _text.Append("(none)");

            CandleManager candle = CandleManager.Instance;
            _text.Append("\nCandle: ");
            if (candle != null)
            {
                _text.Append(candle.Current.ToString("0.0")).Append(" / ").Append(candle.Max.ToString("0"));
                if (candle.Flaring) _text.Append("  FLARING");
                if (candle.Paused) _text.Append("  paused");
            }
            else _text.Append("(none)");

            IReadOnlyList<GhostAI> ghosts = GhostAI.All;
            _text.Append("\nGhosts: ").Append(ghosts.Count);
            for (int i = 0; i < ghosts.Count; i++)
            {
                GhostAI ghost = ghosts[i];
                _text.Append("\n  ").Append(ghost.name).Append(": ").Append(ghost.State);
                if (ghost.IsEating) _text.Append(" (eating)");
                _text.Append("  ").Append(ghost.TimeInState.ToString("0.0")).Append(" s");
            }

            _text.Append("\nFPS: ").Append(Mathf.RoundToInt(_fps));
            _text.Append("\n[F1] hide");
            _shownText = _text.ToString();
        }

        private void OnGUI()
        {
            if (!_visible) return;

            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = false };
                _panelStyle.normal.textColor = Color.white;
                _panelStyle.padding = new RectOffset(8, 8, 6, 6);
                _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                _labelStyle.normal.textColor = Color.white;
            }
            int size = Mathf.Max(8, Mathf.RoundToInt(fontSize * Screen.height / 1080f));
            _panelStyle.fontSize = size;
            _labelStyle.fontSize = size;

            var content = new GUIContent(_shownText);
            Vector2 boxSize = _panelStyle.CalcSize(content);
            GUI.Box(new Rect(Screen.width - boxSize.x - 12f, 12f, boxSize.x, boxSize.y), content, _panelStyle);

            if (!labelGhosts) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            IReadOnlyList<GhostAI> ghosts = GhostAI.All;
            for (int i = 0; i < ghosts.Count; i++)
            {
                Vector3 screen = _camera.WorldToScreenPoint(ghosts[i].transform.position + Vector3.up * 2.3f);
                if (screen.z <= 0f) continue;
                GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 12f, 200f, 24f + size), ghosts[i].State.ToString(), _labelStyle);
            }
        }
#endif
    }
}

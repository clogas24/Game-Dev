using System;
using UnityEngine;
using UnityEngine.Events;

namespace LanternKeeper
{
    /// <summary>
    /// A plate that is pressed while the player and/or a ghost stands on it.
    /// Instead of relying on trigger enter/exit messages it polls a box above the plate about
    /// ten times a second, so an occupant that is disabled, destroyed or teleported away
    /// releases the plate without any exit event, and a ghost standing still to eat candy on
    /// the plate keeps it pressed.
    /// Setup: place on the ground, set who may press it, and size the detection box (yellow
    /// gizmo) to cover the pad. Hook Gate.plates to this plate, or use the UnityEvents.
    /// The player must be on a layer in Player Layers, ghosts on a layer in Ghost Layers.
    /// </summary>
    public class PressurePlate : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Who can press it")]
        [SerializeField, Tooltip("The player presses this plate.")]
        private bool acceptPlayer = true;
        [SerializeField, Tooltip("Layers the player's collider is on (layer 8, Player).")]
        private LayerMask playerLayers = 1 << 8;
        [SerializeField, Tooltip("Ghosts press this plate.")]
        private bool acceptGhosts = true;
        [SerializeField, Tooltip("Layers ghost colliders are on (layer 9, Ghost).")]
        private LayerMask ghostLayers = 1 << 9;
        [SerializeField, Tooltip("If true the plate stays pressed for good once pressed.")]
        private bool latch;

        [Header("Detection")]
        [SerializeField, Tooltip("Centre of the detection box, in the plate's local space.")]
        private Vector3 detectCenter = new Vector3(0f, 0.6f, 0f);
        [SerializeField, Tooltip("Size of the detection box, in the plate's local space.")]
        private Vector3 detectSize = new Vector3(2f, 1.2f, 2f);
        [SerializeField, Min(0.02f), Tooltip("Seconds between occupancy checks. 0.1 = ten times a second.")]
        private float pollInterval = 0.1f;
        [SerializeField, Min(0f), Tooltip("Seconds the plate stays pressed after the last occupant is seen. Smooths over a jump or a brief step off.")]
        private float releaseDelay = 0.2f;

        [Header("Visual")]
        [SerializeField, Tooltip("The part that sinks when pressed. Optional.")]
        private Transform pad;
        [SerializeField, Min(0f), Tooltip("How far the pad sinks (metres).")]
        private float pressDepth = 0.06f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the pad takes to sink or rise.")]
        private float pressSeconds = 0.12f;
        [SerializeField, Tooltip("Renderer tinted by state. Optional.")]
        private Renderer padRenderer;
        [SerializeField, Tooltip("Pad colour when not pressed.")]
        private Color idleColor = new Color(0.55f, 0.4f, 0.2f);
        [SerializeField, Tooltip("Pad colour when pressed.")]
        private Color pressedColor = new Color(0.4f, 1f, 0.5f);

        [Header("Events")]
        [SerializeField, Tooltip("Invoked when the plate becomes pressed.")]
        private UnityEvent onPressed = new UnityEvent();
        [SerializeField, Tooltip("Invoked when the plate is released.")]
        private UnityEvent onReleased = new UnityEvent();

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when pressed.")]
        private AudioClip pressClip;
        [SerializeField, Tooltip("Played when released.")]
        private AudioClip releaseClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for plate sounds.")]
        private float volume = 0.8f;

        private readonly Collider[] _hits = new Collider[4];
        private MaterialPropertyBlock _block;
        private Vector3 _padRest;
        private float _padT;
        private float _pollTimer;
        private float _emptyTime;
        private bool _pressed;

        public bool IsPressed => _pressed;
        public UnityEvent OnPressed => onPressed;
        public UnityEvent OnReleased => onReleased;
        /// <summary>Raised when the pressed state changes. Arguments: plate, pressed.</summary>
        public event Action<PressurePlate, bool> OnPressedChanged;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (pad != null) _padRest = pad.localPosition;
            _pollTimer = UnityEngine.Random.value * pollInterval;
            ApplyColor();
        }

        /// <summary>Releases a latched plate.</summary>
        public void ResetPlate()
        {
            _emptyTime = releaseDelay;
            SetPressed(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _pollTimer -= dt;
            if (_pollTimer <= 0f)
            {
                _pollTimer += pollInterval;
                if (IsOccupied())
                {
                    _emptyTime = 0f;
                    SetPressed(true);
                }
                else
                {
                    _emptyTime += pollInterval;
                    if (!latch && _emptyTime >= releaseDelay) SetPressed(false);
                }
            }

            if (pad == null) return;
            float target = _pressed ? 1f : 0f;
            if (Mathf.Approximately(_padT, target)) return;
            _padT = Mathf.MoveTowards(_padT, target, dt / pressSeconds);
            pad.localPosition = _padRest + Vector3.down * (pressDepth * Mathf.SmoothStep(0f, 1f, _padT));
        }

        private bool IsOccupied()
        {
            int mask = 0;
            if (acceptPlayer) mask |= playerLayers.value;
            if (acceptGhosts) mask |= ghostLayers.value;
            if (mask == 0) return false;

            Vector3 halfExtents = Vector3.Scale(detectSize, transform.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(detectCenter), halfExtents, _hits,
                transform.rotation, mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) _hits[i] = null;
            return count > 0;
        }

        private void SetPressed(bool pressed)
        {
            if (_pressed == pressed) return;
            _pressed = pressed;
            ApplyColor();
            Sfx.PlayAt(pressed ? pressClip : releaseClip, transform.position, volume);
            if (pressed) onPressed.Invoke();
            else onReleased.Invoke();
            OnPressedChanged?.Invoke(this, pressed);
        }

        private void ApplyColor()
        {
            if (padRenderer == null) return;
            padRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, _pressed ? pressedColor : idleColor);
            padRenderer.SetPropertyBlock(_block);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _pressed ? Color.green : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(detectCenter, detectSize);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LanternKeeper
{
    /// <summary>How a <see cref="Gate"/> door moves.</summary>
    public enum GateMotion
    {
        Slide = 0,
        Rotate = 1
    }

    /// <summary>
    /// A gate that opens while enough of its pressure plates are pressed. The door slides or
    /// swings between closed and open with a smoothstep ease.
    /// It never closes on someone: while anything on the Blocker Layers stands in the doorway
    /// box (red gizmo) the door stays open, and closes once the doorway is clear.
    /// Setup: assign Door (the moving child; give it a collider, a kinematic Rigidbody and a
    /// carving NavMeshObstacle so ghosts respect it), list the Plates, and set Required Plates
    /// (2 for the two-plate puzzle). Tick Stay Open Once Opened when the player has to leave a
    /// plate to walk through. Open / Close / ForceOpen are available for scripts and UnityEvents.
    /// </summary>
    public class Gate : MonoBehaviour
    {
        private static readonly List<Gate> s_all = new List<Gate>(4);

        [Header("Plates")]
        [SerializeField, Tooltip("Plates that can open this gate. Empty = script or event control only.")]
        private PressurePlate[] plates;
        [SerializeField, Min(1), Tooltip("How many of the plates must be pressed at once.")]
        private int requiredPlates = 1;
        [SerializeField, Tooltip("Once the plates have opened the gate it stays open for good.")]
        private bool stayOpenOnceOpened;
        [SerializeField, Tooltip("Start the scene open.")]
        private bool startOpen;

        [Header("Door motion")]
        [SerializeField, Tooltip("The moving part.")]
        private Transform door;
        [SerializeField, Tooltip("Slide = move by Slide Offset. Rotate = swing by Rotate Angle around Rotate Axis.")]
        private GateMotion motion = GateMotion.Slide;
        [SerializeField, Tooltip("Slide mode: door movement from closed to open, in the door's parent space. Sliding down into the ground cannot pin anyone.")]
        private Vector3 slideOffset = new Vector3(0f, -3.1f, 0f);
        [SerializeField, Tooltip("Rotate mode: local axis the door swings around.")]
        private Vector3 rotateAxis = Vector3.up;
        [SerializeField, Tooltip("Rotate mode: swing angle from closed to open (degrees).")]
        private float rotateAngle = 95f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to open fully.")]
        private float openSeconds = 1f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to close fully.")]
        private float closeSeconds = 1.4f;

        [Header("Safety (never close on someone)")]
        [SerializeField, Tooltip("Layers that keep the door from closing while inside the doorway box (Player and Ghost).")]
        private LayerMask blockerLayers = (1 << 8) | (1 << 9);
        [SerializeField, Tooltip("Centre of the doorway box, in the gate's local space.")]
        private Vector3 doorwayCenter = new Vector3(0f, 1.5f, 0f);
        [SerializeField, Tooltip("Size of the doorway box, in the gate's local space. Make it deeper than the door is thick.")]
        private Vector3 doorwaySize = new Vector3(3f, 3f, 1.6f);
        [SerializeField, Min(0.02f), Tooltip("Seconds between plate and doorway checks.")]
        private float checkInterval = 0.1f;

        [Header("Events")]
        [SerializeField, Tooltip("Invoked when the gate starts opening.")]
        private UnityEvent onOpened = new UnityEvent();
        [SerializeField, Tooltip("Invoked when the gate starts closing.")]
        private UnityEvent onClosed = new UnityEvent();

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when the gate starts opening.")]
        private AudioClip openClip;
        [SerializeField, Tooltip("Played when the gate starts closing.")]
        private AudioClip closeClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for gate sounds.")]
        private float volume = 0.9f;

        private readonly Collider[] _hits = new Collider[4];
        private Vector3 _closedPosition;
        private Quaternion _closedRotation;
        private float _t;
        private float _appliedT = -1f;
        private float _checkTimer;
        private bool _wantsOpen;
        private bool _doorwayBlocked;
        private bool _manualOpen;
        private bool _forced;
        private bool _latched;

        /// <summary>Every enabled gate. Used by GameManager (respawn reset) and the playtest logger.</summary>
        public static IReadOnlyList<Gate> All => s_all;

        /// <summary>The plates that can open this gate (may be empty).</summary>
        public IReadOnlyList<PressurePlate> Plates => plates ?? Array.Empty<PressurePlate>();
        /// <summary>True once a Stay Open Once Opened gate has been opened by its plates.</summary>
        public bool IsLatched => _latched;
        /// <summary>True while the gate is open or opening by plates, script or latch.</summary>
        public bool IsOpen => _wantsOpen;
        /// <summary>True once the door has finished opening.</summary>
        public bool IsFullyOpen => _t >= 1f;
        public int RequiredPlates => requiredPlates;
        /// <summary>How many of this gate's plates are pressed right now.</summary>
        public int PressedCount
        {
            get
            {
                int count = 0;
                if (plates == null) return 0;
                for (int i = 0; i < plates.Length; i++)
                {
                    if (plates[i] != null && plates[i].IsPressed) count++;
                }
                return count;
            }
        }
        /// <summary>Raised when the gate starts opening (true) or closing (false).</summary>
        public event Action<Gate, bool> OnOpenChanged;

        private void Awake()
        {
            if (door != null)
            {
                _closedPosition = door.localPosition;
                _closedRotation = door.localRotation;
            }
            if (startOpen)
            {
                _manualOpen = true;
                _wantsOpen = true;
                _t = 1f;
            }
            ApplyDoor();
        }

        private void OnEnable() => s_all.Add(this);

        private void OnDisable() => s_all.Remove(this);

        /// <summary>
        /// Player respawn: a gate that is only open because its plates are (or were just) held
        /// shuts at once, so the puzzle starts clean. A gate that latched open (Stay Open Once
        /// Opened), was forced open or was opened by script stays open: a solved gate stays
        /// solved. Call it after the plates have been reset. The door never snaps shut on
        /// someone standing in the doorway.
        /// </summary>
        public void ResetForRespawn()
        {
            _checkTimer = checkInterval;
            if (_forced || _manualOpen || _latched) return;

            bool platesSatisfied = plates != null && plates.Length > 0 && PressedCount >= requiredPlates;
            if (platesSatisfied)
            {
                if (stayOpenOnceOpened) _latched = true;
                SetWantsOpen(true);
                return;
            }

            SetWantsOpen(false);
            if (_t <= 0f) return;
            _doorwayBlocked = DoorwayOccupied();
            if (_doorwayBlocked) return;
            _t = 0f;
            ApplyDoor();
        }

        /// <summary>Opens the gate until Close is called.</summary>
        public void Open() => _manualOpen = true;

        /// <summary>Removes every script/latch override; the plates decide again.</summary>
        public void Close()
        {
            _manualOpen = false;
            _forced = false;
            _latched = false;
        }

        /// <summary>Opens the gate for good, whatever the plates do.</summary>
        public void ForceOpen() => _forced = true;

        private void Update()
        {
            float dt = Time.deltaTime;

            _checkTimer -= dt;
            if (_checkTimer <= 0f)
            {
                _checkTimer += checkInterval;

                bool platesSatisfied = plates != null && plates.Length > 0 && PressedCount >= requiredPlates;
                if (platesSatisfied && stayOpenOnceOpened) _latched = true;
                SetWantsOpen(_forced || _manualOpen || _latched || platesSatisfied);

                // Only matters while the door is not fully shut.
                _doorwayBlocked = !_wantsOpen && _t > 0f && DoorwayOccupied();
            }

            bool open = _wantsOpen || _doorwayBlocked;
            float target = open ? 1f : 0f;
            _t = Mathf.MoveTowards(_t, target, dt / (open ? openSeconds : closeSeconds));
            if (!Mathf.Approximately(_t, _appliedT)) ApplyDoor();
        }

        private void SetWantsOpen(bool open)
        {
            if (_wantsOpen == open) return;
            _wantsOpen = open;
            Sfx.PlayAt(open ? openClip : closeClip, transform.position, volume);
            if (open) onOpened.Invoke();
            else onClosed.Invoke();
            OnOpenChanged?.Invoke(this, open);
        }

        private bool DoorwayOccupied()
        {
            Vector3 halfExtents = Vector3.Scale(doorwaySize, transform.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(doorwayCenter), halfExtents, _hits,
                transform.rotation, blockerLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) _hits[i] = null;
            return count > 0;
        }

        private void ApplyDoor()
        {
            _appliedT = _t;
            if (door == null) return;
            float eased = Mathf.SmoothStep(0f, 1f, _t);
            if (motion == GateMotion.Slide)
            {
                door.localPosition = _closedPosition + slideOffset * eased;
            }
            else
            {
                door.localRotation = _closedRotation * Quaternion.AngleAxis(rotateAngle * eased, rotateAxis);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _doorwayBlocked ? Color.red : new Color(1f, 0.3f, 0.3f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(doorwayCenter, doorwaySize);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_all.Clear();
    }
}

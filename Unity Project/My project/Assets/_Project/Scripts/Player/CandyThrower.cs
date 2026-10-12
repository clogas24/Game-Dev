using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
    /// <summary>
    /// Throws candy: a limited count (starts at 0, max 5), a cooldown, and a lob along the
    /// camera's flat forward direction tilted upward. Cannot throw while the lantern is out.
    /// Setup: on the Player root next to PlayerInput. Wire Candy Prefab (the Candy prefab) and
    /// Throw Origin (the ThrowOrigin child). Input comes from the "Throw" action of the
    /// PlayerInput's actions asset. Fires the Animator "Throw" trigger only if it exists.
    /// </summary>
    [DisallowMultipleComponent]
    public class CandyThrower : MonoBehaviour
    {
        private static readonly int ThrowTrigger = Animator.StringToHash("Throw");

        [Header("References")]
        [SerializeField, Tooltip("Candy prefab to spawn (needs Rigidbody + Candy).")]
        private Candy candyPrefab;
        [SerializeField, Tooltip("Where the candy leaves the hand. Falls back to chest height if empty.")]
        private Transform throwOrigin;
        [SerializeField, Tooltip("Name of the throw action in the PlayerInput actions asset.")]
        private string throwActionName = "Throw";

        [Header("Inventory")]
        [SerializeField, Min(0), Tooltip("Candy held at scene start.")]
        private int startCount;
        [SerializeField, Min(1), Tooltip("Most candy the player can carry. Overview: 5.")]
        private int maxCount = 5;

        [Header("Throw")]
        [SerializeField, Min(0f), Tooltip("Seconds between throws.")]
        private float cooldown = 0.6f;
        [SerializeField, Min(0f), Tooltip("Launch speed (m/s). 9 at 35 degrees lands roughly 8 m away.")]
        private float throwSpeed = 9f;
        [SerializeField, Range(0f, 80f), Tooltip("Upward launch angle above the camera's flat forward (degrees).")]
        private float upwardAngle = 35f;
        [SerializeField, Min(0f), Tooltip("Seconds the player FSM reports Throwing after a throw.")]
        private float throwStateSeconds = 0.4f;

        [Header("Drop")]
        [SerializeField, Tooltip("Name of the drop action in the PlayerInput actions asset.")]
        private string dropActionName = "Drop";
        [SerializeField, Min(0f), Tooltip("How far in front of the player a dropped candy appears (metres).")]
        private float dropForward = 0.6f;
        [SerializeField, Min(0f), Tooltip("Height a dropped candy appears at (metres).")]
        private float dropHeight = 0.5f;
        [SerializeField, Min(0f), Tooltip("Downward speed of a dropped candy (m/s).")]
        private float dropSpeed = 1f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played on a successful throw.")]
        private AudioClip throwClip;
        [SerializeField, Tooltip("Played on a successful drop.")]
        private AudioClip dropClip;
        [SerializeField, Tooltip("Played when Throw is pressed with no candy.")]
        private AudioClip emptyClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for throw sounds.")]
        private float volume = 1f;

        private CandleManager _candle;
        private Animator _animator;
        private Collider _ownCollider;
        private Transform _camera;
        private InputAction _throwAction;
        private InputAction _dropAction;
        private bool _hasThrowTrigger;
        private int _count;
        private float _cooldownTimer;
        private float _throwStateTimer;

        public int Count => _count;
        public int MaxCount => maxCount;
        public bool IsFull => _count >= maxCount;
        /// <summary>True for a short window after each throw. Read by PlayerStateTracker.</summary>
        public bool IsThrowing => _throwStateTimer > 0f;
        /// <summary>Raised when the candy count changes. Arguments: count, max.</summary>
        public event Action<int, int> OnCountChanged;

        private void Awake()
        {
            _candle = GetComponent<CandleManager>();
            _animator = GetComponent<Animator>();
            _ownCollider = GetComponent<Collider>();
            _count = Mathf.Clamp(startCount, 0, maxCount);
        }

        private void Start()
        {
            _hasThrowTrigger = AnimatorParams.Has(_animator, ThrowTrigger, AnimatorControllerParameterType.Trigger);
            if (Camera.main != null) _camera = Camera.main.transform;

            if (TryGetComponent(out PlayerInput playerInput) && playerInput.actions != null)
            {
                _throwAction = playerInput.actions.FindAction(throwActionName);
                _dropAction = playerInput.actions.FindAction(dropActionName);
            }
            if (_throwAction == null)
            {
                Debug.LogWarning($"[CandyThrower] No '{throwActionName}' action found on PlayerInput; throwing is disabled.", this);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_cooldownTimer > 0f) _cooldownTimer -= dt;
            if (_throwStateTimer > 0f) _throwStateTimer -= dt;

            if (_throwAction != null && _throwAction.WasPressedThisFrame())
            {
                // Standing at a story note, the Throw key reads (or closes) it and no candy is thrown.
                if (!StoryNote.TryInteract()) TryThrow();
            }
            else if (_dropAction != null && _dropAction.WasPressedThisFrame()) TryDrop();
        }

        /// <summary>
        /// Drops one candy at the player's feet instead of lobbing it. Same count, cooldown and
        /// rules as a throw, and it raises the same events, but it does not play the throw
        /// animation or put the player FSM into Throwing.
        /// </summary>
        public bool TryDrop()
        {
            if (candyPrefab == null || _cooldownTimer > 0f) return false;
            if (_candle != null && !_candle.IsLit) return false;
            if (_count <= 0)
            {
                Sfx.PlayAt(emptyClip, transform.position, volume);
                return false;
            }

            Vector3 origin = transform.position + transform.forward * dropForward + Vector3.up * dropHeight;
            Candy candy = Instantiate(candyPrefab, origin, Quaternion.identity);
            candy.Launch(Vector3.down * dropSpeed, _ownCollider);

            _count--;
            _cooldownTimer = cooldown;
            Sfx.PlayAt(dropClip, origin, volume);
            OnCountChanged?.Invoke(_count, maxCount);
            GameEvents.RaiseCandyThrown(origin);
            return true;
        }

        /// <summary>Throws one candy if allowed. Returns true if a candy left the hand.</summary>
        public bool TryThrow()
        {
            if (candyPrefab == null || _cooldownTimer > 0f) return false;
            if (_candle != null && !_candle.IsLit) return false;
            if (_count <= 0)
            {
                Sfx.PlayAt(emptyClip, transform.position, volume);
                return false;
            }

            Vector3 forward = _camera != null ? _camera.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
            forward.Normalize();

            float radians = upwardAngle * Mathf.Deg2Rad;
            Vector3 direction = forward * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians);
            Vector3 origin = throwOrigin != null ? throwOrigin.position : transform.position + Vector3.up * 1.3f;

            Candy candy = Instantiate(candyPrefab, origin, Quaternion.identity);
            candy.Launch(direction * throwSpeed, _ownCollider);

            _count--;
            _cooldownTimer = cooldown;
            _throwStateTimer = throwStateSeconds;
            if (_hasThrowTrigger) _animator.SetTrigger(ThrowTrigger);
            Sfx.PlayAt(throwClip, origin, volume);
            OnCountChanged?.Invoke(_count, maxCount);
            GameEvents.RaiseCandyThrown(origin);
            return true;
        }

        /// <summary>Adds candy (pickups, candy bowls). Returns false and adds nothing if already full.</summary>
        public bool TryAdd(int amount)
        {
            if (amount <= 0 || _count >= maxCount) return false;
            _count = Mathf.Min(maxCount, _count + amount);
            OnCountChanged?.Invoke(_count, maxCount);
            return true;
        }
    }
}

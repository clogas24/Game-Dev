using System;
using StarterAssets;
using UnityEngine;

namespace LanternKeeper
{
    /// <summary>The player FSM states from the overview, lowest priority first.</summary>
    public enum PlayerState
    {
        Idle = 0,
        Walking = 1,
        Running = 2,
        Airborne = 3,
        Flaring = 4,
        Throwing = 5,
        KnockedBack = 6,
        Extinguished = 7
    }

    /// <summary>
    /// The explicit player FSM. It does not drive movement; it observes the other player
    /// components each frame, picks the highest-priority state that applies, and raises
    /// <see cref="OnStateChanged"/>. Step 3's debug overlay, telemetry and FSM docs read it.
    /// Setup: on the Player root. It finds CandleManager, LanternController, CandyThrower,
    /// PlayerKnockback, ThirdPersonController and CharacterController on the same object.
    /// Sets Animator bools "Flaring" / "Extinguished" and int "State" only if they exist.
    /// <code>
    /// Priority (highest wins, re-evaluated every frame):
    ///   Extinguished > KnockedBack > Throwing > Flaring > Airborne > Running / Walking > Idle
    ///
    ///                       candle == 0 (from ANY state)
    ///        +--------------------------------------------------------+
    ///        |                                                        v
    ///        |   ghost hit lands (any state but Extinguished)   +--------------+
    ///        |  +--------------------------------------+        | EXTINGUISHED |
    ///        |  |                                      v        +--------------+
    ///        |  |                              +-------------+         |
    ///        |  |                              | KNOCKED BACK|         | candle > 0
    ///        |  |                              +-------------+         | (respawn / SetTo)
    ///        |  |                                     | shove ends     |
    ///        |  |                                     v                v
    ///   +----------------------------------------------------------------------+
    ///   |                     LOCOMOTION (base states)                         |
    ///   |                                                                      |
    ///   |   +------+  speed > walk  +---------+  speed > run   +---------+     |
    ///   |   | IDLE | -------------> | WALKING | -------------> | RUNNING |     |
    ///   |   +------+ <------------- +---------+ <------------- +---------+     |
    ///   |       |      speed ~ 0         |       speed <= run       |          |
    ///   |       +------------------------+--------------------------+          |
    ///   |              not grounded  |       ^  grounded                       |
    ///   |                            v       |                                 |
    ///   |                         +------------+                               |
    ///   |                         |  AIRBORNE  |                               |
    ///   |                         +------------+                               |
    ///   +----------------------------------------------------------------------+
    ///        |  ^                                   |  ^
    ///        |  | Flare released, or                |  | throw window ends
    ///        |  | candle == 0                       |  | (throwStateSeconds)
    ///        v  |                                   v  |
    ///   +-----------+   candy thrown mid-flare   +----------+
    ///   |  FLARING  | -------------------------> | THROWING |
    ///   +-----------+ <------------------------- +----------+
    ///     Flare held,    window ends, Flare        candy thrown
    ///     candle >= min  still held                (count > 0, cooldown ready, lit)
    ///
    /// Flaring and Throwing overlay locomotion: the player keeps moving and jumping while in
    /// them, and drops back to whichever locomotion state applies when they end.
    /// </code>
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStateTracker : MonoBehaviour
    {
        private static readonly int FlaringBool = Animator.StringToHash("Flaring");
        private static readonly int ExtinguishedBool = Animator.StringToHash("Extinguished");
        private static readonly int StateInt = Animator.StringToHash("State");

        [Header("Locomotion thresholds")]
        [SerializeField, Min(0f), Tooltip("Horizontal speed (m/s) above which the player counts as Walking.")]
        private float walkSpeedThreshold = 0.2f;
        [SerializeField, Min(0f), Tooltip("Horizontal speed (m/s) above which the player counts as Running. Starter Assets walks at 2 and sprints at 5.3.")]
        private float runSpeedThreshold = 3.5f;

        [Header("Debug")]
        [SerializeField, Tooltip("Log every state change to the Console.")]
        private bool logStateChanges;

        private CandleManager _candle;
        private LanternController _lantern;
        private CandyThrower _thrower;
        private PlayerKnockback _knockback;
        private ThirdPersonController _mover;
        private CharacterController _controller;
        private Animator _animator;
        private bool _hasFlaringBool;
        private bool _hasExtinguishedBool;
        private bool _hasStateInt;

        public PlayerState Current { get; private set; } = PlayerState.Idle;
        /// <summary>Seconds spent in the current state.</summary>
        public float TimeInState { get; private set; }
        /// <summary>Raised on every transition. Arguments: previous state, next state.</summary>
        public event Action<PlayerState, PlayerState> OnStateChanged;

        private void Awake()
        {
            _candle = GetComponent<CandleManager>();
            _lantern = GetComponent<LanternController>();
            _thrower = GetComponent<CandyThrower>();
            _knockback = GetComponent<PlayerKnockback>();
            _mover = GetComponent<ThirdPersonController>();
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            _hasFlaringBool = AnimatorParams.Has(_animator, FlaringBool, AnimatorControllerParameterType.Bool);
            _hasExtinguishedBool = AnimatorParams.Has(_animator, ExtinguishedBool, AnimatorControllerParameterType.Bool);
            _hasStateInt = AnimatorParams.Has(_animator, StateInt, AnimatorControllerParameterType.Int);
        }

        private void LateUpdate()
        {
            PlayerState next = Evaluate();
            if (next == Current)
            {
                TimeInState += Time.deltaTime;
                return;
            }

            PlayerState previous = Current;
            Current = next;
            TimeInState = 0f;

            if (_hasFlaringBool) _animator.SetBool(FlaringBool, next == PlayerState.Flaring);
            if (_hasExtinguishedBool) _animator.SetBool(ExtinguishedBool, next == PlayerState.Extinguished);
            if (_hasStateInt) _animator.SetInteger(StateInt, (int)next);

            if (logStateChanges) Debug.Log($"[PlayerFSM] {previous} -> {next}", this);
            OnStateChanged?.Invoke(previous, next);
        }

        private PlayerState Evaluate()
        {
            if (_candle != null && !_candle.IsLit) return PlayerState.Extinguished;
            if (_knockback != null && _knockback.IsKnockedBack) return PlayerState.KnockedBack;
            if (_thrower != null && _thrower.IsThrowing) return PlayerState.Throwing;
            if (_lantern != null && _lantern.IsFlaring) return PlayerState.Flaring;

            bool grounded = _mover != null ? _mover.Grounded : _controller == null || _controller.isGrounded;
            if (!grounded) return PlayerState.Airborne;

            float speed = 0f;
            if (_controller != null)
            {
                Vector3 velocity = _controller.velocity;
                speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            }
            if (speed > runSpeedThreshold) return PlayerState.Running;
            if (speed > walkSpeedThreshold) return PlayerState.Walking;
            return PlayerState.Idle;
        }
    }
}

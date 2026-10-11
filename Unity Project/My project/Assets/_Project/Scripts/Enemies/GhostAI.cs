using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace LanternKeeper
{
    /// <summary>The ghost FSM states from the overview. The int values feed the Animator "State" parameter.</summary>
    public enum GhostState
    {
        Drift = 0,
        Notice = 1,
        Chase = 2,
        Repelled = 3,
        Distracted = 4,
        Return = 5
    }

    /// <summary>
    /// Ghost enemy: an explicit finite state machine on top of a NavMeshAgent.
    /// Setup: on the Ghost prefab root (layer Ghost) with a NavMeshAgent, a kinematic Rigidbody
    /// and a trigger collider (the collider is how the flare and pressure plates find the
    /// ghost). Assign Patrol Points in the scene; with none, the ghost idles at its start
    /// position. The scene needs a baked NavMesh. The player is found through
    /// CandleManager.Instance, so there is nothing to wire to the player.
    /// A ghost can only hurt the player in Chase, by getting within Touch Distance.
    /// <code>
    ///                     candle lit AND player within noticeRange
    ///                     (x flareNoticeMultiplier while flaring) AND line of sight
    ///   +---------+ ----------------------------------------------> +----------+
    ///   |  DRIFT  |                                                 |  NOTICE  |
    ///   | patrol  |                                                 | stop,face|
    ///   +---------+ <-------------------+                           +----------+
    ///        ^        reached closest   |          player left range / |    | noticeTime
    ///        |        patrol point      |          lantern out / LOS   |    | elapsed
    ///        |                          |                 lost         |    v
    ///   (ResetToPatrol)            +----------+ <----------------------+ +---------+
    ///                              |  RETURN  | <----------------------- |  CHASE  |
    ///                              | go home  |   touched player (hit),  | re-path |
    ///                              +----------+   beyond loseRange,      +---------+
    ///                                ^      ^     or lantern out
    ///          repelDuration elapsed |      | candy eaten, lost or unreachable
    ///                                |      |
    ///                        +----------+  +------------+
    ///                        | REPELLED |  | DISTRACTED |
    ///                        | flee     |  | go eat     |
    ///                        +----------+  +------------+
    ///                             ^               ^
    ///   Repel() from ANY state ---+               +--- landed candy within candySenseRange,
    ///   (releases any candy claim;                     from ANY state except Repelled
    ///    more Repel calls restart the timer)           (claims it; one ghost per candy)
    /// </code>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class GhostAI : MonoBehaviour, IRepellable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int StateParam = Animator.StringToHash("State");
        private static readonly List<GhostAI> s_all = new List<GhostAI>(8);
        private static int s_alertCount;

        [Header("References")]
        [SerializeField, Tooltip("Waypoints visited in order during Drift. Empty = idle at the start position.")]
        private Transform[] patrolPoints;
        [SerializeField, Tooltip("Renderers tinted by state. Empty = all renderers in children.")]
        private Renderer[] tintRenderers;
        [SerializeField, Tooltip("Optional Animator. 'Speed' (float) and 'State' (int) are set only if they exist.")]
        private Animator animator;
        [SerializeField, Tooltip("Optional Light whose colour follows the state tint, so the ghost's state reads in the dark.")]
        private Light glowLight;

        [Header("Drift (patrol)")]
        [SerializeField, Min(0f), Tooltip("Move speed while patrolling (m/s).")]
        private float driftSpeed = 1.6f;
        [SerializeField, Min(0f), Tooltip("Seconds to wait at each waypoint.")]
        private float waypointWait = 1.5f;
        [SerializeField, Min(0.05f), Tooltip("How close counts as having arrived at a destination (metres, on top of the agent's stopping distance).")]
        private float arriveTolerance = 0.35f;

        [Header("Notice")]
        [SerializeField, Min(0f), Tooltip("Distance at which the ghost notices a lit player (metres).")]
        private float noticeRange = 7f;
        [SerializeField, Min(1f), Tooltip("Notice range multiplier while the player is flaring. Overview: twice as far.")]
        private float flareNoticeMultiplier = 2f;
        [SerializeField, Min(0f), Tooltip("Wind-up before the chase starts (seconds). Overview: about 0.9.")]
        private float noticeTime = 0.9f;
        [SerializeField, Tooltip("Optional. Layers that block the ghost's view of the player. Nothing = ghosts see through everything.")]
        private LayerMask lineOfSightBlockers;
        [SerializeField, Min(0f), Tooltip("Height of the ghost's eyes and of the point it looks at on the player (metres).")]
        private float eyeHeight = 1.2f;
        [SerializeField, Min(0f), Tooltip("How fast the ghost turns to face the player while noticing (degrees per second).")]
        private float faceTurnSpeed = 360f;

        [Header("Chase")]
        [SerializeField, Min(0f), Tooltip("Move speed while chasing (m/s). The player walks at 2 and sprints at 5.3.")]
        private float chaseSpeed = 3.6f;
        [SerializeField, Min(0.05f), Tooltip("Seconds between path updates while chasing. 0.25 = four times a second.")]
        private float repathInterval = 0.25f;
        [SerializeField, Min(0f), Tooltip("The chase is abandoned beyond this distance (metres). Keep it above noticeRange x flareNoticeMultiplier.")]
        private float loseRange = 16f;
        [SerializeField, Min(0f), Tooltip("The player is hit when a chasing ghost gets this close (metres, centre to centre).")]
        private float touchDistance = 1.1f;

        [Header("Repelled")]
        [SerializeField, Min(0f), Tooltip("Seconds the ghost stays repelled after the LAST Repel call. The flare calls Repel about 5 times a second.")]
        private float repelDuration = 2f;
        [SerializeField, Min(0f), Tooltip("Move speed while fleeing (m/s).")]
        private float fleeSpeed = 4.5f;
        [SerializeField, Min(0f), Tooltip("How far from itself the ghost aims when fleeing (metres).")]
        private float fleeDistance = 6f;
        [SerializeField, Min(0.1f), Tooltip("Search radius used to snap the flee point onto the NavMesh (metres).")]
        private float fleeSampleRadius = 3f;

        [Header("Distracted (candy)")]
        [SerializeField, Min(0f), Tooltip("Distance at which the ghost senses landed candy (metres).")]
        private float candySenseRange = 9f;
        [SerializeField, Min(0.05f), Tooltip("Seconds between candy searches.")]
        private float candySearchInterval = 0.3f;
        [SerializeField, Min(0f), Tooltip("Move speed toward candy (m/s).")]
        private float distractedSpeed = 2.8f;
        [SerializeField, Min(0.1f), Tooltip("The ghost starts eating within this distance of the candy (metres). Small, so a ghost eating candy on a plate stands on the plate.")]
        private float eatDistance = 0.5f;
        [SerializeField, Min(0f), Tooltip("Seconds spent eating. Overview: about 4.")]
        private float eatDuration = 4f;

        [Header("Return")]
        [SerializeField, Min(0f), Tooltip("Move speed while returning to the patrol (m/s).")]
        private float returnSpeed = 2.2f;

        [Header("Crowding")]
        [SerializeField, Tooltip("Give each ghost a random avoidance priority at start, so two ghosts heading for the same spot don't deadlock.")]
        private bool randomizeAvoidancePriority = true;

        [Header("State colours")]
        [SerializeField, Tooltip("Drift and Return (calm).")]
        private Color calmColor = new Color(0.35f, 1f, 0.45f);
        [SerializeField, Tooltip("Notice (alert).")]
        private Color noticeColor = new Color(1f, 0.9f, 0.2f);
        [SerializeField, Tooltip("Chase.")]
        private Color chaseColor = new Color(1f, 0.2f, 0.15f);
        [SerializeField, Tooltip("Repelled.")]
        private Color repelledColor = Color.white;
        [SerializeField, Tooltip("Distracted (walking to or eating candy).")]
        private Color distractedColor = new Color(1f, 0.45f, 0.85f);
        [SerializeField, Min(0.1f), Tooltip("How fast the tint blends to the new state's colour. Higher = snappier.")]
        private float tintSharpness = 10f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played on entering Notice.")]
        private AudioClip noticeClip;
        [SerializeField, Tooltip("Played on entering Chase.")]
        private AudioClip chaseClip;
        [SerializeField, Tooltip("Played on entering Repelled.")]
        private AudioClip repelledClip;
        [SerializeField, Tooltip("Played when the ghost starts eating candy.")]
        private AudioClip eatClip;
        [SerializeField, Tooltip("Played when the ghost hits the player.")]
        private AudioClip hitClip;
        [SerializeField, Tooltip("Played on entering Return.")]
        private AudioClip returnClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for ghost sounds.")]
        private float volume = 0.8f;

        private NavMeshAgent _agent;
        private MaterialPropertyBlock _block;
        private float[] _baseAlphas;
        private bool _hasSpeedParam;
        private bool _hasStateParam;

        private Transform _player;
        private CandleManager _candle;
        private LanternController _lantern;
        private PlayerKnockback _knockback;

        private GhostState _state = GhostState.Drift;
        private float _stateTime;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private int _patrolIndex;
        private bool _waiting;
        private float _waitTimer;
        private float _repathTimer;
        private float _repelTimer;
        private float _fleePathTime;
        private float _candyTimer;
        private Candy _candy;
        private Candy _unreachableCandy;
        private bool _eating;
        private float _eatTimer;
        private Color _tint;
        private Color _appliedTint;
        private bool _started;

        /// <summary>Every enabled ghost. Used by GameManager (respawn reset) and DebugOverlay.</summary>
        public static IReadOnlyList<GhostAI> All => s_all;
        /// <summary>True while at least one ghost is in Notice or Chase (the overview's "Evasion" mode).</summary>
        public static bool AnyAlert => s_alertCount > 0;
        /// <summary>Raised when any ghost changes state. Arguments: ghost, previous state, next state.</summary>
        public static event Action<GhostAI, GhostState, GhostState> AnyStateChanged;

        /// <summary>Raised when this ghost changes state. Arguments: ghost, previous state, next state.</summary>
        public event Action<GhostAI, GhostState, GhostState> OnStateChanged;

        public GhostState State => _state;
        /// <summary>Seconds spent in the current state.</summary>
        public float TimeInState => _stateTime;
        /// <summary>True while standing at a candy and eating it.</summary>
        public bool IsEating => _state == GhostState.Distracted && _eating;
        /// <summary>Notice range right now, including the flare multiplier.</summary>
        public float CurrentNoticeRange => noticeRange * (_lantern != null && _lantern.IsFlaring ? flareNoticeMultiplier : 1f);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (tintRenderers == null || tintRenderers.Length == 0) tintRenderers = GetComponentsInChildren<Renderer>();

            _block = new MaterialPropertyBlock();
            _baseAlphas = new float[tintRenderers.Length];
            for (int i = 0; i < tintRenderers.Length; i++)
            {
                Material shared = tintRenderers[i] != null ? tintRenderers[i].sharedMaterial : null;
                _baseAlphas[i] = shared != null && shared.HasProperty(BaseColorId) ? shared.GetColor(BaseColorId).a : 1f;
            }

            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            if (randomizeAvoidancePriority) _agent.avoidancePriority = UnityEngine.Random.Range(30, 70);
            _tint = calmColor;
            ApplyTint();
        }

        private void OnEnable()
        {
            s_all.Add(this);
            if (_started) EnterState(GhostState.Drift); // re-enabled after being switched off
        }

        private void OnDisable()
        {
            s_all.Remove(this);
            ReleaseCandy();
            if (IsAlert(_state)) s_alertCount--;
            _state = GhostState.Drift;
        }

        private void Start()
        {
            _hasSpeedParam = AnimatorParams.Has(animator, SpeedParam, AnimatorControllerParameterType.Float);
            _hasStateParam = AnimatorParams.Has(animator, StateParam, AnimatorControllerParameterType.Int);
            FindPlayer();
            _started = true;
            _candyTimer = UnityEngine.Random.value * candySearchInterval; // stagger ghosts across frames
            EnterState(GhostState.Drift);
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// IRepellable: flee from <paramref name="sourcePosition"/>. Works from any state, drops any
        /// candy claim, and each call restarts the repelled timer.
        /// </summary>
        public void Repel(Vector3 sourcePosition)
        {
            if (!isActiveAndEnabled) return;
            _repelTimer = repelDuration;
            if (_state != GhostState.Repelled) ChangeState(GhostState.Repelled);

            // The flare calls this several times a second; re-aiming the flee path that often is wasted work.
            if (Time.time - _fleePathTime < 0.4f) return;
            _fleePathTime = Time.time;

            Vector3 away = transform.position - sourcePosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            Vector3 target = transform.position + away.normalized * fleeDistance;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, fleeSampleRadius, NavMesh.AllAreas)) target = hit.position;
            MoveTo(target);
        }

        /// <summary>Puts the ghost back at its start position in Drift (player respawn).</summary>
        public void ResetToPatrol()
        {
            ReleaseCandy();
            _unreachableCandy = null;
            if (_agent.isOnNavMesh) _agent.Warp(_homePosition);
            else transform.position = _homePosition;
            transform.rotation = _homeRotation;
            _patrolIndex = 0;
            if (_player == null) FindPlayer();

            if (_state == GhostState.Drift) EnterState(GhostState.Drift);
            else ChangeState(GhostState.Drift);
        }

        // ------------------------------------------------------------------ FSM

        private void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;

            // Any state except Repelled -> Distracted.
            if (_state != GhostState.Repelled && _state != GhostState.Distracted)
            {
                _candyTimer -= dt;
                if (_candyTimer <= 0f)
                {
                    _candyTimer = candySearchInterval;
                    if (TryClaimCandy()) ChangeState(GhostState.Distracted);
                }
            }

            switch (_state)
            {
                case GhostState.Drift: TickDrift(dt); break;
                case GhostState.Notice: TickNotice(dt); break;
                case GhostState.Chase: TickChase(dt); break;
                case GhostState.Repelled: TickRepelled(dt); break;
                case GhostState.Distracted: TickDistracted(dt); break;
                case GhostState.Return: TickReturn(); break;
            }

            UpdateTint(dt);
            if (_hasSpeedParam) animator.SetFloat(SpeedParam, _agent.velocity.magnitude);
        }

        private void TickDrift(float dt)
        {
            if (CanNoticePlayer())
            {
                ChangeState(GhostState.Notice);
                return;
            }
            if (patrolPoints == null || patrolPoints.Length == 0) return;

            if (_waiting)
            {
                _waitTimer -= dt;
                if (_waitTimer > 0f) return;
                _waiting = false;
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                MoveToPatrolPoint();
            }
            else if (HasArrived())
            {
                _waiting = true;
                _waitTimer = waypointWait;
            }
        }

        private void TickNotice(float dt)
        {
            if (!CanNoticePlayer())
            {
                ChangeState(GhostState.Return);
                return;
            }

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(toPlayer), faceTurnSpeed * dt);
            }

            if (_stateTime >= noticeTime) ChangeState(GhostState.Chase);
        }

        private void TickChase(float dt)
        {
            if (_player == null || _candle == null || !_candle.IsLit)
            {
                ChangeState(GhostState.Return);
                return;
            }

            float sqrDistance = (_player.position - transform.position).sqrMagnitude;
            if (sqrDistance > loseRange * loseRange)
            {
                ChangeState(GhostState.Return);
                return;
            }

            if (sqrDistance <= touchDistance * touchDistance)
            {
                // PlayerKnockback.Apply raises GameEvents.PlayerHit itself and returns false during invulnerability.
                if (_knockback != null && _knockback.Apply(transform.position))
                {
                    _candle.DamageFromGhost();
                    Sfx.PlayAt(hitClip, transform.position, volume);
                }
                ChangeState(GhostState.Return);
                return;
            }

            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                MoveTo(_player.position);
            }
        }

        private void TickRepelled(float dt)
        {
            _repelTimer -= dt;
            if (_repelTimer <= 0f) ChangeState(GhostState.Return);
        }

        private void TickDistracted(float dt)
        {
            // The candy was destroyed (despawned, respawn clean-up) or its claim was taken away.
            if (_candy == null || !ReferenceEquals(_candy.Claimant, this))
            {
                _candy = null;
                if (TryClaimCandy()) EnterState(GhostState.Distracted);
                else ChangeState(GhostState.Return);
                return;
            }

            if (_eating)
            {
                _eatTimer += dt;
                if (_eatTimer < eatDuration) return;
                _candy.Consume();
                _candy = null;
                ChangeState(GhostState.Return);
                return;
            }

            Vector3 toCandy = _candy.transform.position - transform.position;
            toCandy.y = 0f;
            if (toCandy.sqrMagnitude <= eatDistance * eatDistance)
            {
                _eating = true;
                _eatTimer = 0f;
                Stop();
                Sfx.PlayAt(eatClip, transform.position, volume);
                return;
            }

            // The path ended and the candy is still out of reach (in a pit, across a gap): give it up.
            if (_stateTime > 0.5f && HasArrived())
            {
                _unreachableCandy = _candy;
                ReleaseCandy();
                if (TryClaimCandy()) EnterState(GhostState.Distracted);
                else ChangeState(GhostState.Return);
                return;
            }

            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                _repathTimer = 0.5f;
                MoveTo(_candy.transform.position);
            }
        }

        private void TickReturn()
        {
            if (HasArrived()) ChangeState(GhostState.Drift);
        }

        private void ChangeState(GhostState next)
        {
            GhostState previous = _state;
            if (previous == next) return;

            if (previous == GhostState.Distracted) ReleaseCandy();
            if (IsAlert(previous)) s_alertCount--;
            if (IsAlert(next)) s_alertCount++;

            _state = next;
            EnterState(next);

            OnStateChanged?.Invoke(this, previous, next);
            AnyStateChanged?.Invoke(this, previous, next);
        }

        // Also called directly to restart a state (new candy while already Distracted, patrol reset).
        private void EnterState(GhostState state)
        {
            _stateTime = 0f;
            _repathTimer = 0f;
            _eating = false;
            _eatTimer = 0f;
            _waiting = false;
            if (_hasStateParam) animator.SetInteger(StateParam, (int)state);

            switch (state)
            {
                case GhostState.Drift:
                    _agent.speed = driftSpeed;
                    if (patrolPoints != null && patrolPoints.Length > 0) MoveToPatrolPoint();
                    else Stop();
                    break;

                case GhostState.Notice:
                    Stop();
                    Sfx.PlayAt(noticeClip, transform.position, volume);
                    break;

                case GhostState.Chase:
                    _agent.speed = chaseSpeed;
                    Sfx.PlayAt(chaseClip, transform.position, volume);
                    break;

                case GhostState.Repelled:
                    _agent.speed = fleeSpeed;
                    _fleePathTime = -10f;
                    Sfx.PlayAt(repelledClip, transform.position, volume);
                    break;

                case GhostState.Distracted:
                    _agent.speed = distractedSpeed;
                    if (_candy != null) MoveTo(_candy.transform.position);
                    _repathTimer = 0.5f;
                    break;

                case GhostState.Return:
                    _agent.speed = returnSpeed;
                    _patrolIndex = ClosestPatrolIndex();
                    if (patrolPoints != null && patrolPoints.Length > 0) MoveToPatrolPoint();
                    else MoveTo(_homePosition);
                    Sfx.PlayAt(returnClip, transform.position, volume);
                    break;
            }
        }

        private static bool IsAlert(GhostState state) => state == GhostState.Notice || state == GhostState.Chase;

        // ------------------------------------------------------------------ senses

        private bool CanNoticePlayer()
        {
            if (_player == null || _candle == null || !_candle.IsLit) return false;

            float range = CurrentNoticeRange;
            if ((_player.position - transform.position).sqrMagnitude > range * range) return false;

            if (lineOfSightBlockers.value != 0)
            {
                Vector3 eye = transform.position + Vector3.up * eyeHeight;
                Vector3 target = _player.position + Vector3.up * eyeHeight;
                if (Physics.Linecast(eye, target, lineOfSightBlockers, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }

        private bool TryClaimCandy()
        {
            Candy found = Candy.FindClosestAvailable(transform.position, candySenseRange);
            if (found == null || found == _unreachableCandy) return false;
            if (!found.Claim(this)) return false;
            _candy = found;
            return true;
        }

        private void ReleaseCandy()
        {
            if (_candy != null) _candy.ReleaseClaim(this);
            _candy = null;
            _eating = false;
        }

        private void FindPlayer()
        {
            _candle = CandleManager.Instance;
            if (_candle == null) return;
            _player = _candle.transform;
            _lantern = _candle.GetComponent<LanternController>();
            _knockback = _candle.GetComponent<PlayerKnockback>();
        }

        // ------------------------------------------------------------------ movement

        private void MoveTo(Vector3 destination)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        private void MoveToPatrolPoint()
        {
            Transform point = patrolPoints[_patrolIndex];
            MoveTo(point != null ? point.position : _homePosition);
        }

        private void Stop()
        {
            if (!_agent.isOnNavMesh) return;
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        private bool HasArrived()
        {
            if (!_agent.isOnNavMesh) return true;
            return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + arriveTolerance;
        }

        private int ClosestPatrolIndex()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return 0;
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                float sqr = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        // ------------------------------------------------------------------ visuals

        private Color ColorFor(GhostState state)
        {
            switch (state)
            {
                case GhostState.Notice: return noticeColor;
                case GhostState.Chase: return chaseColor;
                case GhostState.Repelled: return repelledColor;
                case GhostState.Distracted: return distractedColor;
                default: return calmColor;
            }
        }

        private void UpdateTint(float dt)
        {
            _tint = Color.Lerp(_tint, ColorFor(_state), 1f - Mathf.Exp(-tintSharpness * dt));
            Color delta = _tint - _appliedTint;
            if (Mathf.Abs(delta.r) + Mathf.Abs(delta.g) + Mathf.Abs(delta.b) > 0.004f) ApplyTint();
        }

        private void ApplyTint()
        {
            _appliedTint = _tint;
            if (glowLight != null) glowLight.color = _tint;
            for (int i = 0; i < tintRenderers.Length; i++)
            {
                Renderer r = tintRenderers[i];
                if (r == null) continue;
                Color c = _tint;
                c.a = _baseAlphas[i];
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_block);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 p = transform.position;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(p, noticeRange);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(p, noticeRange * flareNoticeMultiplier);
            Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.6f);
            Gizmos.DrawWireSphere(p, loseRange);
            Gizmos.color = new Color(1f, 0.45f, 0.85f, 0.8f);
            Gizmos.DrawWireSphere(p, candySenseRange);

            if (patrolPoints == null || patrolPoints.Length == 0) return;
            Gizmos.color = new Color(0.35f, 1f, 0.45f, 0.9f);
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Transform a = patrolPoints[i];
                Transform b = patrolPoints[(i + 1) % patrolPoints.Length];
                if (a == null) continue;
                Gizmos.DrawSphere(a.position, 0.15f);
                if (b != null) Gizmos.DrawLine(a.position, b.position);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
            s_alertCount = 0;
            AnyStateChanged = null;
        }
    }
}

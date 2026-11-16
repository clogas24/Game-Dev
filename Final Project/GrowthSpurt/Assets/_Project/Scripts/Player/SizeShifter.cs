using System;
using StarterAssets;
using UnityEngine;

namespace GrowthSpurt
{
    /// <summary>
    /// The size-shift core. Steps the player between Small, Normal and Big and pushes the active
    /// <see cref="SizeProfile"/> into the Starter Assets ThirdPersonController, the CharacterController
    /// and the Animator. Starter Assets scripts are never edited: everything goes through their public fields.
    ///
    /// How the capsule scales (measured headless in Unity 6000.6.3f1):
    /// - The body is scaled with transform.localScale (pivot = feet).
    /// - CharacterController height, radius and center are LOCAL values that Unity multiplies by the
    ///   transform scale, so they stay at the scale-1 values.
    /// - skinWidth and stepOffset are WORLD values that ignore the scale, so they are set in metres.
    /// - PhysX rests the capsule one skin width above the ground, so center.y puts the capsule bottom one
    ///   (world) skin width above the feet: the feet then touch the ground at every size.
    /// - After a scale change, PhysX keeps the OLD capsule centre: the next Move() teleports the body by
    ///   the difference (1.1 m into the floor going Normal to Big). Physics.SyncTransforms() after
    ///   setting scale + center fixes it; the feet then stay at exactly the same height.
    /// </summary>
    [DefaultExecutionOrder(-50)] // before ThirdPersonController.Update, so new values apply the same frame
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(ThirdPersonController))]
    public class SizeShifter : MonoBehaviour
    {
        [Header("Profiles")]
        [SerializeField] private SizeProfile smallProfile;
        [SerializeField] private SizeProfile normalProfile;
        [SerializeField] private SizeProfile bigProfile;
        [SerializeField] private PlayerSize startingSize = PlayerSize.Normal;

        [Header("Transition")]
        [Tooltip("Seconds for the eased scale change. Grow/Shrink input is ignored meanwhile.")]
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.25f;
        [Tooltip("Seconds after a transition before the next Grow/Shrink is accepted.")]
        [SerializeField, Min(0f)] private float cooldown = 0.15f;

        [Header("Space check (before growing)")]
        [Tooltip("Layers that can block a grow. The player's own layer is always excluded.")]
        [SerializeField] private LayerMask obstacleLayers = ~0;
        [Tooltip("Shrinks the check capsule slightly so touching (not overlapping) geometry doesn't block.")]
        [SerializeField, Min(0f)] private float checkInset = 0.01f;
        [Tooltip("If growing next to a wall would overlap it, step away from the wall while growing (if there is room).")]
        [SerializeField] private bool nudgeAwayFromWalls = true;

        [Header("Mid-air grow to Big (slam)")]
        [Tooltip("Extra gravity multiplier on top of Big's, from a mid-air grow until landing.")]
        [SerializeField, Min(1f)] private float slamGravityBoost = 1.5f;

        [Header("Landing")]
        [Tooltip("Shorter airborne spells (stair edges, tiny bumps) don't fire OnLanded.")]
        [SerializeField, Min(0f)] private float minAirTimeForLanding = 0.12f;

        [Header("Pushing")]
        [SerializeField] private LayerMask pushLayers = ~0;
        [Tooltip("Speed factor (of move speed) when pushing a body as heavy as the push strength allows. Lighter bodies move faster.")]
        [SerializeField, Range(0.05f, 1f)] private float heaviestPushSpeedFactor = 0.35f;

        // ---------- public state ----------
        public PlayerSize CurrentSize { get; private set; }
        public SizeProfile CurrentProfile => GetProfile(CurrentSize);
        public int Weight => CurrentProfile != null ? CurrentProfile.weight : 0;
        public bool IsTransitioning { get; private set; }
        /// <summary>True from a mid-air grow to Big until the next landing (still true inside OnLanded).</summary>
        public bool IsSlamming { get; private set; }
        public bool CanShift => isActiveAndEnabled && !IsTransitioning && Time.time >= _cooldownUntil;
        public bool IsGrounded => _tpc != null && _tpc.Grounded;
        /// <summary>Current uniform scale, including mid-transition values.</summary>
        public float CurrentScale => _scale;
        public Vector3 Velocity => _cc != null ? _cc.velocity : Vector3.zero;
        public float LastLandingSpeed { get; private set; }
        public bool LastLandingWasSlam { get; private set; }

        // ---------- events ----------
        /// <summary>(from, to). Fires when a transition starts; CurrentSize/Weight already report the new size.</summary>
        public event Action<PlayerSize, PlayerSize> OnSizeChanged;
        /// <summary>Fires when a transition has fully finished scaling.</summary>
        public event Action<PlayerSize> OnTransitionFinished;
        /// <summary>A grow was refused because the target size doesn't fit. Argument = the size that didn't fit.</summary>
        public event Action<PlayerSize> OnGrowBlocked;
        /// <summary>(size at landing, downward speed in m/s just before touching down).</summary>
        public event Action<PlayerSize, float> OnLanded;

        // ---------- components ----------
        private CharacterController _cc;
        private ThirdPersonController _tpc;
        private StarterAssetsInputs _inputs;
        private Animator _animator;

        // scale-1 rig values, read from the prefab once
        private float _baseHeight, _baseRadius, _baseSkin;
        private Vector3 _baseCenter;
        private float _baseGravity, _baseGroundedOffset, _baseGroundedRadius;
        private bool _initialised;

        // live blended values
        private float _scale = 1f;
        private float _gravityMultiplier = 1f;

        // transition
        private SizeProfile _from, _to;
        private float _t, _lastEased;
        private Vector3 _nudge;
        private float _cooldownUntil;

        // landing
        private bool _wasGrounded = true;
        private float _airTime;
        private float _lastAirVerticalSpeed;

        private static readonly Vector3[] NudgeDirections = BuildDirections(16);

        // =====================================================================

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _tpc = GetComponent<ThirdPersonController>();
            _inputs = GetComponent<StarterAssetsInputs>();
            _animator = GetComponent<Animator>();

            if (smallProfile == null || normalProfile == null || bigProfile == null)
            {
                Debug.LogError($"{nameof(SizeShifter)} on {name} is missing a SizeProfile. " +
                               "Run Tools > Growth Spurt > Build GS_Player Prefab.", this);
                enabled = false;
                return;
            }

            CaptureBaseRig();
            CurrentSize = startingSize;
            ApplyBlend(GetProfile(startingSize), GetProfile(startingSize), 1f);
            ApplyGravity();
            _wasGrounded = true;
        }

        private void CaptureBaseRig()
        {
            if (_initialised) return;
            // The prefab is authored at scale 1; divide out any scale someone left on the instance.
            float s = Mathf.Max(0.01f, transform.localScale.y);
            _baseHeight = _cc.height;
            _baseRadius = _cc.radius;
            _baseCenter = _cc.center;
            _baseSkin = _cc.skinWidth / s;
            _baseGravity = _tpc.Gravity;
            _baseGroundedOffset = _tpc.GroundedOffset / s;
            _baseGroundedRadius = _tpc.GroundedRadius / s;
            _initialised = true;
        }

        private void Update()
        {
            if (IsTransitioning)
            {
                _t += Time.deltaTime / transitionDuration;
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t));

                // Step away from walls first, then grow into the space that was checked.
                if (_nudge != Vector3.zero)
                    _cc.Move(_nudge * (eased - _lastEased));
                _lastEased = eased;

                ApplyBlend(_from, _to, eased);

                if (_t >= 1f)
                {
                    IsTransitioning = false;
                    _nudge = Vector3.zero;
                    _cooldownUntil = Time.time + cooldown;
                    OnTransitionFinished?.Invoke(CurrentSize);
                }
            }

            ApplyGravity();
        }

        private void LateUpdate()
        {
            // ThirdPersonController has run its grounded check and Move by now.
            bool grounded = _tpc.Grounded;
            if (!grounded)
            {
                _airTime += Time.deltaTime;
                _lastAirVerticalSpeed = _cc.velocity.y;
            }
            else if (!_wasGrounded)
            {
                if (_airTime >= minAirTimeForLanding)
                {
                    LastLandingSpeed = Mathf.Max(0f, -_lastAirVerticalSpeed);
                    LastLandingWasSlam = IsSlamming;
                    OnLanded?.Invoke(CurrentSize, LastLandingSpeed);
                }
                IsSlamming = false;
            }

            if (grounded) _airTime = 0f;
            _wasGrounded = grounded;
        }

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public bool TryGrow() => TryStep(+1);
        public bool TryShrink() => TryStep(-1);

        public SizeProfile GetProfile(PlayerSize size)
        {
            switch (size)
            {
                case PlayerSize.Small: return smallProfile;
                case PlayerSize.Big: return bigProfile;
                default: return normalProfile;
            }
        }

        /// <summary>Would the player fit at <paramref name="feet"/> at the given size? (Same test as the grow check.)</summary>
        public bool CanFitAt(Vector3 feet, PlayerSize size)
        {
            var p = GetProfile(size);
            return p != null && !Overlaps(feet, p.scale);
        }

        /// <summary>Instantly set a size with no transition and no space check (respawns, checkpoints, cutscenes).</summary>
        public void SetSizeImmediate(PlayerSize size)
        {
            if (!_initialised) return;
            var from = CurrentSize;
            IsTransitioning = false;
            IsSlamming = false;
            _nudge = Vector3.zero;
            CurrentSize = size;
            ApplyBlend(GetProfile(size), GetProfile(size), 1f);
            ApplyGravity();
            if (from != size) OnSizeChanged?.Invoke(from, size);
        }

        // ---------------------------------------------------------------------
        // Transition
        // ---------------------------------------------------------------------

        private bool TryStep(int direction)
        {
            if (!CanShift) return false;
            int target = (int)CurrentSize + direction;
            if (target < (int)PlayerSize.Small || target > (int)PlayerSize.Big) return false;
            return BeginTransition((PlayerSize)target);
        }

        private bool BeginTransition(PlayerSize to)
        {
            PlayerSize from = CurrentSize;
            SizeProfile fromProfile = GetProfile(from);
            SizeProfile toProfile = GetProfile(to);
            bool growing = toProfile.scale > _scale + 0.0001f;

            Vector3 nudge = Vector3.zero;
            if (growing && !FindRoomToGrow(toProfile.scale, out nudge))
            {
                OnGrowBlocked?.Invoke(to);
                return false;
            }

            _from = fromProfile;
            _to = toProfile;
            _t = 0f;
            _lastEased = 0f;
            _nudge = nudge;
            IsTransitioning = true;
            CurrentSize = to;

            if (growing && to == PlayerSize.Big && !_tpc.Grounded) IsSlamming = true;
            else if (!growing) IsSlamming = false;

            OnSizeChanged?.Invoke(from, to);
            return true;
        }

        private void ApplyBlend(SizeProfile a, SizeProfile b, float k)
        {
            SetScale(Mathf.Lerp(a.scale, b.scale, k));

            _tpc.MoveSpeed = Mathf.Lerp(a.moveSpeed, b.moveSpeed, k);
            _tpc.SprintSpeed = Mathf.Lerp(a.sprintSpeed, b.sprintSpeed, k);
            _tpc.JumpHeight = Mathf.Lerp(a.jumpHeight, b.jumpHeight, k);
            _gravityMultiplier = Mathf.Lerp(a.gravityMultiplier, b.gravityMultiplier, k);

            _cc.stepOffset = Mathf.Lerp(a.stepOffset, b.stepOffset, k); // world metres

            if (_animator != null)
                _animator.speed = Mathf.Lerp(a.animationSpeedMultiplier, b.animationSpeedMultiplier, k);
        }

        private void SetScale(float s)
        {
            _scale = s;
            transform.localScale = Vector3.one * s;

            float skinWorld = SkinWorld(s);
            _cc.height = _baseHeight;
            _cc.radius = _baseRadius;
            _cc.skinWidth = skinWorld; // world metres
            // bottom of capsule = one skin width above the feet (in world units) at this scale
            _cc.center = new Vector3(_baseCenter.x, _baseHeight * 0.5f + skinWorld / s, _baseCenter.z);
            // PhysX still holds the capsule centre from the old scale; re-sync or Move() teleports us.
            Physics.SyncTransforms();

            _tpc.GroundedRadius = _baseGroundedRadius * s;
            _tpc.GroundedOffset = _baseGroundedOffset * s;
        }

        private void ApplyGravity()
        {
            _tpc.Gravity = _baseGravity * _gravityMultiplier * (IsSlamming ? slamGravityBoost : 1f);
        }

        // World-space skin: proportional to the body (Unity recommends ~10% of the radius), with a floor
        // so a Small capsule doesn't jitter.
        private float SkinWorld(float scale) => Mathf.Max(0.008f, _baseSkin * scale);

        // ---------------------------------------------------------------------
        // Space check
        // ---------------------------------------------------------------------

        private int ObstacleMask => obstacleLayers.value & ~(1 << gameObject.layer);

        private void GetCheckCapsule(Vector3 feet, float scale, out Vector3 p0, out Vector3 p1, out float r)
        {
            float radius = _baseRadius * scale;
            float height = Mathf.Max(_baseHeight * scale, radius * 2f);
            float skin = SkinWorld(scale);
            // Lift the bottom a little: on a slope a wider sphere with the same lowest point dips into the
            // ground (by (r2 - r1) * (1/cos θ - 1)); 0.3 r covers slopes up to ~40°. The top is exact, so
            // ceilings are still caught.
            float bottom = skin + radius * 0.3f + 0.02f;
            float top = skin + height - checkInset;
            r = Mathf.Max(0.01f, radius - checkInset);
            float c0 = bottom + r;
            float c1 = Mathf.Max(c0, top - r);
            p0 = feet + Vector3.up * c0;
            p1 = feet + Vector3.up * c1;
        }

        private bool Overlaps(Vector3 feet, float scale)
        {
            GetCheckCapsule(feet, scale, out var p0, out var p1, out var r);
            return Physics.CheckCapsule(p0, p1, r, ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        private bool FindRoomToGrow(float targetScale, out Vector3 nudge)
        {
            Vector3 feet = transform.position;
            Vector3 slopeLift = SlopeLift(targetScale);

            if (!Overlaps(feet + slopeLift, targetScale))
            {
                nudge = slopeLift;
                return true;
            }

            nudge = Vector3.zero;
            if (!nudgeAwayFromWalls) return false;

            float radiusGain = (targetScale - _scale) * _baseRadius;
            Vector3 away = AwayFromOverlap(feet, targetScale);
            GetCheckCapsule(feet, _scale, out var c0, out var c1, out var cr);

            for (int pass = 1; pass <= 2; pass++)
            {
                float dist = (radiusGain + 0.05f) * pass;
                for (int i = -1; i < NudgeDirections.Length; i++)
                {
                    Vector3 dir = i < 0 ? away : NudgeDirections[i];
                    if (dir == Vector3.zero) continue;
                    Vector3 candidate = feet + dir * dist + slopeLift;
                    if (Overlaps(candidate, targetScale)) continue;
                    // the current body must be able to get there
                    if (Physics.CapsuleCast(c0, c1, cr, dir, dist, ObstacleMask, QueryTriggerInteraction.Ignore))
                        continue;
                    nudge = dir * dist + slopeLift;
                    return true;
                }
            }
            return false;
        }

        // Horizontal direction pointing away from whatever the target capsule overlaps.
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private Vector3 AwayFromOverlap(Vector3 feet, float scale)
        {
            GetCheckCapsule(feet, scale, out var p0, out var p1, out var r);
            int n = Physics.OverlapCapsuleNonAlloc(p0, p1, r, _overlapBuffer, ObstacleMask, QueryTriggerInteraction.Ignore);
            Vector3 mid = (p0 + p1) * 0.5f;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var col = _overlapBuffer[i];
                Vector3 closest = col is MeshCollider { convex: false } ? col.bounds.ClosestPoint(mid) : col.ClosestPoint(mid);
                Vector3 d = mid - closest;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) sum += d.normalized;
            }
            sum.y = 0f;
            return sum.sqrMagnitude > 1e-6f ? sum.normalized : Vector3.zero;
        }

        // On a slope, raise the feet so the wider bottom sphere doesn't start inside the ground.
        private Vector3 SlopeLift(float targetScale)
        {
            if (!_tpc.Grounded) return Vector3.zero;
            Vector3 origin = transform.position + Vector3.up * (0.5f * _scale);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 1.5f * _scale + 0.5f, ObstacleMask, QueryTriggerInteraction.Ignore))
                return Vector3.zero;
            float cos = Mathf.Clamp(hit.normal.y, 0.5f, 1f);
            if (cos > 0.999f) return Vector3.zero;
            float radiusGain = (targetScale - _scale) * _baseRadius;
            return Vector3.up * (radiusGain * (1f / cos - 1f) + 0.01f);
        }

        private static Vector3[] BuildDirections(int count)
        {
            var dirs = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                dirs[i] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            }
            return dirs;
        }

        // ---------------------------------------------------------------------
        // Size-based pushing
        // ---------------------------------------------------------------------

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic) return;
            if ((pushLayers.value & (1 << body.gameObject.layer)) == 0) return;
            // Standing on it? Use the contact normal: ThirdPersonController always moves with -2 m/s vertical
            // velocity on the ground, so hit.moveDirection points ~45° down even when walking straight into a crate.
            if (hit.normal.y > 0.6f) return;

            SizeProfile profile = CurrentProfile;
            if (profile == null || body.mass > profile.pushStrength) return; // too heavy for this size

            Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();

            float intended = (_inputs != null && _inputs.sprint) ? _tpc.SprintSpeed : _tpc.MoveSpeed;
            float heaviness = profile.pushStrength > 0f ? body.mass / profile.pushStrength : 1f;
            float targetSpeed = intended * Mathf.Lerp(1f, heaviestPushSpeedFactor, heaviness);

            float along = Vector3.Dot(body.linearVelocity, dir);
            if (along < targetSpeed)
                body.AddForce(dir * (targetSpeed - along), ForceMode.VelocityChange);
        }

        // ---------------------------------------------------------------------
        // Gizmos: the capsule the next grow would test
        // ---------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !_initialised || CurrentSize == PlayerSize.Big) return;
            var next = GetProfile(CurrentSize + 1);
            if (next == null) return;
            GetCheckCapsule(transform.position, next.scale, out var p0, out var p1, out var r);
            Gizmos.color = Overlaps(transform.position, next.scale) ? new Color(1f, 0.2f, 0.2f, 0.6f) : new Color(0.2f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(p0, r);
            Gizmos.DrawWireSphere(p1, r);
            Gizmos.DrawLine(p0 + Vector3.left * r, p1 + Vector3.left * r);
            Gizmos.DrawLine(p0 + Vector3.right * r, p1 + Vector3.right * r);
            Gizmos.DrawLine(p0 + Vector3.forward * r, p1 + Vector3.forward * r);
            Gizmos.DrawLine(p0 + Vector3.back * r, p1 + Vector3.back * r);
        }
    }
}

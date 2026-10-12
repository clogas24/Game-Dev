using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Animates what the player holds, on top of the Starter Assets locomotion clips:
    /// - Carry: the right hand follows the jack-o'-lantern's handle (humanoid hand IK), and the
    ///   lantern sways a little as the player moves.
    /// - Lantern-raise: while LanternController.IsFlaring the lantern prop lifts above the
    ///   shoulder and tips forward, and the arm follows it because the hand is pinned to it.
    /// - Throw: when CandyThrower starts a throw the left hand swings back, up and through.
    /// - Flame: the lantern's glow follows the real light's colour and strength, and its flame
    ///   particles stop while the lantern is out.
    /// All of it is procedural: Starter Assets ships only idle, walk, run, jump and land clips.
    /// Only the visual prop moves. The Light and RevealLight stay where they are, because their
    /// position is the centre of the reveal and repel radius.
    /// Setup: on the Player root, next to the Animator (OnAnimatorIK is only sent there). The
    /// Animator's controller needs IK Pass ticked on its base layer. Assign Prop and Handle.
    /// </summary>
    [DisallowMultipleComponent]
    public class LanternProp : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("References")]
        [SerializeField, Tooltip("The jack-o'-lantern visual that moves. Its starting local position is the carry pose.")]
        private Transform prop;
        [SerializeField, Tooltip("Where the right hand grips (a child of Prop).")]
        private Transform handle;
        [SerializeField, Tooltip("The lantern's real light. Read only, for the glow colour.")]
        private Light lanternLight;
        [SerializeField, Tooltip("Renderers whose emission follows the light (the carved face).")]
        private Renderer[] glowRenderers;
        [SerializeField, Tooltip("Flame particles, stopped while the lantern is out. Optional.")]
        private ParticleSystem flame;

        [Header("Lantern raise (flare)")]
        [SerializeField, Tooltip("How far the prop moves from the carry pose while flaring (local metres).")]
        private Vector3 raiseOffset = new Vector3(0.05f, 0.62f, 0.12f);
        [SerializeField, Tooltip("Prop tilt at the top of the raise (degrees).")]
        private Vector3 raiseEuler = new Vector3(-18f, 0f, 8f);
        [SerializeField, Min(0.1f), Tooltip("How fast the prop moves between carry and raised. Higher = snappier.")]
        private float raiseSharpness = 11f;

        [Header("Carry sway")]
        [SerializeField, Min(0f), Tooltip("Sway of the carried lantern at full sprint (degrees).")]
        private float swayDegrees = 9f;
        [SerializeField, Min(0f), Tooltip("Bob of the carried lantern at full sprint (metres).")]
        private float bobHeight = 0.035f;
        [SerializeField, Min(0f), Tooltip("Sway cycles per second at walking speed.")]
        private float swayFrequency = 1.8f;

        [Header("Hand IK")]
        [SerializeField, Range(0f, 1f), Tooltip("How strongly the right hand is pinned to the handle.")]
        private float rightHandWeight = 0.9f;
        [SerializeField, Tooltip("Where the right elbow points, in the player's local space.")]
        private Vector3 elbowHint = new Vector3(0.7f, 0.9f, -0.25f);

        [Header("Throw (left hand)")]
        [SerializeField, Min(0.05f), Tooltip("Seconds the throwing motion lasts.")]
        private float throwSeconds = 0.42f;
        [SerializeField, Tooltip("Left hand at the back of the swing, in the player's local space.")]
        private Vector3 throwWindUp = new Vector3(-0.38f, 1.15f, -0.3f);
        [SerializeField, Tooltip("Left hand at the moment of release.")]
        private Vector3 throwRelease = new Vector3(-0.12f, 1.62f, 0.55f);
        [SerializeField, Tooltip("Left hand at the end of the follow-through.")]
        private Vector3 throwFollow = new Vector3(-0.05f, 1.05f, 0.6f);

        [Header("Glow")]
        [SerializeField, Min(0f), Tooltip("Emission strength of the carved face at normal light intensity.")]
        private float glowStrength = 2.2f;
        [SerializeField, Min(0.01f), Tooltip("The light intensity that counts as normal (LanternController's Normal Intensity).")]
        private float referenceIntensity = 4f;

        private Animator _animator;
        private LanternController _lantern;
        private CandleManager _candle;
        private CandyThrower _thrower;
        private CharacterController _controller;
        private MaterialPropertyBlock _block;
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private float _raise;
        private float _swayPhase;
        private float _throwTime = -1f;
        private bool _wasThrowing;
        private bool _flameOn = true;

        /// <summary>0 at the carry pose, 1 with the lantern fully raised.</summary>
        public float Raise => _raise;
        /// <summary>True while the throwing motion is playing.</summary>
        public bool IsThrowAnimating => _throwTime >= 0f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _lantern = GetComponent<LanternController>();
            _candle = GetComponent<CandleManager>();
            _thrower = GetComponent<CandyThrower>();
            _controller = GetComponent<CharacterController>();
            _block = new MaterialPropertyBlock();
            if (prop != null)
            {
                _restPosition = prop.localPosition;
                _restRotation = prop.localRotation;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool lit = _candle == null || _candle.IsLit;

            // Rising edge of CandyThrower.IsThrowing = a candy just left the hand. A drop (G) never sets it.
            bool throwing = _thrower != null && _thrower.IsThrowing;
            if (throwing && !_wasThrowing) _throwTime = 0f;
            _wasThrowing = throwing;
            if (_throwTime >= 0f)
            {
                _throwTime += dt;
                if (_throwTime >= throwSeconds) _throwTime = -1f;
            }

            bool flaring = _lantern != null && _lantern.IsFlaring;
            _raise = Mathf.Lerp(_raise, flaring ? 1f : 0f, 1f - Mathf.Exp(-raiseSharpness * dt));

            if (prop != null)
            {
                float speed = 0f;
                if (_controller != null)
                {
                    Vector3 velocity = _controller.velocity;
                    speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
                }
                // Faster steps swing the lantern faster and further; a raised lantern is held steady.
                float moving = Mathf.Clamp01(speed / 5.3f);
                _swayPhase += dt * swayFrequency * Mathf.Lerp(1f, 1.7f, moving) * Mathf.PI * 2f;
                float carry = (1f - _raise) * Mathf.Lerp(0.15f, 1f, moving);
                float sway = Mathf.Sin(_swayPhase) * swayDegrees * carry;
                float bob = Mathf.Abs(Mathf.Cos(_swayPhase)) * bobHeight * carry;

                float eased = Mathf.SmoothStep(0f, 1f, _raise);
                prop.localPosition = _restPosition + raiseOffset * eased + Vector3.up * bob;
                prop.localRotation = _restRotation * Quaternion.Euler(raiseEuler * eased) * Quaternion.Euler(sway * 0.4f, 0f, sway);
            }

            UpdateGlow(lit);
        }

        private void UpdateGlow(bool lit)
        {
            if (flame != null && lit != _flameOn)
            {
                _flameOn = lit;
                if (lit) flame.Play(true);
                else flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (glowRenderers == null || glowRenderers.Length == 0) return;
            Color emission = Color.black;
            if (lit && lanternLight != null)
            {
                emission = lanternLight.color * (glowStrength * lanternLight.intensity / referenceIntensity);
            }
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                Renderer r = glowRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(_block);
            }
        }

        // Sent by the Animator after the clips have posed the body (needs IK Pass on the layer).
        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null) return;

            if (handle != null)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, handle.position);
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, rightHandWeight * 0.6f);
                _animator.SetIKHintPosition(AvatarIKHint.RightElbow, transform.TransformPoint(elbowHint));
            }

            if (_throwTime < 0f)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                return;
            }

            // The hand is pulled back to the wind-up for the first 30% of the motion (the weight
            // below blends it in), reaches the release at 60%, then follows through.
            float t = Mathf.Clamp01(_throwTime / throwSeconds);
            Vector3 local = t < 0.3f
                ? throwWindUp
                : t < 0.6f
                    ? Vector3.Lerp(throwWindUp, throwRelease, Mathf.SmoothStep(0f, 1f, (t - 0.3f) / 0.3f))
                    : Vector3.Lerp(throwRelease, throwFollow, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
            // Blend the hand in over the first tenth and out over the last fifth.
            float weight = Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((1f - t) / 0.2f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            _animator.SetIKPosition(AvatarIKGoal.LeftHand, transform.TransformPoint(local));
        }
    }
}

using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A platform that only exists inside light. Revealed: it fades in and is solid at once.
    /// Not revealed (dark, or overexposed by a flare): it fades out and its colliders switch off
    /// after a short grace period, so a flicker at the edge of the light doesn't drop the player.
    /// Setup: put on an object with a Renderer using a TRANSPARENT URP material and at least one
    /// Collider (children count). Leave it on a layer the player treats as ground (Default).
    /// Alpha is written through a MaterialPropertyBlock, so the shared material is never
    /// instanced. By default the bounds centre plus the four top corners are tested against
    /// RevealLight; assign Sample Points to override that for odd shapes.
    /// </summary>
    public class SpiritPlatform : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        [Header("References (auto-filled from children if left empty)")]
        [SerializeField, Tooltip("Renderers to fade. Empty = all renderers on this object and its children.")]
        private Renderer[] renderers;
        [SerializeField, Tooltip("Colliders to switch on and off. Empty = all colliders on this object and its children.")]
        private Collider[] colliders;
        [SerializeField, Tooltip("Optional custom points tested against the lights. Empty = bounds centre + four top corners.")]
        private Transform[] samplePoints;

        [Header("Reveal")]
        [SerializeField, Min(0.02f), Tooltip("Seconds between light checks. 0.1 = about 10 checks per second.")]
        private float checkInterval = 0.1f;
        [SerializeField, Range(0f, 1f), Tooltip("Alpha when fully revealed.")]
        private float revealedAlpha = 0.85f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to fade in once revealed. The collider turns on immediately.")]
        private float fadeInTime = 0.15f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to fade out once no longer revealed.")]
        private float fadeOutTime = 0.35f;
        [SerializeField, Min(0f), Tooltip("Seconds the collider stays solid after the light is lost.")]
        private float graceTime = 0.35f;

        [Header("Hidden shimmer")]
        [SerializeField, Tooltip("Show a faint pulsing outline while hidden, as a hint that something is there.")]
        private bool shimmerWhenHidden;
        [SerializeField, Range(0f, 0.3f), Tooltip("Peak alpha of the hidden shimmer.")]
        private float shimmerAlpha = 0.06f;
        [SerializeField, Min(0f), Tooltip("Shimmer pulses per second.")]
        private float shimmerSpeed = 0.6f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when the platform appears.")]
        private AudioClip appearClip;
        [SerializeField, Tooltip("Played when the platform starts to vanish.")]
        private AudioClip vanishClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for appear/vanish sounds.")]
        private float volume = 0.7f;

        private readonly Vector3[] _localSamples = new Vector3[5];
        private MaterialPropertyBlock _block;
        private Color[] _baseColors;
        private int[] _colorIds;
        private bool _revealed;
        private bool _solid;
        private float _alpha;
        private float _appliedAlpha = -1f;
        private float _checkTimer;
        private float _graceTimer;
        private float _shimmerPhase;

        /// <summary>True while at least one sample point is lit and not overexposed.</summary>
        public bool IsRevealed => _revealed;
        /// <summary>True while the colliders are on (includes the grace period).</summary>
        public bool IsSolid => _solid;

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            if (colliders == null || colliders.Length == 0) colliders = GetComponentsInChildren<Collider>();

            _block = new MaterialPropertyBlock();
            _baseColors = new Color[renderers.Length];
            _colorIds = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material shared = renderers[i].sharedMaterial;
                bool hasBase = shared != null && shared.HasProperty(BaseColorId);
                _colorIds[i] = hasBase ? BaseColorId : LegacyColorId;
                _baseColors[i] = shared != null && shared.HasProperty(_colorIds[i]) ? shared.GetColor(_colorIds[i]) : Color.cyan;
            }

            CacheLocalSamples();
            _shimmerPhase = Random.value * Mathf.PI * 2f;
            _checkTimer = Random.value * checkInterval; // stagger platforms across frames

            _solid = true;
            SetSolid(false);
            ApplyAlpha(0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _checkTimer -= dt;
            if (_checkTimer <= 0f)
            {
                _checkTimer += checkInterval;
                bool lit = SampleLights();
                if (lit != _revealed) SetRevealed(lit);
            }

            if (!_revealed && _solid)
            {
                _graceTimer -= dt;
                if (_graceTimer <= 0f) SetSolid(false);
            }

            float target;
            float rate;
            if (_revealed)
            {
                target = revealedAlpha;
                rate = revealedAlpha / fadeInTime;
            }
            else
            {
                target = shimmerWhenHidden
                    ? shimmerAlpha * (0.5f + 0.5f * Mathf.Sin(Time.time * shimmerSpeed * Mathf.PI * 2f + _shimmerPhase))
                    : 0f;
                rate = revealedAlpha / fadeOutTime;
            }

            _alpha = Mathf.MoveTowards(_alpha, target, rate * dt);
            if (!Mathf.Approximately(_alpha, _appliedAlpha)) ApplyAlpha(_alpha);
        }

        private bool SampleLights()
        {
            if (samplePoints != null && samplePoints.Length > 0)
            {
                for (int i = 0; i < samplePoints.Length; i++)
                {
                    if (samplePoints[i] != null && RevealLight.IsRevealed(samplePoints[i].position)) return true;
                }
                return false;
            }

            Transform t = transform;
            for (int i = 0; i < _localSamples.Length; i++)
            {
                if (RevealLight.IsRevealed(t.TransformPoint(_localSamples[i]))) return true;
            }
            return false;
        }

        private void SetRevealed(bool revealed)
        {
            _revealed = revealed;
            if (revealed)
            {
                SetSolid(true);
                Sfx.PlayAt(appearClip, transform.position, volume);
            }
            else
            {
                _graceTimer = graceTime;
                Sfx.PlayAt(vanishClip, transform.position, volume);
            }
        }

        private void SetSolid(bool solid)
        {
            if (_solid == solid) return;
            _solid = solid;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = solid;
            }
        }

        private void ApplyAlpha(float alpha)
        {
            _appliedAlpha = alpha;
            bool visible = alpha > 0.002f;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.enabled = visible;
                if (!visible) continue;

                Color c = _baseColors[i];
                c.a = alpha;
                r.GetPropertyBlock(_block);
                _block.SetColor(_colorIds[i], c);
                r.SetPropertyBlock(_block);
            }
        }

        // Stored in local space so platforms can be moved (or parented to movers) at runtime.
        private void CacheLocalSamples()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (any) bounds.Encapsulate(renderers[i].bounds);
                else bounds = renderers[i].bounds;
                any = true;
            }

            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            float top = bounds.max.y;
            _localSamples[0] = transform.InverseTransformPoint(c);
            _localSamples[1] = transform.InverseTransformPoint(new Vector3(c.x - e.x, top, c.z - e.z));
            _localSamples[2] = transform.InverseTransformPoint(new Vector3(c.x - e.x, top, c.z + e.z));
            _localSamples[3] = transform.InverseTransformPoint(new Vector3(c.x + e.x, top, c.z - e.z));
            _localSamples[4] = transform.InverseTransformPoint(new Vector3(c.x + e.x, top, c.z + e.z));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _revealed ? Color.cyan : Color.gray;
            if (samplePoints != null && samplePoints.Length > 0)
            {
                for (int i = 0; i < samplePoints.Length; i++)
                {
                    if (samplePoints[i] != null) Gizmos.DrawSphere(samplePoints[i].position, 0.08f);
                }
                return;
            }

            if (!Application.isPlaying)
            {
                if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
                CacheLocalSamples();
            }
            for (int i = 0; i < _localSamples.Length; i++)
            {
                Gizmos.DrawSphere(transform.TransformPoint(_localSamples[i]), 0.08f);
            }
        }
    }
}

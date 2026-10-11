using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
    /// <summary>
    /// Drives the lantern: a point light plus the RevealLight that makes spirit platforms exist.
    /// The radius stays full until the candle drops below 25%, then shrinks to a minimum.
    /// Holding Flare makes the light bigger, brighter and whiter, tells CandleManager to drain
    /// faster, marks the RevealLight as overexposing (platforms vanish), and calls
    /// IRepellable.Repel on everything on the ghost layers inside the flare radius.
    /// Setup: on the Player root next to CandleManager and PlayerInput. Wire Lantern Light and
    /// Reveal Light to the "Lantern" child, and set Ghost Layers to the Ghost layer. Input comes
    /// from the "Flare" action of the PlayerInput's actions asset (LanternKeeper.inputactions).
    /// </summary>
    [DisallowMultipleComponent]
    public class LanternController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("The lantern's point light (on the Lantern child).")]
        private Light lanternLight;
        [SerializeField, Tooltip("The lantern's RevealLight (on the Lantern child).")]
        private RevealLight revealLight;
        [SerializeField, Tooltip("Name of the hold-to-flare action in the PlayerInput actions asset.")]
        private string flareActionName = "Flare";

        [Header("Normal light")]
        [SerializeField, Min(0f), Tooltip("Reveal radius with a healthy candle (metres).")]
        private float fullRadius = 6f;
        [SerializeField, Min(0f), Tooltip("Reveal radius just before the candle goes out (metres).")]
        private float minRadius = 2.5f;
        [SerializeField, Range(0.01f, 1f), Tooltip("Candle fraction below which the radius starts shrinking. Overview: 25%.")]
        private float shrinkBelow = 0.25f;
        [SerializeField, Min(0f), Tooltip("Light intensity with a healthy candle.")]
        private float normalIntensity = 4f;
        [SerializeField, Range(0f, 1f), Tooltip("Intensity scale just before the candle goes out.")]
        private float lowCandleIntensityScale = 0.5f;
        [SerializeField, Tooltip("Light colour when not flaring (warm candle orange).")]
        private Color normalColor = new Color(1f, 0.62f, 0.25f);

        [Header("Flicker")]
        [SerializeField, Range(0f, 0.5f), Tooltip("How much Perlin noise wobbles the intensity (0.15 = +/-15%). Visual only; the reveal radius does not flicker.")]
        private float flickerAmount = 0.15f;
        [SerializeField, Min(0f), Tooltip("Flicker speed.")]
        private float flickerSpeed = 6f;

        [Header("Flare")]
        [SerializeField, Min(0f), Tooltip("Light radius, overexposure radius and ghost repel range while flaring (metres).")]
        private float flareRadius = 10f;
        [SerializeField, Min(1f), Tooltip("Intensity multiplier while flaring.")]
        private float flareIntensityMultiplier = 3f;
        [SerializeField, Tooltip("Light colour while flaring (hot white).")]
        private Color flareColor = new Color(1f, 0.96f, 0.88f);
        [SerializeField, Min(0f), Tooltip("Candle (0-100) needed to START a flare. A flare already running continues down to 0.")]
        private float minCandleToFlare = 5f;
        [SerializeField, Min(0.1f), Tooltip("How fast radius, intensity and colour blend between normal and flare. Higher = snappier.")]
        private float blendSharpness = 12f;

        [Header("Repel")]
        [SerializeField, Tooltip("Layers searched for IRepellable while flaring. Set to the Ghost layer.")]
        private LayerMask ghostLayers;
        [SerializeField, Min(0.02f), Tooltip("Seconds between repel pulses while the flare is held.")]
        private float repelInterval = 0.2f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when a flare starts.")]
        private AudioClip flareStartClip;
        [SerializeField, Tooltip("Played when a flare ends.")]
        private AudioClip flareEndClip;
        [SerializeField, Tooltip("Played when Flare is pressed without enough candle.")]
        private AudioClip flareDeniedClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for flare sounds.")]
        private float volume = 1f;

        private readonly Collider[] _hits = new Collider[16];
        private readonly Dictionary<Collider, IRepellable> _repellables = new Dictionary<Collider, IRepellable>(16);
        private CandleManager _candle;
        private InputAction _flareAction;
        private bool _isFlaring;
        private float _radius;
        private float _intensity;
        private Color _color;
        private float _repelTimer;
        private float _flickerSeed;

        /// <summary>True while the flare is active. Step 3 ghosts double their notice range on this.</summary>
        public bool IsFlaring => _isFlaring;
        /// <summary>Current reveal radius in metres.</summary>
        public float CurrentRadius => _radius;
        public float FlareRadius => flareRadius;
        /// <summary>Raised when a flare starts (true) or stops (false).</summary>
        public event Action<bool> OnFlareChanged;

        private void Awake()
        {
            _candle = GetComponent<CandleManager>();
            _flickerSeed = UnityEngine.Random.value * 100f;
            _radius = fullRadius;
            _intensity = normalIntensity;
            _color = normalColor;
        }

        private void Start()
        {
            if (TryGetComponent(out PlayerInput playerInput) && playerInput.actions != null)
            {
                _flareAction = playerInput.actions.FindAction(flareActionName);
            }
            if (_flareAction == null)
            {
                Debug.LogWarning($"[LanternController] No '{flareActionName}' action found on PlayerInput; flare is disabled.", this);
            }
        }

        private void OnDisable() => SetFlaring(false);

        private void Update()
        {
            float dt = Time.deltaTime;
            bool lit = _candle == null || _candle.IsLit;
            bool held = _flareAction != null && _flareAction.IsPressed();

            if (_isFlaring)
            {
                if (!held || !lit) SetFlaring(false);
            }
            else if (held && lit)
            {
                if (_candle == null || _candle.Current >= minCandleToFlare) SetFlaring(true);
                else if (_flareAction.WasPressedThisFrame()) Sfx.PlayAt(flareDeniedClip, transform.position, volume);
            }

            float targetRadius = 0f;
            float targetIntensity = 0f;
            Color targetColor = normalColor;
            if (lit)
            {
                if (_isFlaring)
                {
                    targetRadius = flareRadius;
                    targetIntensity = normalIntensity * flareIntensityMultiplier;
                    targetColor = flareColor;
                }
                else
                {
                    // 1 while the candle is at or above the shrink threshold, falling to 0 as it nears empty.
                    float health = _candle == null ? 1f : Mathf.Clamp01(_candle.Normalized / shrinkBelow);
                    targetRadius = Mathf.Lerp(minRadius, fullRadius, health);
                    targetIntensity = normalIntensity * Mathf.Lerp(lowCandleIntensityScale, 1f, health);
                }
            }

            float blend = 1f - Mathf.Exp(-blendSharpness * dt);
            _radius = Mathf.Lerp(_radius, targetRadius, blend);
            _intensity = Mathf.Lerp(_intensity, targetIntensity, blend);
            _color = Color.Lerp(_color, targetColor, blend);
            if (!lit && _radius < 0.05f) _radius = 0f;

            if (revealLight != null) revealLight.Radius = _radius;
            if (lanternLight != null)
            {
                float flicker = 1f + (Mathf.PerlinNoise(Time.time * flickerSpeed, _flickerSeed) - 0.5f) * 2f * flickerAmount;
                lanternLight.intensity = _intensity * flicker;
                lanternLight.color = _color;
            }

            if (_isFlaring)
            {
                _repelTimer -= dt;
                if (_repelTimer <= 0f)
                {
                    _repelTimer = repelInterval;
                    RepelNearby();
                }
            }
        }

        private void SetFlaring(bool flaring)
        {
            if (_isFlaring == flaring) return;
            _isFlaring = flaring;
            _repelTimer = 0f;
            if (_candle != null) _candle.Flaring = flaring;
            if (revealLight != null) revealLight.Overexposing = flaring;
            Sfx.PlayAt(flaring ? flareStartClip : flareEndClip, transform.position, volume);
            OnFlareChanged?.Invoke(flaring);
        }

        private void RepelNearby()
        {
            Vector3 source = revealLight != null ? revealLight.transform.position : transform.position;
            int count = Physics.OverlapSphereNonAlloc(source, flareRadius, _hits, ghostLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i];
                if (!_repellables.TryGetValue(hit, out IRepellable target))
                {
                    // Looked up once per collider, then cached, so the pulse itself never calls GetComponent.
                    if (_repellables.Count > 128) _repellables.Clear();
                    target = hit.GetComponentInParent<IRepellable>();
                    _repellables[hit] = target;
                }
                target?.Repel(source);
                _hits[i] = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 centre = revealLight != null ? revealLight.transform.position : transform.position;
            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(centre, flareRadius);
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(centre, minRadius);
        }
    }
}

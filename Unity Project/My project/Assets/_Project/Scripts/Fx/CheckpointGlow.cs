using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// The lighting-up animation of a porch lantern: when Checkpoint switches its lit visual
    /// on, the flame grows from nothing with a small overshoot and the lamp's light fades in,
    /// then both keep a gentle candle flicker.
    /// Cosmetic only. Checkpoint still enables the Light and the RevealLight at once, so the
    /// reveal radius is there from the first frame; only the brightness is eased.
    /// Setup: on the checkpoint's "Lantern (lit)" object (the one Checkpoint activates).
    /// </summary>
    public class CheckpointGlow : MonoBehaviour
    {
        [SerializeField, Tooltip("The lamp's Light. Its intensity is faded in.")]
        private Light lampLight;
        [SerializeField, Tooltip("The flame that grows. Optional.")]
        private Transform flame;
        [SerializeField, Min(0f), Tooltip("Light intensity once fully lit.")]
        private float litIntensity = 4f;
        [SerializeField, Min(0.05f), Tooltip("Seconds the lighting-up takes.")]
        private float seconds = 0.9f;
        [SerializeField, Range(0f, 0.5f), Tooltip("How much the lit lamp flickers (0.1 = +/-10%).")]
        private float flicker = 0.1f;
        [SerializeField, Min(0f), Tooltip("Flicker speed.")]
        private float flickerSpeed = 5f;

        private Vector3 _flameScale = Vector3.one;
        private bool _hasFlameScale;
        private float _time;
        private float _seed;

        private void OnEnable()
        {
            if (flame != null && !_hasFlameScale)
            {
                _flameScale = flame.localScale;
                _hasFlameScale = true;
            }
            _time = 0f;
            _seed = Random.value * 100f;
            Apply();
        }

        private void Update()
        {
            _time += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            float t = Mathf.Clamp01(_time / seconds);
            float wobble = 1f + (Mathf.PerlinNoise(Time.time * flickerSpeed, _seed) - 0.5f) * 2f * flicker;
            if (lampLight != null) lampLight.intensity = litIntensity * Mathf.SmoothStep(0f, 1f, t) * wobble;
            if (flame == null) return;
            // Grows past full size at 70% of the way, then settles.
            float grow = t < 0.7f ? Mathf.SmoothStep(0f, 1.25f, t / 0.7f) : Mathf.Lerp(1.25f, 1f, (t - 0.7f) / 0.3f);
            flame.localScale = _flameScale * (grow * wobble);
        }
    }
}

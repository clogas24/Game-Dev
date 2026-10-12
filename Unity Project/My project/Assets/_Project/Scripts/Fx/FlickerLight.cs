using UnityEngine;

namespace LanternKeeper
{
    /// <summary>Candle flicker for a decorative light (porch jack-o'-lanterns, the farmhouse beacon's glow).</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.6f), Tooltip("How much the intensity wobbles (0.15 = +/-15%).")]
        private float amount = 0.15f;
        [SerializeField, Min(0f), Tooltip("Flicker speed.")]
        private float speed = 5f;

        private Light _light;
        private float _intensity;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _intensity = _light.intensity;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            _light.intensity = _intensity * (1f + (Mathf.PerlinNoise(Time.time * speed, _seed) - 0.5f) * 2f * amount);
        }
    }
}

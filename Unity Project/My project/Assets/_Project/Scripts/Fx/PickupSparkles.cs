using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A sparkle burst wherever a pickup is collected. Pickups destroy themselves as they are
    /// collected, so the burst lives here: one shared particle system in the scene, moved to
    /// the pickup and told to emit.
    /// Setup: one per scene, on an object with a ParticleSystem that simulates in world space
    /// and does not play on awake.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PickupSparkles : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("Particles per pickup.")]
        private int burstCount = 16;
        [SerializeField, Tooltip("Sparkle colour for candle stubs.")]
        private Color candleColor = new Color(1f, 0.8f, 0.35f);
        [SerializeField, Tooltip("Sparkle colour for candy.")]
        private Color candyColor = new Color(1f, 0.45f, 0.85f);
        [SerializeField, Tooltip("Extra chime played with the burst. Optional.")]
        private AudioClip sparkleClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume of the chime.")]
        private float volume = 0.35f;

        private ParticleSystem _particles;

        private void Awake() => _particles = GetComponent<ParticleSystem>();

        // GameManager clears GameEvents in its Awake (execution order -100), before this runs.
        private void OnEnable() => GameEvents.PickupCollected += HandlePickupCollected;

        private void OnDisable() => GameEvents.PickupCollected -= HandlePickupCollected;

        private void HandlePickupCollected(Pickup pickup)
        {
            if (pickup == null) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = pickup.transform.position,
                applyShapeToPosition = true,
                startColor = pickup is CandlePickup ? candleColor : candyColor
            };
            _particles.Emit(emit, burstCount);
            Sfx.PlayAt(sparkleClip, pickup.transform.position, volume, 2f, 12f);
        }
    }
}

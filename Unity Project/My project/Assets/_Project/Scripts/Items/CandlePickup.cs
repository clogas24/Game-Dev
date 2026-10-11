using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A candle stub: refills the candle by a fixed amount (+35 in the overview).
    /// Setup: see <see cref="Pickup"/>. Needs a CandleManager in the scene (on the player).
    /// </summary>
    public class CandlePickup : Pickup
    {
        [Header("Candle")]
        [SerializeField, Min(0f), Tooltip("Candle restored. Overview: +35.")]
        private float amount = 35f;
        [SerializeField, Tooltip("If true, the stub is left in the world while the candle is already full.")]
        private bool refuseWhenFull;

        protected override bool TryCollect(GameObject player)
        {
            CandleManager candle = CandleManager.Instance;
            if (candle == null || !candle.IsLit) return false;
            if (refuseWhenFull && candle.Current >= candle.Max) return false;
            candle.Add(amount);
            return true;
        }
    }
}

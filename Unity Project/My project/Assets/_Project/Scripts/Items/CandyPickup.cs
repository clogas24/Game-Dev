using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Gives the player candy (+2 by default). Refused, and left in the world, while the player
    /// already carries the maximum.
    /// Setup: see <see cref="Pickup"/>. The player needs a CandyThrower.
    /// </summary>
    public class CandyPickup : Pickup
    {
        [Header("Candy")]
        [SerializeField, Min(1), Tooltip("Candy given. Anything above the carry limit is lost.")]
        private int amount = 2;

        private GameObject _cachedPlayer;
        private CandyThrower _cachedThrower;

        protected override bool TryCollect(GameObject player)
        {
            // Looked up once per player object: this runs every frame the player stands in a refused pickup.
            if (player != _cachedPlayer)
            {
                _cachedPlayer = player;
                _cachedThrower = player.GetComponentInParent<CandyThrower>();
            }
            return _cachedThrower != null && _cachedThrower.TryAdd(amount);
        }
    }
}

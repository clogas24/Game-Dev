using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A candy bowl: refills the player's candy and stays in the world, so a lure puzzle can
    /// always be retried (overview: "candy is limited and refilled from candy bowls").
    /// CandyPickup is the one-off version.
    /// Setup: a TRIGGER collider around the bowl. The player must be tagged "Player" and carry
    /// a CandyThrower.
    /// </summary>
    public class CandyBowl : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("Candy given per refill. 5 fills the player up.")]
        private int refillAmount = 5;
        [SerializeField, Min(0f), Tooltip("Seconds before the bowl can give candy again.")]
        private float cooldown = 1.5f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when candy is taken.")]
        private AudioClip refillClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for the refill sound.")]
        private float volume = 1f;

        private CandyThrower _thrower;
        private float _readyTime;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _thrower = other.GetComponentInParent<CandyThrower>();
        }

        private void OnTriggerExit(Collider other)
        {
            if (_thrower != null && other.transform == _thrower.transform) _thrower = null;
        }

        private void Update()
        {
            // Polled while the player stands in the bowl, so throwing a candy there refills it again.
            if (_thrower == null || Time.time < _readyTime || _thrower.IsFull) return;
            if (!_thrower.TryAdd(refillAmount)) return;
            _readyTime = Time.time + cooldown;
            Sfx.Play2D(refillClip, volume);
        }
    }
}

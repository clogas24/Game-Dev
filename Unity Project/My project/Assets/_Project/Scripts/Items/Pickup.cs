using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Base class for things the player collects by walking into them. Spins and bobs a visual,
    /// reacts to the "Player" tag, plays a sound and removes itself once collected.
    /// A pickup that is refused (candy while full) stays put and is offered again every frame
    /// the player keeps standing in it.
    /// Setup: an object with a TRIGGER collider and a subclass of this (CandlePickup or
    /// CandyPickup). Put the mesh on a child and assign it as Visual so the trigger itself
    /// stays still. The player must be tagged "Player".
    /// </summary>
    public abstract class Pickup : MonoBehaviour
    {
        [Header("Motion")]
        [SerializeField, Tooltip("Child that spins and bobs. Empty = this object.")]
        private Transform visual;
        [SerializeField, Tooltip("Spin speed (degrees per second).")]
        private float spinSpeed = 90f;
        [SerializeField, Min(0f), Tooltip("Bob height (metres).")]
        private float bobHeight = 0.12f;
        [SerializeField, Min(0f), Tooltip("Bobs per second.")]
        private float bobFrequency = 0.6f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when collected.")]
        private AudioClip collectClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for the collect sound.")]
        private float volume = 1f;

        private Vector3 _visualRestPosition;
        private float _phase;
        private GameObject _player;
        private bool _collected;

        /// <summary>Applies the pickup to the player. Return false to refuse it (it then stays in the world).</summary>
        protected abstract bool TryCollect(GameObject player);

        protected virtual void Awake()
        {
            if (visual == null) visual = transform;
            _visualRestPosition = visual.localPosition;
            _phase = Random.value * Mathf.PI * 2f;
        }

        protected virtual void Update()
        {
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f + _phase) * bobHeight;
            visual.localPosition = _visualRestPosition + Vector3.up * bob;

            if (_player != null) Offer(_player);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _player = other.gameObject;
            Offer(_player);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_player != null && other.gameObject == _player) _player = null;
        }

        private void Offer(GameObject player)
        {
            if (_collected || !TryCollect(player)) return;
            _collected = true;
            Sfx.Play2D(collectClip, volume);
            Destroy(gameObject);
        }
    }
}

using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Shoves the player away from a hit and grants a short invulnerability window.
    /// It moves the CharacterController directly, on top of whatever ThirdPersonController does
    /// that frame, and fades the shove out over its duration.
    /// Setup: on the Player root (needs the CharacterController there). No references to wire.
    /// Step 3 ghosts call <c>if (knockback.Apply(ghostPosition)) candle.DamageFromGhost();</c>
    /// so a hit during invulnerability costs nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerKnockback : MonoBehaviour
    {
        [Header("Knockback")]
        [SerializeField, Min(0f), Tooltip("Initial horizontal speed away from the source (m/s). Fades to 0 over the duration.")]
        private float horizontalSpeed = 9f;
        [SerializeField, Min(0f), Tooltip("Initial upward speed (m/s). Fades to 0 over the duration.")]
        private float upwardSpeed = 3f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the shove lasts. The player FSM reports KnockedBack for this long.")]
        private float duration = 0.4f;

        [Header("Invulnerability")]
        [SerializeField, Min(0f), Tooltip("Seconds after a hit during which further hits are ignored. Overview: 1.2.")]
        private float invulnerabilitySeconds = 1.2f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when a hit lands.")]
        private AudioClip hitClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for the hit sound.")]
        private float volume = 1f;

        private CharacterController _controller;
        private CandleManager _candle;
        private Vector3 _velocity;
        private float _knockTimer;
        private float _invulnerableTimer;

        /// <summary>True while hits are ignored (1.2 s after each hit).</summary>
        public bool Invulnerable => _invulnerableTimer > 0f;
        /// <summary>True while the shove is still moving the player. Read by PlayerStateTracker.</summary>
        public bool IsKnockedBack => _knockTimer > 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _candle = GetComponent<CandleManager>();
        }

        /// <summary>
        /// Knocks the player away from <paramref name="from"/> and starts invulnerability.
        /// Returns false (and does nothing) if the player is invulnerable or the lantern is out.
        /// </summary>
        public bool Apply(Vector3 from)
        {
            if (Invulnerable) return false;
            if (_candle != null && !_candle.IsLit) return false;

            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            away.Normalize();

            _velocity = away * horizontalSpeed + Vector3.up * upwardSpeed;
            _knockTimer = duration;
            _invulnerableTimer = invulnerabilitySeconds;

            Sfx.PlayAt(hitClip, transform.position, volume);
            GameEvents.RaisePlayerHit(from);
            return true;
        }

        /// <summary>Cancels the shove and the invulnerability (respawn).</summary>
        public void ResetState()
        {
            _knockTimer = 0f;
            _invulnerableTimer = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_invulnerableTimer > 0f) _invulnerableTimer -= dt;
            if (_knockTimer <= 0f) return;

            if (_controller.enabled) _controller.Move(_velocity * (_knockTimer / duration) * dt);
            _knockTimer -= dt;
        }
    }
}

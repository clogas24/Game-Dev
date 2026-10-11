using System;
using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// The candle: health and timer in one, from 0 to 100. Drains passively, drains
    /// <c>flareDrainMultiplier</c> times faster while LanternController reports a flare, and
    /// loses <c>ghostHitDamage</c> when a ghost lands a hit.
    /// Setup: one instance on the Player root (singleton, reachable through
    /// <see cref="Instance"/>). No references to wire. Step 3's game flow listens to
    /// <see cref="OnExtinguished"/> and respawns with <see cref="SetTo"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class CandleManager : MonoBehaviour
    {
        public static CandleManager Instance { get; private set; }

        [Header("Candle")]
        [SerializeField, Min(1f), Tooltip("Maximum candle. The overview fixes this at 100.")]
        private float maxCandle = 100f;
        [SerializeField, Min(0f), Tooltip("Candle at scene start.")]
        private float startCandle = 100f;
        [SerializeField, Min(0f), Tooltip("Candle lost per second while not flaring. 0.5 = a full candle lasts 200 s.")]
        private float passiveDrainPerSecond = 0.5f;
        [SerializeField, Min(1f), Tooltip("Drain multiplier while flaring. Overview: 8x.")]
        private float flareDrainMultiplier = 8f;
        [SerializeField, Min(0f), Tooltip("Candle lost when a ghost touches the player. Overview: 20.")]
        private float ghostHitDamage = 20f;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction below which the candle counts as low (warning sound, HUD pulse, lantern shrink). Overview: 25%.")]
        private float lowThreshold = 0.25f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played once each time the candle drops below the low threshold.")]
        private AudioClip candleLowClip;
        [SerializeField, Tooltip("Played when the candle reaches 0.")]
        private AudioClip extinguishClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for candle sounds.")]
        private float volume = 1f;

        private float _current;
        private bool _extinguished;
        private bool _lowWarned;

        /// <summary>Raised whenever the value changes. Argument: candle as 0..1.</summary>
        public event Action<float> OnCandleChanged;
        /// <summary>Raised once when the candle reaches 0. Re-arms after the candle is relit.</summary>
        public event Action OnExtinguished;

        public float Current => _current;
        public float Max => maxCandle;
        /// <summary>Candle as 0..1.</summary>
        public float Normalized => _current / maxCandle;
        public bool IsLit => _current > 0f;
        public bool IsLow => Normalized < lowThreshold;
        public float LowThreshold => lowThreshold;
        public float GhostHitDamage => ghostHitDamage;
        /// <summary>Stops the passive and flare drain (cutscenes, pause, tutorials). Damage still applies.</summary>
        public bool Paused { get; set; }
        /// <summary>Set by LanternController while the flare is held.</summary>
        public bool Flaring { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CandleManager] Duplicate instance destroyed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            _current = Mathf.Clamp(startCandle, 0f, maxCandle);
            _extinguished = _current <= 0f;
            _lowWarned = IsLow;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Paused || _current <= 0f) return;
            float rate = passiveDrainPerSecond * (Flaring ? flareDrainMultiplier : 1f);
            if (rate > 0f) Apply(_current - rate * Time.deltaTime);
        }

        /// <summary>Removes candle. Ignored once the lantern is out.</summary>
        public void Damage(float amount)
        {
            if (amount <= 0f || _current <= 0f) return;
            Apply(_current - amount);
        }

        /// <summary>The standard ghost hit (20 by default).</summary>
        public void DamageFromGhost() => Damage(ghostHitDamage);

        /// <summary>Adds candle, clamped to the maximum. Relights an extinguished lantern.</summary>
        public void Add(float amount)
        {
            if (amount <= 0f) return;
            Apply(_current + amount);
        }

        /// <summary>Sets the candle outright (respawn, debug). Relights if above 0.</summary>
        public void SetTo(float value) => Apply(value);

        private void Apply(float value)
        {
            value = Mathf.Clamp(value, 0f, maxCandle);
            if (Mathf.Approximately(value, _current)) return;
            _current = value;
            OnCandleChanged?.Invoke(Normalized);

            if (!IsLow)
            {
                _lowWarned = false;
            }
            else if (!_lowWarned && _current > 0f)
            {
                _lowWarned = true;
                Sfx.Play2D(candleLowClip, volume);
            }

            if (_current > 0f)
            {
                _extinguished = false;
            }
            else if (!_extinguished)
            {
                _extinguished = true;
                Flaring = false;
                Sfx.Play2D(extinguishClip, volume);
                OnExtinguished?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}

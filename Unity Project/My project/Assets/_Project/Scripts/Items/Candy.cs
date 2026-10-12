using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A thrown candy. It flies as a Rigidbody, "lands" once it has touched something and slowed
    /// down, then goes kinematic and registers in a static list of landed candy that lures can
    /// search with <see cref="FindClosestAvailable"/>.
    /// One claimant per candy: a ghost calls <see cref="Claim"/>, walks over, eats, then calls
    /// <see cref="Consume"/> (or <see cref="ReleaseClaim"/> if interrupted). The owner is a plain
    /// object so any type can claim. Unclaimed candy despawns after its lifetime.
    /// Setup: a prefab with a small Collider, a Rigidbody and this component. CandyThrower
    /// spawns it and calls <see cref="Launch"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Candy : MonoBehaviour
    {
        private static readonly List<Candy> s_landed = new List<Candy>(8);
        private static readonly List<Candy> s_all = new List<Candy>(8);

        [Header("Landing")]
        [SerializeField, Min(0f), Tooltip("Speed (m/s) below which the candy counts as landed, once it has touched something.")]
        private float landSpeed = 0.6f;
        [SerializeField, Min(0f), Tooltip("Shortest flight before it may land (seconds). Stops it landing in the hand.")]
        private float minFlightTime = 0.15f;
        [SerializeField, Min(0.1f), Tooltip("Longest flight before it is forced to land where it is (seconds).")]
        private float maxFlightTime = 4f;

        [Header("Lifetime")]
        [SerializeField, Min(0f), Tooltip("Seconds a landed, unclaimed candy stays before despawning.")]
        private float unclaimedLifetime = 20f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when the candy lands.")]
        private AudioClip landClip;
        [SerializeField, Tooltip("Played when the candy is consumed.")]
        private AudioClip consumeClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for candy sounds.")]
        private float volume = 0.8f;

        private Rigidbody _body;
        private Collider _collider;
        private object _claimant;
        private bool _landed;
        private bool _touched;
        private float _flightTime;
        private float _lifeTimer;

        /// <summary>Every candy that has landed and not yet been consumed or despawned.</summary>
        public static IReadOnlyList<Candy> Landed => s_landed;

        public bool IsLanded => _landed;
        /// <summary>True while a living claimant holds this candy.</summary>
        public bool IsClaimed
        {
            get
            {
                if (_claimant == null) return false;
                // A destroyed Unity object must not keep its claim.
                if (_claimant is Object unityObject && unityObject == null)
                {
                    _claimant = null;
                    return false;
                }
                return true;
            }
        }
        public object Claimant => IsClaimed ? _claimant : null;

        /// <summary>Closest landed, unclaimed candy within range, or null.</summary>
        public static Candy FindClosestAvailable(Vector3 position, float range)
        {
            Candy best = null;
            float bestSqr = range * range;
            for (int i = 0; i < s_landed.Count; i++)
            {
                Candy candy = s_landed[i];
                if (candy.IsClaimed) continue;
                float sqr = (candy.transform.position - position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = candy;
            }
            return best;
        }

        /// <summary>
        /// Removes every candy, landed or still in the air (player respawn). Each one leaves the
        /// lists at once, so no ghost can claim a candy that is about to be destroyed.
        /// </summary>
        public static void ClearAll()
        {
            for (int i = s_all.Count - 1; i >= 0; i--)
            {
                GameObject candy = s_all[i].gameObject;
                candy.SetActive(false);
                Destroy(candy);
            }
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
        }

        private void OnEnable() => s_all.Add(this);

        /// <summary>Starts the flight. <paramref name="ignore"/> is the thrower's collider, if any.</summary>
        public void Launch(Vector3 velocity, Collider ignore = null)
        {
            if (ignore != null && _collider != null) Physics.IgnoreCollision(_collider, ignore, true);
            _body.isKinematic = false;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Random.insideUnitSphere * 8f;
        }

        /// <summary>Reserves this candy for <paramref name="owner"/>. Fails if it has not landed or someone else holds it.</summary>
        public bool Claim(object owner)
        {
            if (owner == null || !_landed) return false;
            if (IsClaimed) return ReferenceEquals(_claimant, owner);
            _claimant = owner;
            return true;
        }

        /// <summary>Gives the candy up without eating it (the claimant was repelled, say).</summary>
        public void ReleaseClaim(object owner)
        {
            if (!ReferenceEquals(_claimant, owner)) return;
            _claimant = null;
            _lifeTimer = unclaimedLifetime;
        }

        /// <summary>The candy has been eaten: remove it from the world.</summary>
        public void Consume()
        {
            Sfx.PlayAt(consumeClip, transform.position, volume);
            Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            if (_landed) return;
            _flightTime += Time.fixedDeltaTime;
            bool settled = _touched && _flightTime >= minFlightTime && _body.linearVelocity.sqrMagnitude <= landSpeed * landSpeed;
            if (settled || _flightTime >= maxFlightTime) Land();
        }

        private void Update()
        {
            if (!_landed || IsClaimed) return;
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f) Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision) => _touched = true;

        private void Land()
        {
            _landed = true;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
            if (_collider != null) _collider.isTrigger = true; // lures should not trip the player
            _lifeTimer = unclaimedLifetime;
            s_landed.Add(this);
            Sfx.PlayAt(landClip, transform.position, volume);
        }

        private void OnDisable()
        {
            s_landed.Remove(this);
            s_all.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_landed.Clear();
            s_all.Clear();
        }
    }
}

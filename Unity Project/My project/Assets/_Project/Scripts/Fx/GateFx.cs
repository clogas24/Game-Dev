using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Rattles a gate's iron bars and kicks up dust when it starts to open or close.
    /// Cosmetic only: Gate still moves the Door (and its collider) by itself; this shakes a
    /// visual child of the door, never the door.
    /// Setup: on the Gate prefab root. Assign the bars' transform and the dust particles.
    /// </summary>
    [RequireComponent(typeof(Gate))]
    public class GateFx : MonoBehaviour
    {
        [SerializeField, Tooltip("Visual child of the door that rattles. Never the Door itself.")]
        private Transform bars;
        [SerializeField, Tooltip("Dust puffed out at the foot of the gate. Optional.")]
        private ParticleSystem dust;
        [SerializeField, Min(0f), Tooltip("Seconds the rattle lasts.")]
        private float rattleSeconds = 0.55f;
        [SerializeField, Min(0f), Tooltip("How far the bars shake (metres, in the door's scaled local space).")]
        private float rattleDistance = 0.012f;
        [SerializeField, Min(0f), Tooltip("How far the bars twist (degrees).")]
        private float rattleDegrees = 1.2f;

        private Gate _gate;
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private float _timer;

        private void Awake()
        {
            _gate = GetComponent<Gate>();
            if (bars == null) return;
            _restPosition = bars.localPosition;
            _restRotation = bars.localRotation;
        }

        private void OnEnable() => _gate.OnOpenChanged += HandleOpenChanged;

        private void OnDisable() => _gate.OnOpenChanged -= HandleOpenChanged;

        private void HandleOpenChanged(Gate gate, bool open)
        {
            _timer = rattleSeconds;
            if (dust != null) dust.Play(true);
        }

        private void Update()
        {
            if (_timer <= 0f || bars == null) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                bars.localPosition = _restPosition;
                bars.localRotation = _restRotation;
                return;
            }
            float strength = _timer / rattleSeconds;
            float t = Time.time * 55f;
            bars.localPosition = _restPosition + new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t * 1.3f)) * (rattleDistance * strength);
            bars.localRotation = _restRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * rattleDegrees * strength);
        }
    }
}

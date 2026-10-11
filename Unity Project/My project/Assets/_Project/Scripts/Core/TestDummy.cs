using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Sandbox stand-in for a ghost so the flare and candy can be tested before Step 3.
    /// When repelled it logs and turns white for a moment. When a landed candy is in range it
    /// claims it, slides over, "eats" for a few seconds, then consumes it.
    /// Setup: a capsule with a Collider on the Ghost layer (the layer in LanternController's
    /// Ghost Layers mask) and this component. Delete it once real ghosts exist.
    /// </summary>
    public class TestDummy : MonoBehaviour, IRepellable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Candy")]
        [SerializeField, Min(0f), Tooltip("How far away the dummy notices landed candy (metres).")]
        private float candyRange = 10f;
        [SerializeField, Min(0f), Tooltip("Slide speed toward claimed candy (m/s).")]
        private float moveSpeed = 2.5f;
        [SerializeField, Min(0f), Tooltip("Seconds spent eating before the candy is consumed.")]
        private float eatSeconds = 4f;
        [SerializeField, Min(0.05f), Tooltip("Seconds between candy searches.")]
        private float searchInterval = 0.25f;

        [Header("Repel")]
        [SerializeField, Min(0f), Tooltip("Seconds the dummy stays white (and ignores candy) after a repel.")]
        private float repelledSeconds = 1.5f;
        [SerializeField, Tooltip("Log each repel to the Console.")]
        private bool logRepels = true;

        [Header("Colours")]
        [SerializeField, Tooltip("Idle colour.")]
        private Color idleColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField, Tooltip("Colour while walking to or eating candy.")]
        private Color candyColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField, Tooltip("Colour while repelled.")]
        private Color repelledColor = Color.white;

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Candy _candy;
        private float _repelTimer;
        private float _eatTimer;
        private float _searchTimer;
        private Color _shownColor;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
            SetColor(idleColor);
        }

        public void Repel(Vector3 sourcePosition)
        {
            if (_repelTimer <= 0f && logRepels)
            {
                Debug.Log($"[TestDummy] Repelled by flare at {sourcePosition}", this);
            }
            _repelTimer = repelledSeconds;
            DropCandy();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_repelTimer > 0f)
            {
                _repelTimer -= dt;
                SetColor(repelledColor);
                return;
            }

            if (_candy == null)
            {
                _eatTimer = 0f;
                SetColor(idleColor);
                _searchTimer -= dt;
                if (_searchTimer > 0f) return;
                _searchTimer = searchInterval;

                Candy found = Candy.FindClosestAvailable(transform.position, candyRange);
                if (found != null && found.Claim(this)) _candy = found;
                return;
            }

            SetColor(candyColor);
            Vector3 target = _candy.transform.position;
            target.y = transform.position.y;
            if ((target - transform.position).sqrMagnitude > 0.5f * 0.5f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * dt);
                return;
            }

            _eatTimer += dt;
            if (_eatTimer >= eatSeconds)
            {
                _candy.Consume();
                _candy = null;
            }
        }

        private void DropCandy()
        {
            if (_candy != null) _candy.ReleaseClaim(this);
            _candy = null;
            _eatTimer = 0f;
        }

        private void OnDisable() => DropCandy();

        private void SetColor(Color color)
        {
            if (_renderer == null || color == _shownColor) return;
            _shownColor = color;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_block);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, candyRange);
        }
    }
}

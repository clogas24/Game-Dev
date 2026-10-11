using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A porch lantern checkpoint. Walking into it lights it, makes it the respawn point and
    /// raises GameEvents.CheckpointReached (first time only). The last checkpoint touched wins.
    /// Setup: a TRIGGER collider on this object, a child Respawn Point where the player should
    /// reappear (keep it clear of the trigger's centre and of walls), and optionally a Light,
    /// a RevealLight and a "lit" visual that are switched on when it lights. The player must be
    /// tagged "Player". GameManager reads Respawn Point and Min Candle On Respawn.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        [Header("Respawn")]
        [SerializeField, Tooltip("Where the player reappears, and which way they face. Empty = this object.")]
        private Transform respawnPoint;
        [SerializeField, Range(0f, 1f), Tooltip("The candle is topped up to at least this fraction on respawn. Overview: 60%.")]
        private float minCandleOnRespawn = 0.6f;
        [SerializeField, Tooltip("Light this checkpoint at scene start (use on the level's first porch).")]
        private bool litAtStart;

        [Header("Lighting up")]
        [SerializeField, Tooltip("Light switched on when the checkpoint is lit. Optional.")]
        private Light lampLight;
        [SerializeField, Tooltip("RevealLight switched on when lit, so the porch reveals spirit platforms. Optional.")]
        private RevealLight revealLight;
        [SerializeField, Tooltip("Object shown only while lit (glow mesh, particles). Optional.")]
        private GameObject litVisual;
        [SerializeField, Tooltip("Prompt shown when the checkpoint is first lit. Empty = none.")]
        private string litPrompt = "Porch lantern lit";
        [SerializeField, Min(0f), Tooltip("Seconds the prompt stays up.")]
        private float litPromptSeconds = 2.5f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when the checkpoint is first lit.")]
        private AudioClip litClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for the lit sound.")]
        private float volume = 1f;

        private bool _lit;

        public bool IsLit => _lit;
        public float MinCandleOnRespawn => minCandleOnRespawn;
        public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;
        public Quaternion RespawnRotation => respawnPoint != null ? respawnPoint.rotation : transform.rotation;

        private void Start()
        {
            ApplyLit(false);
            if (litAtStart) Activate(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) Activate(false);
        }

        /// <summary>Lights the checkpoint (if not already) and makes it the respawn point.</summary>
        public void Activate(bool silent)
        {
            if (GameManager.Instance != null) GameManager.Instance.SetCheckpoint(this);
            if (_lit) return;

            ApplyLit(true);
            if (!silent)
            {
                Sfx.PlayAt(litClip, transform.position, volume);
                if (HUD.Instance != null && !string.IsNullOrEmpty(litPrompt)) HUD.Instance.ShowPrompt(litPrompt, litPromptSeconds);
            }
            GameEvents.RaiseCheckpointReached(transform);
        }

        private void ApplyLit(bool lit)
        {
            _lit = lit;
            if (lampLight != null) lampLight.enabled = lit;
            if (revealLight != null) revealLight.enabled = lit;
            if (litVisual != null) litVisual.SetActive(lit);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _lit ? Color.yellow : new Color(1f, 1f, 0.4f, 0.5f);
            Vector3 p = RespawnPosition;
            Gizmos.DrawWireSphere(p + Vector3.up * 0.9f, 0.3f);
            Gizmos.DrawLine(p + Vector3.up * 0.9f, p + Vector3.up * 0.9f + RespawnRotation * Vector3.forward * 0.8f);
        }
    }
}

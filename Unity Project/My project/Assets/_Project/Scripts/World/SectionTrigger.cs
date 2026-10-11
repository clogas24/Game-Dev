using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Marks the start of a level section. The first time the player walks in it raises
    /// GameEvents.SectionEntered(sectionId), which the Step 4 playtest logger uses to time
    /// sections. Optionally shows the section name on the HUD.
    /// Setup: a TRIGGER collider spanning the whole path into the section, so it cannot be
    /// skipped. Give every section a unique id. The player must be tagged "Player".
    /// </summary>
    public class SectionTrigger : MonoBehaviour
    {
        [SerializeField, Tooltip("Unique id written to the telemetry, e.g. \"2-SpiritBridge\".")]
        private string sectionId = "Section";
        [SerializeField, Tooltip("Name shown on the HUD when the section starts. Empty = show nothing.")]
        private string displayName = "";
        [SerializeField, Min(0f), Tooltip("Seconds the name stays up.")]
        private float displaySeconds = 3f;

        private bool _entered;

        public string SectionId => sectionId;

        private void OnTriggerEnter(Collider other)
        {
            if (_entered || !other.CompareTag("Player")) return;
            _entered = true;
            if (HUD.Instance != null && !string.IsNullOrEmpty(displayName)) HUD.Instance.ShowPrompt(displayName, displaySeconds);
            GameEvents.RaiseSectionEntered(sectionId);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (TryGetComponent(out BoxCollider box)) Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}

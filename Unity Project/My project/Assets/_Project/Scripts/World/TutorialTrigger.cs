using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A one-shot tutorial prompt. The first time the player walks in, the text is shown on the
    /// HUD and GameEvents.TutorialShown(tutorialId) is raised.
    /// The text may use {flare}, {throw} and {jump}; the HUD swaps in the bound key names.
    /// Setup: a TRIGGER collider where the lesson applies. The player must be tagged "Player".
    /// </summary>
    public class TutorialTrigger : MonoBehaviour
    {
        [SerializeField, Tooltip("Unique id written to the telemetry.")]
        private string tutorialId = "Tutorial";
        [SerializeField, TextArea, Tooltip("Prompt text. Tokens: {flare}, {throw}, {jump}.")]
        private string text = "Hold {flare} to flare";
        [SerializeField, Min(0f), Tooltip("Seconds the prompt stays up.")]
        private float seconds = 5f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when the prompt appears.")]
        private AudioClip shownClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for the prompt sound.")]
        private float volume = 0.7f;

        private bool _shown;

        public string TutorialId => tutorialId;

        private void OnTriggerEnter(Collider other)
        {
            if (_shown || !other.CompareTag("Player")) return;
            _shown = true;
            if (HUD.Instance != null) HUD.Instance.ShowPrompt(text, seconds);
            Sfx.Play2D(shownClip, volume);
            GameEvents.RaiseTutorialShown(tutorialId);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.9f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (TryGetComponent(out BoxCollider box)) Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}

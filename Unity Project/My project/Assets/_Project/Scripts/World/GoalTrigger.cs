using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// The farmhouse door. The player wins by reaching it with the candle still lit.
    /// It asks GameManager to win, which raises GameEvents.LevelCompleted and shows the results.
    /// Setup: a TRIGGER collider in front of the door. The player must be tagged "Player".
    /// </summary>
    public class GoalTrigger : MonoBehaviour
    {
        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Played when the player wins here.")]
        private AudioClip reachedClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for the win sound.")]
        private float volume = 1f;

        private bool _reached;

        private void OnTriggerEnter(Collider other)
        {
            if (_reached || !other.CompareTag("Player")) return;

            CandleManager candle = CandleManager.Instance;
            if (candle != null && !candle.IsLit) return;

            if (GameManager.Instance != null)
            {
                if (!GameManager.Instance.Win()) return;
            }
            else
            {
                // No game flow in this scene (a bare test scene): still tell listeners.
                GameEvents.RaiseLevelCompleted();
            }
            _reached = true;
            Sfx.Play2D(reachedClip, volume);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (TryGetComponent(out BoxCollider box)) Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A readable note from the farm family. Optional: it never blocks the path and nothing
    /// depends on it being read.
    /// While the player stands within Read Range the HUD shows "read the note" with the Throw key,
    /// and pressing that key reads the note instead of throwing candy (CandyThrower asks
    /// <see cref="TryInteract"/> first). Reading pauses the candle. NoteReader on the HUD shows
    /// the text and owns the open/close rules.
    /// "In range" is measured from the player every time it is asked for, not tracked with
    /// trigger enter and exit messages: a respawn teleports the player, and a teleport does
    /// not reliably send an exit, which would leave a note "in range" from across the map.
    /// Setup: place it by a signpost and set Read Range (yellow gizmo). It needs no collider.
    /// Give every note a unique id; it is what the playtest log records.
    /// </summary>
    public class StoryNote : MonoBehaviour
    {
        private static readonly List<StoryNote> s_all = new List<StoryNote>(8);

        [SerializeField, Tooltip("Unique id written to the playtest log, e.g. \"Note1_Lanterns\".")]
        private string noteId = "Note";
        [SerializeField, Tooltip("Heading shown above the text.")]
        private string title = "A note";
        [SerializeField, TextArea(4, 12), Tooltip("The note's text.")]
        private string body = "";
        [SerializeField, Min(0.1f), Tooltip("How close the player must stand to read it (metres, measured on the ground).")]
        private float readRange = 1.9f;

        public string NoteId => noteId;
        public string Title => title;
        public string Body => body;

        /// <summary>The note the player is standing at, or null. With two in range, the closer one.</summary>
        public static StoryNote InRange
        {
            get
            {
                CandleManager player = CandleManager.Instance;
                if (player == null) return null;

                Vector3 position = player.transform.position;
                StoryNote best = null;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < s_all.Count; i++)
                {
                    StoryNote note = s_all[i];
                    Vector3 offset = note.transform.position - position;
                    if (Mathf.Abs(offset.y) > 2.5f) continue; // not from a creek bed under the bank
                    float sqr = offset.x * offset.x + offset.z * offset.z;
                    if (sqr > note.readRange * note.readRange || sqr >= bestSqr) continue;
                    bestSqr = sqr;
                    best = note;
                }
                return best;
            }
        }

        /// <summary>
        /// Called by CandyThrower when the Throw key is pressed. Returns true if the press was
        /// used to open or close a note, in which case no candy is thrown.
        /// </summary>
        public static bool TryInteract()
        {
            return NoteReader.Instance != null && NoteReader.Instance.HandleInteract();
        }

        private void OnEnable() => s_all.Add(this);

        private void OnDisable() => s_all.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.5f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, readRange);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_all.Clear();
    }
}

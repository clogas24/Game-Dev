using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// A light that reveals spirit platforms. Every enabled RevealLight registers itself in a
    /// static list, and <see cref="IsRevealed"/> answers "is this point lit?" for SpiritPlatform.
    /// Setup: add to the lantern and to each porch lamp, set Radius, and optionally drag in the
    /// Unity Light on the same object so its range follows the radius. LanternController drives
    /// Radius and Overexposing on the player's lantern; porch lamps just keep their values.
    /// </summary>
    public class RevealLight : MonoBehaviour
    {
        private static readonly List<RevealLight> s_active = new List<RevealLight>(16);

        [SerializeField, Min(0f), Tooltip("Reveal radius in metres. Spirit platforms sampled inside this sphere count as lit.")]
        private float radius = 6f;

        [SerializeField, Tooltip("Optional Unity Light whose range follows the radius.")]
        private Light linkedLight;

        [SerializeField, Min(0f), Tooltip("Linked light range = radius x this. Above 1 because a point light is already very dim at the edge of its range.")]
        private float lightRangeMultiplier = 1.5f;

        [SerializeField, Tooltip("If true this light hides platforms instead of revealing them (the flare). An overexposing light beats any normal light.")]
        private bool overexposing;

        /// <summary>Reveal radius in metres. Also resizes the linked light.</summary>
        public float Radius
        {
            get => radius;
            set
            {
                radius = Mathf.Max(0f, value);
                SyncLinkedLight();
            }
        }

        /// <summary>True while this light erases platforms instead of revealing them.</summary>
        public bool Overexposing
        {
            get => overexposing;
            set => overexposing = value;
        }

        /// <summary>
        /// True if the point is inside at least one normal reveal light AND not inside any
        /// overexposing one.
        /// </summary>
        public static bool IsRevealed(Vector3 point)
        {
            bool lit = false;
            for (int i = 0; i < s_active.Count; i++)
            {
                RevealLight light = s_active[i];
                if (light.radius <= 0f) continue;
                if ((light.transform.position - point).sqrMagnitude > light.radius * light.radius) continue;
                if (light.overexposing) return false;
                lit = true;
            }
            return lit;
        }

        private void OnEnable()
        {
            s_active.Add(this);
            SyncLinkedLight();
        }

        private void OnDisable() => s_active.Remove(this);

        private void OnValidate() => SyncLinkedLight();

        private void SyncLinkedLight()
        {
            if (linkedLight != null) linkedLight.range = radius * lightRangeMultiplier;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = overexposing ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();
    }
}

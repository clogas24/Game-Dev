using UnityEngine;

namespace GrowthSpurt
{
    /// <summary>
    /// Everything that changes with the player's size. One asset per size (SP_Small, SP_Normal, SP_Big).
    /// All distances are in world metres at that size, so they can be tuned directly.
    /// </summary>
    [CreateAssetMenu(fileName = "SP_New", menuName = "Growth Spurt/Size Profile", order = 0)]
    public class SizeProfile : ScriptableObject
    {
        [Tooltip("Which size this profile describes.")]
        public PlayerSize size = PlayerSize.Normal;

        [Header("Body")]
        [Tooltip("Uniform transform scale. 1 = the Starter Assets character (about 1.8 m tall).")]
        [Min(0.05f)] public float scale = 1f;

        [Header("Movement (ThirdPersonController)")]
        [Min(0f)] public float moveSpeed = 2f;
        [Min(0f)] public float sprintSpeed = 5.335f;
        [Tooltip("Jump apex height in metres. Independent of gravity (ThirdPersonController solves for it).")]
        [Min(0f)] public float jumpHeight = 1.2f;
        [Tooltip("Multiplies the controller's base gravity (-15). Higher = falls faster, lands harder.")]
        [Min(0.01f)] public float gravityMultiplier = 1f;

        [Header("Physics")]
        [Tooltip("Weight for pressure plates etc. Small = 1, Normal = 2, Big = 3.")]
        public int weight = 2;
        [Tooltip("Heaviest Rigidbody mass (kg) this size can push. Heavier bodies don't move.")]
        [Min(0f)] public float pushStrength = 30f;
        [Tooltip("Highest step (metres) the CharacterController walks up without jumping.")]
        [Min(0f)] public float stepOffset = 0.25f;

        [Header("Camera (CinemachineThirdPersonFollow)")]
        [Min(0.1f)] public float cameraDistance = 4f;
        public Vector3 cameraShoulderOffset = new Vector3(1f, 0f, 0f);

        [Header("Animation")]
        [Tooltip("Animator.speed at this size. 1 / scale keeps the feet planted (no sliding): " +
                 "the Starter Assets walk/run clips are authored for a 1.8 m body, so a body S times bigger " +
                 "covers S times the ground per cycle and must play 1/S as fast.")]
        [Min(0.01f)] public float animationSpeedMultiplier = 1f;

        /// <summary>The animator speed that exactly cancels foot sliding at this scale.</summary>
        public float NoSlideAnimationSpeed => 1f / Mathf.Max(0.05f, scale);
    }
}

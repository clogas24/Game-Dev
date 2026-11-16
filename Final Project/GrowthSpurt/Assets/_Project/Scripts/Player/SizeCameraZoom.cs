using Unity.Cinemachine;
using UnityEngine;

namespace GrowthSpurt
{
    /// <summary>
    /// Eases the Cinemachine 3 third-person follow distance and shoulder offset toward the player's current
    /// SizeProfile, so Big pulls the camera out and Small brings it in close. Goes on the CinemachineCamera.
    /// (The follow target, PlayerCameraRoot, is a child of the player, so its height already scales with the body.)
    /// </summary>
    [DisallowMultipleComponent]
    public class SizeCameraZoom : MonoBehaviour
    {
        [SerializeField] private SizeShifter shifter;
        [SerializeField] private CinemachineThirdPersonFollow follow;
        [Tooltip("Seconds to settle at the new distance. A bit longer than the 0.25 s body transition so the camera trails it.")]
        [SerializeField, Min(0.01f)] private float smoothTime = 0.35f;

        private float _distanceVelocity;
        private Vector3 _shoulderVelocity;

        private void Awake()
        {
            if (follow == null) follow = GetComponent<CinemachineThirdPersonFollow>();
            if (shifter == null) shifter = FindAnyObjectByType<SizeShifter>();
        }

        private void Start() => Snap();

        /// <summary>Jump straight to the current profile's framing (e.g. after a respawn).</summary>
        public void Snap()
        {
            if (follow == null || shifter == null || shifter.CurrentProfile == null) return;
            follow.CameraDistance = shifter.CurrentProfile.cameraDistance;
            follow.ShoulderOffset = shifter.CurrentProfile.cameraShoulderOffset;
            _distanceVelocity = 0f;
            _shoulderVelocity = Vector3.zero;
        }

        // Update (not LateUpdate) so the values are in place before CinemachineBrain positions the camera.
        private void Update()
        {
            if (follow == null || shifter == null) return;
            SizeProfile p = shifter.CurrentProfile;
            if (p == null) return;

            follow.CameraDistance = Mathf.SmoothDamp(follow.CameraDistance, p.cameraDistance, ref _distanceVelocity, smoothTime);
            follow.ShoulderOffset = Vector3.SmoothDamp(follow.ShoulderOffset, p.cameraShoulderOffset, ref _shoulderVelocity, smoothTime);
        }
    }
}

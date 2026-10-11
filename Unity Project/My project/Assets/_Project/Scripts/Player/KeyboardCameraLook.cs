using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
    /// <summary>
    /// Lets the arrow keys turn the camera, so the game never needs a trackpad (overview,
    /// mechanic 1), and applies one saved camera sensitivity to both mouse and keys.
    /// Starter Assets treats look input as a per-frame mouse delta, so the key input is scaled
    /// by deltaTime here to keep turning speed independent of frame rate. Each frame this
    /// writes the combined mouse + key look into StarterAssetsInputs.look, which
    /// ThirdPersonController reads in LateUpdate.
    /// Setup: on the Player root next to PlayerInput and StarterAssetsInputs. The actions asset
    /// needs "Look" (pointer delta) and "LookKeys" (arrow-key 2D vector).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public class KeyboardCameraLook : MonoBehaviour
    {
        public const string SensitivityPrefKey = "LK_CameraSensitivity";

        [Header("Actions")]
        [SerializeField, Tooltip("Name of the pointer-delta look action.")]
        private string lookActionName = "Look";
        [SerializeField, Tooltip("Name of the arrow-key look action (2D vector).")]
        private string lookKeysActionName = "LookKeys";

        [Header("Arrow keys")]
        [SerializeField, Min(0f), Tooltip("Camera yaw speed while Left/Right is held (degrees per second).")]
        private float yawSpeed = 130f;
        [SerializeField, Min(0f), Tooltip("Camera pitch speed while Up/Down is held (degrees per second).")]
        private float pitchSpeed = 80f;
        [SerializeField, Tooltip("If true, Up arrow tilts the camera down instead of up.")]
        private bool invertPitch;

        [Header("Sensitivity")]
        [SerializeField, Range(0.1f, 3f), Tooltip("Default camera sensitivity (mouse and keys) until the player changes it. The saved value lives in PlayerPrefs.")]
        private float defaultSensitivity = 1f;

        private StarterAssetsInputs _inputs;
        private InputAction _lookAction;
        private InputAction _lookKeysAction;
        private float _sensitivity;

        /// <summary>Multiplier applied to mouse and arrow-key look. Saved to PlayerPrefs when set.</summary>
        public float Sensitivity
        {
            get => _sensitivity;
            set
            {
                _sensitivity = Mathf.Clamp(value, 0.1f, 3f);
                PlayerPrefs.SetFloat(SensitivityPrefKey, _sensitivity);
            }
        }

        private void Awake()
        {
            _inputs = GetComponent<StarterAssetsInputs>();
            _sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityPrefKey, defaultSensitivity), 0.1f, 3f);
        }

        private void Start()
        {
            if (TryGetComponent(out PlayerInput playerInput) && playerInput.actions != null)
            {
                _lookAction = playerInput.actions.FindAction(lookActionName);
                _lookKeysAction = playerInput.actions.FindAction(lookKeysActionName);
            }
            if (_lookKeysAction == null)
            {
                Debug.LogWarning($"[KeyboardCameraLook] No '{lookKeysActionName}' action found on PlayerInput; arrow keys will not turn the camera.", this);
            }
        }

        private void Update()
        {
            if (_inputs == null) return;

            Vector2 look = Vector2.zero;
            if (_lookAction != null && _inputs.cursorInputForLook) look = _lookAction.ReadValue<Vector2>();

            if (_lookKeysAction != null)
            {
                Vector2 keys = _lookKeysAction.ReadValue<Vector2>();
                float dt = Time.deltaTime;
                look.x += keys.x * yawSpeed * dt;
                // ThirdPersonController adds look.y to the pitch, and positive pitch looks down.
                look.y += keys.y * pitchSpeed * dt * (invertPitch ? 1f : -1f);
            }

            _inputs.look = look * _sensitivity;
        }
    }
}

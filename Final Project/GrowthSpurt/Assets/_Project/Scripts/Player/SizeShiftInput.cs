using UnityEngine;
using UnityEngine.InputSystem;

namespace GrowthSpurt
{
    /// <summary>
    /// Receives the Grow / Shrink actions from the player's PlayerInput (Behavior = Send Messages).
    /// PlayerInput sends "On" + action name to every component on its GameObject, so this works next to
    /// StarterAssetsInputs without changing it. Buttons only send on "performed", i.e. once per press.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SizeShifter))]
    public class SizeShiftInput : MonoBehaviour
    {
        private SizeShifter _shifter;

        private void Awake() => _shifter = GetComponent<SizeShifter>();

        // Called by PlayerInput (Send Messages) for the "Grow" action: E / gamepad right shoulder.
        public void OnGrow(InputValue value)
        {
            if (value.isPressed) _shifter.TryGrow();
        }

        // Called by PlayerInput (Send Messages) for the "Shrink" action: Q / gamepad left shoulder.
        public void OnShrink(InputValue value)
        {
            if (value.isPressed) _shifter.TryShrink();
        }
    }
}

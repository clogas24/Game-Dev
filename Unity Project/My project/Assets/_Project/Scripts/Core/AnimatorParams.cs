using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Helper for "set this Animator parameter only if it exists", so scripts can drive the
    /// Starter Assets controller today and pick up Flaring / Extinguished / Throw / State
    /// automatically once those parameters are added in Step 5. Call once in Awake/Start and
    /// cache the result; Animator.parameters allocates.
    /// </summary>
    public static class AnimatorParams
    {
        public static bool Has(Animator animator, int nameHash, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == nameHash && parameters[i].type == type) return true;
            }
            return false;
        }
    }
}

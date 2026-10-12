using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Fades a UI panel in each time it is switched on (the win screen). Uses unscaled time,
    /// because the game is paused (time scale 0) while menus are up.
    /// Setup: on the panel, with a CanvasGroup.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiFadeIn : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("Seconds the fade takes.")]
        private float seconds = 0.9f;
        [SerializeField, Min(0f), Tooltip("Seconds to wait before the fade starts.")]
        private float delay;

        private CanvasGroup _group;
        private float _time;

        private void OnEnable()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _time = -delay;
            _group.alpha = 0f;
        }

        private void Update()
        {
            if (_group.alpha >= 1f) return;
            _time += Time.unscaledDeltaTime;
            _group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_time / seconds));
        }
    }
}

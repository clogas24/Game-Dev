using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
    /// <summary>
    /// Menu sounds: a hover tick when a button or slider is pointed at or reached with the
    /// keyboard, a click when it is pressed, and a sound when the game leaves the title screen.
    /// Setup: on the GameMenus canvas. Assign the clips. It finds every Selectable under itself
    /// at Start, so buttons added to the menus later are covered without wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiSounds : MonoBehaviour
    {
        [SerializeField, Tooltip("Played when a control is hovered or selected with the keyboard.")]
        private AudioClip hoverClip;
        [SerializeField, Tooltip("Played when a button is pressed.")]
        private AudioClip clickClip;
        [SerializeField, Tooltip("Played when the title screen is dismissed.")]
        private AudioClip startClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for menu sounds.")]
        private float volume = 0.7f;

        private GameManager _game;

        private void Start()
        {
            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
            {
                if (selectable.GetComponent<UiSoundTarget>() != null) continue;
                selectable.gameObject.AddComponent<UiSoundTarget>().Owner = this;
            }

            _game = GameManager.Instance;
            if (_game != null) _game.OnModeChanged += HandleModeChanged;
        }

        private void OnDestroy()
        {
            if (_game != null) _game.OnModeChanged -= HandleModeChanged;
        }

        private void HandleModeChanged(GameMode previous, GameMode next)
        {
            if (previous == GameMode.Title && next == GameMode.Playing) Sfx.Play2D(startClip, volume);
        }

        public void PlayHover() => Sfx.Play2D(hoverClip, volume * 0.6f);
        public void PlayClick() => Sfx.Play2D(clickClip, volume);
    }
}

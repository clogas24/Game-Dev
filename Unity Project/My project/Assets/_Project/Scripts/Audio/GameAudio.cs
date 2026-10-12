using UnityEngine;
using UnityEngine.Audio;

namespace LanternKeeper
{
    /// <summary>
    /// The player's volume settings: Music and SFX, each 0..1, saved in PlayerPrefs and applied
    /// to the AudioMixer's exposed parameters. The SFX setting also drives the Ambience group,
    /// so the pause menu needs only two sliders.
    /// Nothing to set up: MusicDirector calls <see cref="Bind"/> with the mixer in Start.
    /// </summary>
    public static class GameAudio
    {
        public const string MusicPrefKey = "LK_MusicVolume";
        public const string SfxPrefKey = "LK_SfxVolume";
        public const string MusicParameter = "MusicVolume";
        public const string SfxParameter = "SfxVolume";
        public const string AmbienceParameter = "AmbienceVolume";
        public const float DefaultMusicVolume = 0.7f;
        public const float DefaultSfxVolume = 0.9f;

        private static AudioMixer s_mixer;

        /// <summary>Music volume, 0..1. Saved to PlayerPrefs when set.</summary>
        public static float MusicVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicPrefKey, DefaultMusicVolume));
            set
            {
                PlayerPrefs.SetFloat(MusicPrefKey, Mathf.Clamp01(value));
                Apply();
            }
        }

        /// <summary>Sound effect and ambience volume, 0..1. Saved to PlayerPrefs when set.</summary>
        public static float SfxVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxPrefKey, DefaultSfxVolume));
            set
            {
                PlayerPrefs.SetFloat(SfxPrefKey, Mathf.Clamp01(value));
                Apply();
            }
        }

        /// <summary>
        /// Remembers the mixer and applies the saved volumes. Call from Start, not Awake: a
        /// mixer ignores SetFloat until the first frame's audio set-up has run.
        /// </summary>
        public static void Bind(AudioMixer mixer)
        {
            s_mixer = mixer;
            Apply();
        }

        private static void Apply()
        {
            if (s_mixer == null) return;
            s_mixer.SetFloat(MusicParameter, ToDecibels(MusicVolume));
            float sfx = ToDecibels(SfxVolume);
            s_mixer.SetFloat(SfxParameter, sfx);
            s_mixer.SetFloat(AmbienceParameter, sfx);
        }

        // A slider is linear; loudness is not. 1 -> 0 dB, 0.5 -> -6 dB, 0 -> silent.
        private static float ToDecibels(float volume) => volume <= 0.0001f ? -80f : Mathf.Log10(volume) * 20f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_mixer = null;
    }
}

using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Null-safe one-shot audio helpers. Every system calls these with its AudioClip fields, so
    /// the game runs silently until clips are assigned in Step 5. Nothing to set up: the 2D
    /// source is created on first use and survives scene loads.
    /// </summary>
    public static class Sfx
    {
        private static AudioSource s_source2D;

        /// <summary>Plays a 3D one-shot at a world position. Does nothing if the clip is null.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        /// <summary>Plays a non-positional one-shot (UI, pickups). Does nothing if the clip is null.</summary>
        public static void Play2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (s_source2D == null)
            {
                var go = new GameObject("Sfx2D");
                Object.DontDestroyOnLoad(go);
                s_source2D = go.AddComponent<AudioSource>();
                s_source2D.playOnAwake = false;
                s_source2D.spatialBlend = 0f;
            }
            s_source2D.PlayOneShot(clip, volume);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_source2D = null;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace LanternKeeper
{
    /// <summary>
    /// Null-safe one-shot audio helpers. Every system calls these with its AudioClip fields, so
    /// a missing clip is simply silent. Nothing to set up: the sources are created on first use
    /// and survive scene loads.
    /// World sounds play from a small pool of 3D sources with a linear rolloff, so a sound is
    /// silent beyond its max distance (a ghost across the map is not heard). Everything is
    /// routed to <see cref="OutputGroup"/>, which MusicDirector sets to the mixer's SFX group;
    /// without one the sounds go straight to the listener, as before Step 5.
    /// </summary>
    public static class Sfx
    {
        /// <summary>Full volume inside this distance (metres).</summary>
        public const float DefaultMinDistance = 2.5f;
        /// <summary>Silent beyond this distance (metres).</summary>
        public const float DefaultMaxDistance = 26f;

        private const int PoolSize = 16;
        // The same clip is not started twice within this time: a bridge of platforms appearing
        // together would otherwise stack into one very loud chime.
        private const float RepeatGuardSeconds = 0.06f;

        private static readonly List<AudioSource> s_pool = new List<AudioSource>(PoolSize);
        private static readonly Dictionary<AudioClip, float> s_lastPlayed = new Dictionary<AudioClip, float>(32);
        private static AudioSource s_source2D;
        private static AudioMixerGroup s_outputGroup;
        private static int s_next;

        /// <summary>Mixer group every one-shot is routed to. Null = no mixer.</summary>
        public static AudioMixerGroup OutputGroup
        {
            get => s_outputGroup;
            set
            {
                s_outputGroup = value;
                if (s_source2D != null) s_source2D.outputAudioMixerGroup = value;
                for (int i = 0; i < s_pool.Count; i++)
                {
                    if (s_pool[i] != null) s_pool[i].outputAudioMixerGroup = value;
                }
            }
        }

        /// <summary>Plays a 3D one-shot at a world position. Does nothing if the clip is null.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f,
            float minDistance = DefaultMinDistance, float maxDistance = DefaultMaxDistance)
        {
            if (clip == null || IsRepeat(clip)) return;

            AudioSource source = NextPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
            source.Play();
        }

        /// <summary>Plays a non-positional one-shot (UI, pickups). Does nothing if the clip is null.</summary>
        public static void Play2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null || IsRepeat(clip)) return;
            if (s_source2D == null)
            {
                var go = new GameObject("Sfx2D");
                Object.DontDestroyOnLoad(go);
                s_source2D = go.AddComponent<AudioSource>();
                s_source2D.playOnAwake = false;
                s_source2D.spatialBlend = 0f;
                s_source2D.outputAudioMixerGroup = s_outputGroup;
            }
            s_source2D.PlayOneShot(clip, volume);
        }

        private static bool IsRepeat(AudioClip clip)
        {
            float now = Time.unscaledTime;
            if (s_lastPlayed.TryGetValue(clip, out float last) && now - last < RepeatGuardSeconds) return true;
            s_lastPlayed[clip] = now;
            return false;
        }

        // A free source if there is one, otherwise the next in line (the oldest sound is cut off).
        private static AudioSource NextPooledSource()
        {
            for (int i = 0; i < s_pool.Count; i++)
            {
                if (s_pool[i] != null && !s_pool[i].isPlaying) return s_pool[i];
            }
            if (s_pool.Count < PoolSize)
            {
                var go = new GameObject("Sfx3D " + s_pool.Count);
                Object.DontDestroyOnLoad(go);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                source.outputAudioMixerGroup = s_outputGroup;
                s_pool.Add(source);
                return source;
            }
            s_next = (s_next + 1) % s_pool.Count;
            if (s_pool[s_next] == null) s_pool[s_next] = s_pool[0];
            return s_pool[s_next];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_source2D = null;
            s_outputGroup = null;
            s_pool.Clear();
            s_lastPlayed.Clear();
            s_next = 0;
        }
    }
}

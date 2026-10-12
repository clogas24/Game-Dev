using UnityEngine;
using UnityEngine.Audio;

namespace LanternKeeper
{
    /// <summary>
    /// Music and level-wide ambience. Plays the title loop on the title screen, then the calm
    /// exploration loop; while GameManager.IsEvasion is true (a ghost is in Notice or Chase) it
    /// crossfades to the chase layer and back. The exploration and chase loops are the same
    /// length and start on the same audio clock tick, so they stay in step and the crossfade
    /// never lands off the beat. A night ambience loop runs under everything.
    /// It also hands the mixer's SFX group to <see cref="Sfx"/> and applies the saved volumes.
    /// Setup: one per scene. Assign the mixer, its three groups and the clips; any clip may be
    /// left empty. It finds GameManager.Instance by itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicDirector : MonoBehaviour
    {
        [Header("Mixer")]
        [SerializeField, Tooltip("The project's AudioMixer (exposed parameters MusicVolume, SfxVolume, AmbienceVolume).")]
        private AudioMixer mixer;
        [SerializeField, Tooltip("Group the music sources play through.")]
        private AudioMixerGroup musicGroup;
        [SerializeField, Tooltip("Group every one-shot sound effect plays through.")]
        private AudioMixerGroup sfxGroup;
        [SerializeField, Tooltip("Group the ambience loop plays through.")]
        private AudioMixerGroup ambienceGroup;

        [Header("Clips (any may be empty)")]
        [SerializeField, Tooltip("Loop for the title screen.")]
        private AudioClip titleMusic;
        [SerializeField, Tooltip("Calm loop while exploring.")]
        private AudioClip exploreMusic;
        [SerializeField, Tooltip("Tense layer while a ghost has noticed or is chasing the player. Same length as the exploration loop.")]
        private AudioClip chaseMusic;
        [SerializeField, Tooltip("Level-wide night ambience (wind and crickets).")]
        private AudioClip ambienceLoop;

        [Header("Levels")]
        [SerializeField, Range(0f, 1f), Tooltip("Title loop volume.")]
        private float titleVolume = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("Exploration loop volume while nothing is chasing.")]
        private float exploreVolume = 0.75f;
        [SerializeField, Range(0f, 1f), Tooltip("Exploration loop volume left under the chase layer.")]
        private float exploreUnderChase = 0.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Chase layer volume during evasion.")]
        private float chaseVolume = 0.9f;
        [SerializeField, Range(0f, 1f), Tooltip("Ambience loop volume.")]
        private float ambienceVolume = 0.55f;
        [SerializeField, Range(0f, 1f), Tooltip("Music is scaled by this while paused, extinguished or on the win screen.")]
        private float quietScale = 0.35f;

        [Header("Timing")]
        [SerializeField, Min(0.05f), Tooltip("Seconds the exploration/chase crossfade takes, each way.")]
        private float crossfadeSeconds = 1.5f;
        [SerializeField, Min(0.05f), Tooltip("Seconds other volume changes take (title to game, pause).")]
        private float fadeSeconds = 1f;

        private AudioSource _title;
        private AudioSource _explore;
        private AudioSource _chase;
        private AudioSource _ambience;
        private GameManager _game;
        private bool _evasion;

        /// <summary>True while the chase layer is the one being faded in.</summary>
        public bool ChaseActive => _evasion;

        private void Awake()
        {
            _title = NewSource("Music Title", titleMusic, musicGroup);
            _explore = NewSource("Music Explore", exploreMusic, musicGroup);
            _chase = NewSource("Music Chase", chaseMusic, musicGroup);
            _ambience = NewSource("Ambience Night", ambienceLoop, ambienceGroup);
            Sfx.OutputGroup = sfxGroup;
        }

        private void Start()
        {
            GameAudio.Bind(mixer);

            _game = GameManager.Instance;
            if (_game != null)
            {
                _game.OnEvasionChanged += HandleEvasionChanged;
                _evasion = _game.IsEvasion;
            }

            // Both level loops start on the same tick of the audio clock, a moment from now.
            double startAt = AudioSettings.dspTime + 0.1;
            if (_explore.clip != null) _explore.PlayScheduled(startAt);
            if (_chase.clip != null) _chase.PlayScheduled(startAt);
            if (_ambience.clip != null) _ambience.Play();
            if (_title.clip != null && _game != null && _game.Mode == GameMode.Title)
            {
                _title.volume = titleVolume;
                _title.Play();
            }
        }

        private void OnDestroy()
        {
            if (_game != null) _game.OnEvasionChanged -= HandleEvasionChanged;
        }

        private void HandleEvasionChanged(bool evasion) => _evasion = evasion;

        private void Update()
        {
            GameMode mode = _game != null ? _game.Mode : GameMode.Playing;
            bool title = mode == GameMode.Title;
            float scale = mode == GameMode.Playing || title ? 1f : quietScale;
            bool chasing = _evasion && mode == GameMode.Playing;

            // Each step is the layer's own range divided by the fade time, so the exploration
            // loop dipping and the chase layer rising both take crossfadeSeconds.
            float dt = Time.unscaledDeltaTime;
            float crossfade = dt / crossfadeSeconds;
            float fade = dt / fadeSeconds;

            Approach(_title, title ? titleVolume : 0f, titleVolume * fade);
            Approach(_explore, title ? 0f : (chasing ? exploreUnderChase : exploreVolume) * scale,
                title || scale < 1f ? exploreVolume * fade : Mathf.Abs(exploreVolume - exploreUnderChase) * crossfade);
            Approach(_chase, chasing ? chaseVolume : 0f, chaseVolume * crossfade);
            Approach(_ambience, ambienceVolume * (title ? 0.6f : 1f), ambienceVolume * fade);

            // The title loop is not needed again once it has faded out (Restart skips the title).
            if (!title && _title.isPlaying && _title.volume <= 0f) _title.Stop();
        }

        private static void Approach(AudioSource source, float target, float step)
        {
            if (source.clip == null) return;
            source.volume = Mathf.MoveTowards(source.volume, target, Mathf.Max(step, 0.0001f));
        }

        private AudioSource NewSource(string sourceName, AudioClip clip, AudioMixerGroup group)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = group;
            // Music keeps playing on the title, pause and win screens (time scale 0).
            source.ignoreListenerPause = true;
            return source;
        }
    }
}

using UnityEngine;
using UnityEngine.Audio;

namespace LanternKeeper
{
    /// <summary>
    /// The player's own looping and footstep sounds, played through the mixer.
    /// Footsteps: the Starter Assets walk, run and land clips raise the animation events
    /// OnFootstep and OnLand on every script on the Animator's object, so this component
    /// receives them alongside ThirdPersonController. ThirdPersonController plays its own
    /// clips with AudioSource.PlayClipAtPoint, which bypasses the mixer (the SFX slider would
    /// not reach them), so the Player prefab sets its Footstep Audio Volume to 0 and the steps
    /// are played here instead.
    /// Flare: a loop that fades in while LanternController.IsFlaring and out when it stops.
    /// Setup: on the Player root, next to the Animator and LanternController.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSounds : MonoBehaviour
    {
        [Header("Footsteps")]
        [SerializeField, Tooltip("One is picked at random for each step.")]
        private AudioClip[] footstepClips;
        [SerializeField, Tooltip("Played when the player lands from a jump or a fall.")]
        private AudioClip landClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume for steps and landings.")]
        private float footstepVolume = 0.45f;

        [Header("Flare loop")]
        [SerializeField, Tooltip("Looping roar while the flare is held.")]
        private AudioClip flareLoopClip;
        [SerializeField, Range(0f, 1f), Tooltip("Volume of the flare loop at full strength.")]
        private float flareLoopVolume = 0.5f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the loop takes to fade in or out.")]
        private float flareFadeSeconds = 0.2f;
        [SerializeField, Tooltip("Mixer group for the flare loop (SFX).")]
        private AudioMixerGroup outputGroup;

        private LanternController _lantern;
        private AudioSource _flareLoop;
        private int _lastStep = -1;

        private void Awake()
        {
            _lantern = GetComponent<LanternController>();
            if (flareLoopClip == null) return;

            var go = new GameObject("FlareLoop");
            go.transform.SetParent(transform, false);
            _flareLoop = go.AddComponent<AudioSource>();
            _flareLoop.clip = flareLoopClip;
            _flareLoop.loop = true;
            _flareLoop.playOnAwake = false;
            _flareLoop.spatialBlend = 0f;
            _flareLoop.volume = 0f;
            _flareLoop.outputAudioMixerGroup = outputGroup;
        }

        private void Update()
        {
            if (_flareLoop == null) return;
            bool flaring = _lantern != null && _lantern.IsFlaring;
            float target = flaring ? flareLoopVolume : 0f;
            _flareLoop.volume = Mathf.MoveTowards(_flareLoop.volume, target, flareLoopVolume * Time.deltaTime / flareFadeSeconds);
            if (flaring && !_flareLoop.isPlaying) _flareLoop.Play();
            else if (!flaring && _flareLoop.isPlaying && _flareLoop.volume <= 0f) _flareLoop.Stop();
        }

        // Animation event (Starter Assets walk and run clips).
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight <= 0.5f) return;
            if (footstepClips == null || footstepClips.Length == 0) return;

            // Never the same clip twice running.
            int index = Random.Range(0, footstepClips.Length);
            if (index == _lastStep) index = (index + 1) % footstepClips.Length;
            _lastStep = index;
            Sfx.PlayAt(footstepClips[index], transform.position, footstepVolume, 1.5f, 14f);
        }

        // Animation event (Starter Assets landing clip).
        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight <= 0.5f) return;
            Sfx.PlayAt(landClip, transform.position, footstepVolume, 1.5f, 14f);
        }
    }
}

using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// The ghost's cosmetic side: a soft emissive glow in the state tint, a white burst when it
    /// is repelled, candy crumbs while it eats, and a safety net for the Animator.
    /// GhostAI stays the only thing that decides behaviour; this reads its state and never
    /// writes to it. GhostAI already drives the Animator's Speed and State parameters and tints
    /// the base colour; the glow here copies that tint from the ghost's light into the
    /// material's emission, so the ghost reads in the dark without a second colour table.
    /// Setup: on the Ghost prefab root next to GhostAI. Assign the Animator on the Visual
    /// child, the body renderer, the Glow light and the two particle systems.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GhostAI))]
    public class GhostVisualFx : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        // Animator state names, indexed by GhostState. docs/ANIMATION.md lists them.
        private static readonly int[] StateHashes =
        {
            Animator.StringToHash("Drift"), Animator.StringToHash("Notice"), Animator.StringToHash("Chase"),
            Animator.StringToHash("Repelled"), Animator.StringToHash("Distracted"), Animator.StringToHash("Return")
        };

        [Header("References")]
        [SerializeField, Tooltip("The Animator on the Visual child.")]
        private Animator animator;
        [SerializeField, Tooltip("Renderers that glow in the state tint.")]
        private Renderer[] glowRenderers;
        [SerializeField, Tooltip("The ghost's Glow light; GhostAI sets its colour to the state tint.")]
        private Light glowLight;
        [SerializeField, Tooltip("One-shot white burst played on entering Repelled.")]
        private ParticleSystem repelBurst;
        [SerializeField, Tooltip("Crumbs that fall while the ghost is eating candy.")]
        private ParticleSystem crumbs;

        [Header("Glow")]
        [SerializeField, Min(0f), Tooltip("Emission strength, as a multiple of the tint colour.")]
        private float glowStrength = 0.55f;
        [SerializeField, Min(0f), Tooltip("Extra emission added during Chase and Notice, so an alert ghost is brighter.")]
        private float alertBoost = 0.35f;

        [Header("Animator safety net")]
        [SerializeField, Min(0.05f), Tooltip("If the Animator sits in the wrong state for this long it is cross-faded to the right one.")]
        private float resyncAfter = 0.4f;

        private GhostAI _ghost;
        private MaterialPropertyBlock _block;
        private Color _appliedEmission = new Color(-1f, -1f, -1f);
        private float _mismatchTime;
        private bool _crumbsOn;

        private void Awake()
        {
            _ghost = GetComponent<GhostAI>();
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _ghost.OnStateChanged += HandleStateChanged;
            SetCrumbs(false);
        }

        private void OnDisable() => _ghost.OnStateChanged -= HandleStateChanged;

        private void HandleStateChanged(GhostAI ghost, GhostState previous, GhostState next)
        {
            if (next == GhostState.Repelled && repelBurst != null) repelBurst.Play(true);
        }

        private void LateUpdate()
        {
            GhostState state = _ghost.State;

            bool eating = _ghost.IsEating;
            if (eating != _crumbsOn) SetCrumbs(eating);

            if (glowLight != null && glowRenderers != null)
            {
                bool alert = state == GhostState.Notice || state == GhostState.Chase;
                Color emission = glowLight.color * (glowStrength + (alert ? alertBoost : 0f));
                Color delta = emission - _appliedEmission;
                if (Mathf.Abs(delta.r) + Mathf.Abs(delta.g) + Mathf.Abs(delta.b) > 0.004f)
                {
                    _appliedEmission = emission;
                    for (int i = 0; i < glowRenderers.Length; i++)
                    {
                        Renderer r = glowRenderers[i];
                        if (r == null) continue;
                        r.GetPropertyBlock(_block);
                        _block.SetColor(EmissionColorId, emission);
                        r.SetPropertyBlock(_block);
                    }
                }
            }

            ResyncAnimator(state);
        }

        // The controller only has the transitions of docs/FSM.md. If two FSM changes land in
        // one frame the Animator can miss the first and be left without a transition to take,
        // so a mismatch that outlasts every normal blend is corrected here.
        private void ResyncAnimator(GhostState state)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            int expected = StateHashes[(int)state];
            bool matches = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == expected
                           || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == expected);
            if (matches)
            {
                _mismatchTime = 0f;
                return;
            }
            _mismatchTime += Time.deltaTime;
            if (_mismatchTime < resyncAfter) return;
            _mismatchTime = 0f;
            animator.CrossFadeInFixedTime(expected, 0.12f, 0);
        }

        private void SetCrumbs(bool on)
        {
            _crumbsOn = on;
            if (crumbs == null) return;
            if (on) crumbs.Play(true);
            else crumbs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}

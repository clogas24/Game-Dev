using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// The rune carved into a pressure plate: dark while the plate is up, glowing while it is
    /// pressed. Cosmetic only; it listens to PressurePlate and changes nothing on it.
    /// Setup: on the PressurePlate prefab root. Assign the rune's renderer (an emissive
    /// material; the glow is written through a MaterialPropertyBlock).
    /// </summary>
    [RequireComponent(typeof(PressurePlate))]
    public class PlateRune : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Tooltip("Renderer of the rune on the pad.")]
        private Renderer rune;
        [SerializeField, Tooltip("Rune colour while the plate is up (carved, unlit).")]
        private Color idleColor = new Color(0.16f, 0.14f, 0.12f);
        [SerializeField, ColorUsage(false, true), Tooltip("Rune glow while the plate is pressed.")]
        private Color glowColor = new Color(1.6f, 3.2f, 1.4f);
        [SerializeField, Min(0.01f), Tooltip("Seconds the glow takes to fade in or out.")]
        private float fadeSeconds = 0.25f;

        private PressurePlate _plate;
        private MaterialPropertyBlock _block;
        private float _glow;
        private float _applied = -1f;

        private void Awake()
        {
            _plate = GetComponent<PressurePlate>();
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            _glow = Mathf.MoveTowards(_glow, _plate.IsPressed ? 1f : 0f, Time.deltaTime / fadeSeconds);
            if (rune == null || Mathf.Approximately(_glow, _applied)) return;
            _applied = _glow;
            rune.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Color.Lerp(idleColor, Color.white, _glow));
            _block.SetColor(EmissionColorId, glowColor * _glow);
            rune.SetPropertyBlock(_block);
        }
    }
}

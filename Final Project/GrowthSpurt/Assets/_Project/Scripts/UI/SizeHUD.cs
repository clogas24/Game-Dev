using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GrowthSpurt
{
    /// <summary>
    /// Three pips (S / N / B). The current size is highlighted and enlarged; a refused grow flashes all
    /// pips red and shakes the row. An optional debug line shows weight and the last landing speed.
    /// </summary>
    public class SizeHUD : MonoBehaviour
    {
        [SerializeField] private SizeShifter shifter;

        [Header("Pips (index 0 = Small, 1 = Normal, 2 = Big)")]
        [SerializeField] private Image[] pips = new Image[3];
        [SerializeField] private TMP_Text[] pipLabels = new TMP_Text[3];
        [SerializeField] private RectTransform pipRow;

        [Header("Look")]
        [SerializeField] private Color activeColor = new Color(0.36f, 0.86f, 0.62f, 1f);
        [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color activeLabelColor = new Color(0.05f, 0.1f, 0.08f, 1f);
        [SerializeField] private Color inactiveLabelColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color blockedColor = new Color(1f, 0.22f, 0.2f, 1f);
        [SerializeField, Min(1f)] private float activeScale = 1.25f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.45f;
        [SerializeField, Min(0f)] private float shakePixels = 10f;

        [Header("Debug")]
        [SerializeField] private TMP_Text debugText;

        private float _flash;     // 1 -> 0 after a blocked grow
        private Vector2 _rowHome;

        private void Awake()
        {
            if (shifter == null) shifter = FindAnyObjectByType<SizeShifter>();
            if (pipRow != null) _rowHome = pipRow.anchoredPosition;
        }

        private void OnEnable()
        {
            if (shifter != null) shifter.OnGrowBlocked += HandleGrowBlocked;
        }

        private void OnDisable()
        {
            if (shifter != null) shifter.OnGrowBlocked -= HandleGrowBlocked;
        }

        private void HandleGrowBlocked(PlayerSize _) => _flash = 1f;

        private void Update()
        {
            if (shifter == null) return;

            _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime / flashDuration);
            int current = (int)shifter.CurrentSize;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                bool active = i == current;
                Color baseColor = active ? activeColor : inactiveColor;
                pips[i].color = Color.Lerp(baseColor, blockedColor, _flash);

                float targetScale = active ? activeScale : 1f;
                var t = pips[i].rectTransform;
                float s = Mathf.Lerp(t.localScale.x, targetScale, 1f - Mathf.Exp(-18f * dt));
                t.localScale = new Vector3(s, s, 1f);

                if (i < pipLabels.Length && pipLabels[i] != null)
                    pipLabels[i].color = active ? activeLabelColor : inactiveLabelColor;
            }

            if (pipRow != null)
            {
                float shake = _flash * _flash * shakePixels;
                pipRow.anchoredPosition = _rowHome + new Vector2(Mathf.Sin(Time.unscaledTime * 70f) * shake, 0f);
            }

            if (debugText != null)
            {
                debugText.text =
                    $"{shifter.CurrentSize}  ·  weight {shifter.Weight}  ·  scale {shifter.CurrentScale:0.00}\n" +
                    $"{(shifter.IsGrounded ? "grounded" : "airborne")}{(shifter.IsSlamming ? "  ·  SLAM" : "")}  ·  " +
                    $"last landing {shifter.LastLandingSpeed:0.0} m/s{(shifter.LastLandingWasSlam ? " (slam)" : "")}";
            }
        }
    }
}

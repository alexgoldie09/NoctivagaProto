using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component that visualizes beat progress using two fill bars moving toward center,
/// and a central pulsing heart image with optional glow.
/// </summary>
public class BeatBarUI : MonoBehaviour
{
    [Header("Fill Bars")]
    [SerializeField] private Image fillLeft;           // Fills left-to-right
    [SerializeField] private Image fillRight;          // Fills right-to-left

    [Header("Heart Visuals")]
    [SerializeField] private Image heartImage;         // Central heart that pulses
    [SerializeField] private Image glowImage;          // Optional glow behind heart

    [Header("Pulse Settings")]
    [SerializeField] private float pulseDuration = 0.15f;
    [SerializeField] private Vector3 pulseScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private Color pulseColor = Color.white;
    [SerializeField] private Color glowColor = new Color(1f, 1f, 1f, 0.4f);

    private Vector3 originalScale;
    private Color originalHeartColor;
    private Color originalGlowColor;
    private float pulseTimer = 0f;

    // ─────────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (heartImage != null)
            originalScale = heartImage.rectTransform.localScale;

        originalHeartColor = heartImage?.color ?? Color.white;
        originalGlowColor = glowImage?.color ?? Color.white;

        RhythmManager.OnBeat += TriggerPulse;
    }

    private void OnDestroy()
    {
        RhythmManager.OnBeat -= TriggerPulse;
    }

    private void Update()
    {
        if (Utilities.IsGameFrozen) return; // skip ticking while frozen
        
        if (RhythmManager.Instance == null)
            return;

        float progress = RhythmManager.Instance.GetBeatProgress();

        // Update both fill directions
        if (fillLeft != null)
            fillLeft.fillAmount = progress;
        if (fillRight != null)
            fillRight.fillAmount = progress;

        // Animate pulse effect
        if (pulseTimer > 0f)
        {
            pulseTimer -= Time.deltaTime;
            float t = 1f - (pulseTimer / pulseDuration);

            if (heartImage != null)
            {
                heartImage.rectTransform.localScale = Vector3.Lerp(pulseScale, originalScale, t);
                heartImage.color = Color.Lerp(pulseColor, originalHeartColor, t);
            }

            if (glowImage != null)
                glowImage.color = Color.Lerp(glowColor, originalGlowColor, t);
        }
        else
        {
            if (heartImage != null)
            {
                heartImage.rectTransform.localScale = originalScale;
                heartImage.color = originalHeartColor;
            }

            if (glowImage != null)
                glowImage.color = originalGlowColor;
        }
    }

    /// <summary>
    /// Triggers the visual pulse effect on each beat.
    /// </summary>
    private void TriggerPulse()
    {
        pulseTimer = pulseDuration;

        if (heartImage != null)
        {
            heartImage.rectTransform.localScale = pulseScale;
            heartImage.color = pulseColor;
        }

        if (glowImage != null)
            glowImage.color = glowColor;
    }
}

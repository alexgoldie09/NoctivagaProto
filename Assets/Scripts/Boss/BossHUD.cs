using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple boss HUD that binds to a BossHealth component and updates UI via events.
/// </summary>
public class BossHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BossHealth bossHealth;

    [Header("UI")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text phaseText;

    private void OnEnable()
    {
        if (bossHealth == null)
            bossHealth = FindAnyObjectByType<BossHealth>();

        Bind(bossHealth);
    }

    private void OnDisable()
    {
        Unbind();
    }

    public void Bind(BossHealth health)
    {
        Unbind();

        bossHealth = health;
        if (bossHealth == null)
        {
            Debug.LogWarning("[BossHUD] No BossHealth found to bind.");
            return;
        }

        bossHealth.OnHealthChanged += HandleHealthChanged;
        bossHealth.OnPhaseChanged += HandlePhaseChanged;
        bossHealth.OnDied += HandleDied;
        bossHealth.OnPlayerDied += HandleDied;

        // Initialize display immediately
        HandleHealthChanged(bossHealth.CurrentHP, bossHealth.MaxHP);
        HandlePhaseChanged(bossHealth.CurrentPhase);

        if (bossNameText != null)
            bossNameText.text = bossHealth.BossName;

        gameObject.SetActive(true);
    }

    private void Unbind()
    {
        if (bossHealth == null) return;

        bossHealth.OnHealthChanged -= HandleHealthChanged;
        bossHealth.OnPhaseChanged -= HandlePhaseChanged;
        bossHealth.OnDied -= HandleDied;
        bossHealth.OnPlayerDied -= HandleDied;
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (hpSlider != null)
        {
            hpSlider.minValue = 0;
            hpSlider.maxValue = max;
            hpSlider.value = current;
        }
    }

    private void HandlePhaseChanged(BossPhase phase)
    {
        if (phaseText != null)
            phaseText.text = $"Phase {(int)phase}";
    }

    private void HandleDied()
    {
        gameObject.SetActive(false);
    }
}

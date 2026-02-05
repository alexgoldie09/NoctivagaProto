using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple boss HUD that binds to a BossHealth instance and updates UI elements via events.
/// Intended to be enabled during boss fights and hidden when the fight ends (boss dies or player dies).
/// </summary>
public class BossHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("BossHealth to bind to. If not assigned, the HUD will attempt to FindAnyObjectByType<BossHealth>() on enable.")]
    [SerializeField] private BossHealth bossHealth;
    [Tooltip("Shield component to bind to. If not assigned, the HUD will attempt to FindAnyObjectByType<BossShieldHealth>().")]
    [SerializeField] private BossShieldHealth shieldHealth;

    [Header("UI")]
    [Tooltip("Slider used to display boss HP.")]
    [SerializeField] private Slider hpSlider;
    [Tooltip("Optional slider used to display boss shield HP.")]
    [SerializeField] private Slider shieldSlider;
    [Tooltip("Text field used to display boss name.")]
    [SerializeField] private TMP_Text bossNameText;
    [Tooltip("Text field used to display current boss phase.")]
    [SerializeField] private TMP_Text phaseText;

    // ─────────────────────────────────────────────
    #region Unity Lifecycle

    /// <summary>
    /// Attempts to locate a boss health component (if not assigned) and binds the HUD to it.
    /// </summary>
    private void OnEnable()
    {
        if (bossHealth == null)
            bossHealth = FindAnyObjectByType<BossHealth>();

        if (shieldHealth == null)
            shieldHealth = FindAnyObjectByType<BossShieldHealth>();

        Bind(bossHealth);
        BindShield(shieldHealth);
    }

    /// <summary>
    /// Unbinds from any previously bound <see cref="BossHealth"/> to avoid event leaks.
    /// </summary>
    private void OnDisable()
    {
        Unbind();
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Binding

    /// <summary>
    /// Binds the HUD to a boss health component and initializes UI immediately.
    /// </summary>
    /// <param name="health">Boss health component to bind to.</param>
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

        // Initialize display immediately.
        HandleHealthChanged(bossHealth.CurrentHP, bossHealth.MaxHP);
        HandlePhaseChanged(bossHealth.CurrentPhase);

        if (bossNameText != null)
            bossNameText.text = bossHealth.BossName;

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Binds the HUD to a boss shield component and initializes UI immediately.
    /// </summary>
    /// <param name="shield">Shield component to bind to.</param>
    public void BindShield(BossShieldHealth shield)
    {
        UnbindShield();

        shieldHealth = shield;
        if (shieldHealth == null || shieldSlider == null)
        {
            if (shieldSlider != null)
                shieldSlider.gameObject.SetActive(false);
            return;
        }

        shieldHealth.OnShieldChanged += HandleShieldChanged;
        shieldHealth.OnShieldBroken += HandleShieldBroken;

        HandleShieldChanged(shieldHealth.CurrentHP, shieldHealth.MaxHP);
        shieldSlider.gameObject.SetActive(!shieldHealth.IsBroken);
    }

    /// <summary>
    /// Unbinds from the current <see cref="BossHealth"/> instance (if any).
    /// </summary>
    private void Unbind()
    {
        if (bossHealth == null)
            return;

        bossHealth.OnHealthChanged -= HandleHealthChanged;
        bossHealth.OnPhaseChanged -= HandlePhaseChanged;
        bossHealth.OnDied -= HandleDied;
        bossHealth.OnPlayerDied -= HandleDied;
    }

    /// <summary>
    /// Unbinds from the current <see cref="BossShieldHealth"/> instance (if any).
    /// </summary>
    private void UnbindShield()
    {
        if (shieldHealth == null)
            return;

        shieldHealth.OnShieldChanged -= HandleShieldChanged;
        shieldHealth.OnShieldBroken -= HandleShieldBroken;
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Event Handlers

    /// <summary>
    /// Updates the HP slider based on the current/max HP values.
    /// </summary>
    /// <param name="current">Current boss HP.</param>
    /// <param name="max">Max boss HP.</param>
    private void HandleHealthChanged(int current, int max)
    {
        if (hpSlider == null)
            return;

        hpSlider.minValue = 0;
        hpSlider.maxValue = max;
        hpSlider.value = current;
    }

    /// <summary>
    /// Updates the phase label to reflect the current boss phase.
    /// </summary>
    /// <param name="phase">Current boss phase.</param>
    private void HandlePhaseChanged(BossPhase phase)
    {
        if (phaseText != null)
            phaseText.text = $"Phase {(int)phase}";
    }

    /// <summary>
    /// Hides the HUD when the boss dies or when the player death event is raised.
    /// </summary>
    private void HandleDied()
    {
        gameObject.SetActive(false);
    }
    
    private void HandleShieldChanged(int current, int max)
    {
        if (shieldSlider == null)
            return;

        shieldSlider.minValue = 0;
        shieldSlider.maxValue = max;
        shieldSlider.value = current;
        shieldSlider.gameObject.SetActive(current > 0);
    }

    private void HandleShieldBroken()
    {
        if (shieldSlider != null)
            shieldSlider.gameObject.SetActive(false);
    }

    #endregion
    // ─────────────────────────────────────────────
}

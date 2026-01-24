using System;
using UnityEngine;

/// <summary>
/// Discrete phase buckets based on normalized boss health thresholds.
/// </summary>
public enum BossPhase
{
    Phase1 = 1,
    Phase2 = 2,
    Phase3 = 3
}

/// <summary>
/// Holds boss HP, computes phase transitions from normalized thresholds, and raises events for UI and controllers.
/// </summary>
public class BossHealth : MonoBehaviour
{

    [Header("Identity")]
    [Tooltip("Display name for this boss (used by UI and/or announcers).")]
    [SerializeField] private string bossName = "Boss";
    [Header("Health")]
    [Tooltip("Maximum hit points for this boss.")]
    [Min(1)]
    [SerializeField] private int maxHP = 9;
    [Tooltip("Starting hit points at runtime. If 0 or less, defaults to Max HP.")]
    [SerializeField] private int startingHP = 9;

    [Header("Damage Tuning")]
    [Tooltip("Default damage applied per valid hit. Boss controller can override and pass a custom amount.")]
    [Min(0)]
    [SerializeField] private int damagePerHit = 1;

    [Header("Phase Thresholds (normalized)")]
    [Tooltip("When health fraction is <= this value, boss enters Phase 2.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase2Threshold = 0.66f;
    [Tooltip("When health fraction is <= this value, boss enters Phase 3.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase3Threshold = 0.33f;

    // ─────────────────────────────────────────────
    #region Events

    /// <summary>
    /// Raised whenever current HP changes.
    /// (currentHP, maxHP)
    /// </summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>
    /// Raised whenever the boss phase changes.
    /// (newPhase)
    /// </summary>
    public event Action<BossPhase> OnPhaseChanged;

    /// <summary>
    /// Raised once when the boss reaches 0 HP.
    /// </summary>
    public event Action OnDied;

    /// <summary>
    /// Raised when the player death is reported while this boss is active.
    /// </summary>
    public event Action OnPlayerDied;

    #endregion
    // ─────────────────────────────────────────────
    #region Public Properties

    /// <summary>
    /// Display name for this boss.
    /// </summary>
    public string BossName => bossName;

    /// <summary>
    /// Maximum hit points.
    /// </summary>
    public int MaxHP => maxHP;

    /// <summary>
    /// Current hit points.
    /// </summary>
    public int CurrentHP { get; private set; }

    /// <summary>
    /// Default damage amount applied by <see cref="ApplyDefaultHit"/>.
    /// </summary>
    public int DamagePerHit => damagePerHit;

    /// <summary>
    /// Current computed phase based on normalized thresholds.
    /// </summary>
    public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;

    /// <summary>
    /// Current health fraction, in the range [0..1].
    /// </summary>
    public float Health01 => maxHP <= 0 ? 0f : Mathf.Clamp01((float)CurrentHP / maxHP);

    #endregion
    // ─────────────────────────────────────────────
    #region Unity Lifecycle

    /// <summary>
    /// Validates starting HP, initializes runtime values, computes initial phase,
    /// and fires initial events so UI can initialize immediately.
    /// </summary>
    private void Awake()
    {
        // Keep startingHP valid
        if (startingHP <= 0)
            startingHP = maxHP;

        startingHP = Mathf.Clamp(startingHP, 1, maxHP);

        CurrentHP = startingHP;
        RecomputePhase(true);

        // Fire once so UI can initialize immediately
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Damage / Healing

    /// <summary>
    /// Applies default damage (Damage Per Hit).
    /// </summary>
    /// <returns>True if damage was applied; false if ignored (dead or amount invalid).</returns>
    [ContextMenu("Apply Default Damage")]
    public bool ApplyDefaultHit()
    {
        return ApplyDamage(damagePerHit);
    }

    /// <summary>
    /// Applies a custom damage amount.
    /// </summary>
    /// <param name="amount">Damage amount to apply (must be greater than 0).</param>
    /// <returns>True if damage was applied; false if ignored (dead or amount invalid).</returns>
    public bool ApplyDamage(int amount)
    {
        if (amount <= 0)
            return false;

        if (CurrentHP <= 0)
            return false;

        int prev = CurrentHP;
        CurrentHP = Mathf.Max(0, CurrentHP - amount);

        if (CurrentHP != prev)
        {
            OnHealthChanged?.Invoke(CurrentHP, maxHP);

            RecomputePhase(false);

            if (CurrentHP == 0)
                OnDied?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Heals the boss.
    /// </summary>
    /// <param name="amount">Heal amount to apply (must be greater than 0).</param>
    /// <returns>True if HP changed; false if ignored (dead or amount invalid).</returns>
    public bool Heal(int amount)
    {
        if (amount <= 0)
            return false;

        if (CurrentHP <= 0)
            return false;

        int prev = CurrentHP;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);

        if (CurrentHP != prev)
        {
            OnHealthChanged?.Invoke(CurrentHP, maxHP);
            RecomputePhase(false);
            return true;
        }

        return false;
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Reset / Phase Computation
    /// <summary>
    /// Resets the boss to full HP and recomputes phase (notifies listeners immediately).
    /// </summary>
    public void ResetToFull()
    {
        CurrentHP = maxHP;
        RecomputePhase(true);

        OnHealthChanged?.Invoke(CurrentHP, maxHP);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    /// <summary>
    /// Recomputes phase from current health fraction and normalized thresholds.
    /// Ensures thresholds are ordered logically (phase 3 threshold must be <= phase 2 threshold).
    /// </summary>
    /// <param name="forceNotify">If true, emits a phase event even if phase did not change.</param>
    private void RecomputePhase(bool forceNotify)
    {
        // Ensure thresholds are ordered logically (phase3 must be <= phase2)
        float p2 = Mathf.Clamp01(phase2Threshold);
        float p3 = Mathf.Clamp01(phase3Threshold);

        if (p3 > p2)
            p3 = p2;

        float hp01 = Health01;

        BossPhase newPhase;

        if (hp01 <= p3)
            newPhase = BossPhase.Phase3;
        else if (hp01 <= p2)
            newPhase = BossPhase.Phase2;
        else
            newPhase = BossPhase.Phase1;

        if (forceNotify || newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            OnPhaseChanged?.Invoke(CurrentPhase);
        }
    }
    #endregion
    // ─────────────────────────────────────────────
    #region External Notifications

    /// <summary>
    /// Notifies listeners that the player died while this boss is active.
    /// This is typically invoked by the fight controller or health system.
    /// </summary>
    public void NotifyPlayerDied()
    {
        OnPlayerDied?.Invoke();
    }

    #endregion
}

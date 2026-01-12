using System;
using UnityEngine;

public enum BossPhase
{
    Phase1 = 1,
    Phase2 = 2,
    Phase3 = 3
}

/// <summary>
/// Holds boss HP, computes phases, and raises events for UI and controllers.
/// </summary>
public class BossHealth : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string bossName = "Boss";

    [Header("Health")]
    [Min(1)]
    [SerializeField] private int maxHP = 9;

    [SerializeField] private int startingHP = 9;

    [Header("Damage Tuning")]
    [Tooltip("Default damage applied per valid hit. Boss controller can override and pass a custom amount.")]
    [Min(0)]
    [SerializeField] private int damagePerHit = 1;

    [Header("Phase Thresholds (normalized)")]
    [Range(0f, 1f)]
    [Tooltip("When health fraction is <= this value, boss enters Phase 2.")]
    [SerializeField] private float phase2Threshold = 0.66f;

    [Range(0f, 1f)]
    [Tooltip("When health fraction is <= this value, boss enters Phase 3.")]
    [SerializeField] private float phase3Threshold = 0.33f;

    public event Action<int, int> OnHealthChanged;          // current, max
    public event Action<BossPhase> OnPhaseChanged;          // new phase
    public event Action OnDied;

    public string BossName => bossName;
    public int MaxHP => maxHP;
    public int CurrentHP { get; private set; }
    public int DamagePerHit => damagePerHit;
    public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;

    public float Health01 => (maxHP <= 0) ? 0f : Mathf.Clamp01((float)CurrentHP / maxHP);

    private void Awake()
    {
        // Keep startingHP valid
        if (startingHP <= 0) startingHP = maxHP;
        startingHP = Mathf.Clamp(startingHP, 1, maxHP);

        CurrentHP = startingHP;
        RecomputePhase(true);

        // Fire once so UI can initialize immediately
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    /// <summary>
    /// Apply default damage (damagePerHit). Returns true if damage was applied.
    /// </summary>
    [ContextMenu("Apply Default Damage")]
    public bool ApplyDefaultHit()
    {
        return ApplyDamage(damagePerHit);
    }

    /// <summary>
    /// Apply a custom damage amount. Returns true if damage was applied.
    /// </summary>
    public bool ApplyDamage(int amount)
    {
        if (amount <= 0) return false;
        if (CurrentHP <= 0) return false;

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
    /// Heals the boss (if you ever need it). Returns true if changed.
    /// </summary>
    public bool Heal(int amount)
    {
        if (amount <= 0) return false;
        if (CurrentHP <= 0) return false;

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

    public void ResetToFull()
    {
        CurrentHP = maxHP;
        RecomputePhase(true);
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    private void RecomputePhase(bool forceNotify)
    {
        // Ensure thresholds are ordered logically (phase3 must be <= phase2)
        float p2 = Mathf.Clamp01(phase2Threshold);
        float p3 = Mathf.Clamp01(phase3Threshold);
        if (p3 > p2) p3 = p2;

        BossPhase newPhase;

        float hp01 = Health01;

        if (hp01 <= p3) newPhase = BossPhase.Phase3;
        else if (hp01 <= p2) newPhase = BossPhase.Phase2;
        else newPhase = BossPhase.Phase1;

        if (forceNotify || newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            OnPhaseChanged?.Invoke(CurrentPhase);
        }
    }
}

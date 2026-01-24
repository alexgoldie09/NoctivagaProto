using UnityEngine;

/// <summary>
/// Phase tuning asset that maps boss phases to boss-specific phase data assets.
/// </summary>
[CreateAssetMenu(menuName = "Boss/Boss Phase Tuning")]
public class BossPhaseTuning : ScriptableObject
{
    [Header("Phase Data (assign boss-specific assets here)")]
    [SerializeField] private BossPhaseData phase1;
    [SerializeField] private BossPhaseData phase2;
    [SerializeField] private BossPhaseData phase3;

    /// <summary>
    /// Returns the phase data asset for the specified phase.
    /// </summary>
    public BossPhaseData Get(BossPhase phase)
    {
        switch (phase)
        {
            case BossPhase.Phase2: return phase2;
            case BossPhase.Phase3: return phase3;
            default: return phase1;
        }
    }

    /// <summary>
    /// Returns the phase data asset for the specified phase, cast to the requested type.
    /// </summary>
    public T Get<T>(BossPhase phase) where T : BossPhaseData
    {
        return Get(phase) as T;
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase tuning data for the Vampire Queen attack action.
/// </summary>
[CreateAssetMenu(menuName = "Boss/Phases/Vampire Queen Attack Phase Data")]
public class VampireQueenAttackPhaseData : BossPhaseData
{
    [System.Serializable]
    public struct AttackMicroPhase
    {
        [Header("Vulnerable Window")]
        [Min(0f)]
        public float vulnerableDuration;

        [Header("Movement")]
        [Min(0f)]
        public float descentSpeed;

        [Header("Retreat")]
        [Min(0f)]
        public float retreatDelay;

        [Header("Projectile Bursts")]
        [Min(0)]
        public int shotBursts;
        [Min(0f)]
        public float shotInterval;
    }

    [Header("Vulnerable Window")]
    [Min(0f)]
    public float vulnerableDuration = 3f;

    [Header("Movement")]
    [Min(0f)]
    public float descentSpeed = 3f;

    [Header("Retreat")]
    [Min(0f)]
    public float retreatDelay = 0.3f;

    [Header("Projectile Bursts")]
    [Min(0)]
    public int shotBursts = 2;
    [Min(0f)]
    public float shotInterval = 0.4f;

    [Header("Micro Phases (Phase 1 Loop)")]
    [Tooltip("Optional micro-phase overrides. If empty, the default fields above are used.")]
    [SerializeField] private List<AttackMicroPhase> microPhases = new();

    public AttackMicroPhase GetMicroPhase(int index)
    {
        if (microPhases == null || microPhases.Count == 0)
        {
            return new AttackMicroPhase
            {
                vulnerableDuration = vulnerableDuration,
                descentSpeed = descentSpeed,
                retreatDelay = retreatDelay,
                shotBursts = shotBursts,
                shotInterval = shotInterval
            };
        }

        if (index < 0)
            index = 0;

        return microPhases[Mathf.Min(index, microPhases.Count - 1)];
    }
}
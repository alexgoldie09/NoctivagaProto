using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase tuning data for the Vampire Queen summon action.
/// </summary>
[CreateAssetMenu(menuName = "Boss/Phases/Vampire Queen Summon Phase Data")]
public class VampireQueenSummonPhaseData : BossPhaseData
{
    [System.Serializable]
    public struct SummonMicroPhase
    {
        [Header("Wave Size")]
        [Min(1)]
        public int waveSize;

        [Header("Summon Timing")]
        [Min(0f)]
        public float summonInterval;

        [Header("Movement")]
        [Min(0f)]
        public float flyMoveSpeed;

        [Header("Concurrency")]
        [Tooltip("Maximum number of concurrent enemies; 0 or less means no cap.")]
        public int maxConcurrentEnemies;
    }

    [Header("Wave Size")]
    [Min(1)]
    public int waveSize = 4;

    [Header("Summon Timing")]
    [Min(0f)]
    public float summonInterval = 1.25f;

    [Header("Movement")]
    [Min(0f)]
    public float flyMoveSpeed = 3.0f;

    [Header("Concurrency")]
    [Tooltip("Maximum number of concurrent enemies; 0 or less means no cap.")]
    public int maxConcurrentEnemies = 0;

    [Header("Micro Phases (Phase 1 Loop)")]
    [Tooltip("Optional micro-phase overrides. If empty, the default fields above are used.")]
    [SerializeField] private List<SummonMicroPhase> microPhases = new();

    public SummonMicroPhase GetMicroPhase(int index)
    {
        if (microPhases == null || microPhases.Count == 0)
        {
            return new SummonMicroPhase
            {
                waveSize = waveSize,
                summonInterval = summonInterval,
                flyMoveSpeed = flyMoveSpeed,
                maxConcurrentEnemies = maxConcurrentEnemies
            };
        }

        if (index < 0)
            index = 0;

        return microPhases[Mathf.Min(index, microPhases.Count - 1)];
    }
}
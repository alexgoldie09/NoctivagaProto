using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Phase tuning data for the Vampire Queen charge action.
/// </summary>
[CreateAssetMenu(menuName = "Boss/Phases/Vampire Queen Charge Phase Data")]
public class VampireQueenChargePhaseData : BossPhaseData
{
    [System.Serializable]
    public struct ChargeMicroPhase
    {
        [Header("Telegraph")]
        [Min(0f)]
        public float telegraphDuration;
        [Min(0f)]
        public float preChargeDelay;

        [Header("Movement")]
        [Min(0f)]
        public float approachSpeed;
        [Min(0f)]
        public float chargeSpeed;
        [Min(0f)]
        public float postChargeDelay;

        [Header("Recovery")]
        [Min(0f)]
        public float hurtRecoveryDuration;

        [Header("Damage")]
        [Min(1)]
        public int trapDamage;
    }

    [Header("Telegraph")]
    [Tooltip("Seconds the telegraph is shown before charging.")]
    public float telegraphDuration = 1.25f;
    [Tooltip("Delay between telegraph clearing and charge start.")]
    public float preChargeDelay = 0.2f;

    [Header("Movement")]
    [Tooltip("Movement speed to position at the start of the lane.")]
    public float approachSpeed = 4f;
    [Tooltip("Charge speed while moving across the lane.")]
    public float chargeSpeed = 4.5f;
    [Tooltip("Delay after completing a charge.")]
    public float postChargeDelay = 0.3f;

    [Header("Recovery")]
    [Tooltip("Duration spent recovering after hitting a trap.")]
    public float hurtRecoveryDuration = 5f;

    [Header("Damage")]
    [Tooltip("Damage applied when the boss hits a trap tile during the charge.")]
    public int trapDamage = 1;

    [Header("Micro Phases (Phase 2+ Loop)")]
    [Tooltip("Optional micro-phase overrides. If empty, the default fields above are used.")]
    [SerializeField] private List<ChargeMicroPhase> microPhases = new();

    public ChargeMicroPhase GetMicroPhase(int index)
    {
        if (microPhases == null || microPhases.Count == 0)
        {
            return new ChargeMicroPhase
            {
                telegraphDuration = telegraphDuration,
                preChargeDelay = preChargeDelay,
                approachSpeed = approachSpeed,
                chargeSpeed = chargeSpeed,
                postChargeDelay = postChargeDelay,
                hurtRecoveryDuration = hurtRecoveryDuration,
                trapDamage = trapDamage
            };
        }

        if (index < 0)
            index = 0;

        return microPhases[Mathf.Min(index, microPhases.Count - 1)];
    }
}

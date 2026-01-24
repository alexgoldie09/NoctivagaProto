using UnityEngine;

/// <summary>
/// Phase tuning data for the gargoyle bombing action.
/// </summary>
[CreateAssetMenu(menuName = "Boss/Phases/Gargoyle Bombing Phase Data")]
public class GargoyleBombingPhaseData : BossPhaseData
{
    public int bombsPerFlight = 6;
    public float flyMoveSpeed = 6f;
    public float bombInterval = 1.0f;
    public float telegraphDuration = 0.8f;
}
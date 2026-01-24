using UnityEngine;

/// <summary>
/// Phase tuning data for the gargoyle rest action.
/// </summary>
[CreateAssetMenu(menuName = "Boss/Phases/Gargoyle Rest Phase Data")]
public class GargoyleRestPhaseData : BossPhaseData
{
    public float restDuration = 6.0f;
    public float flyMoveSpeed = 6f;
}
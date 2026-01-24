using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss action component that runs the gargoyle rest sequence.
/// </summary>
public class GargoyleRestAction : BossAction
{
    [Header("Rest Travel")]
    [SerializeField] private float restHoverHeight = 2.0f;
    [SerializeField] private float landingSpeed = 10f;
    [SerializeField] private float preRestHoverTime = 0.10f;
    
    [Header("Rest Damage")]
    [Tooltip("Damage applied when player successfully places onto boss footprint during Rest.")]
    [SerializeField] private int damagePerRestHit = 1;
    [Tooltip("Boss can only take one hit per rest cycle.")]
    [SerializeField] private bool oneHitPerRest = true;

    [Header("Phase Tuning")]
    [SerializeField] private BossPhaseTuning phaseTuning;

    [Header("Movement")]
    [SerializeField] private float arriveEpsilon = 0.05f;
    
    /// <summary>
    /// Damage applied when the boss is hit during rest.
    /// </summary>
    public int DamagePerRestHit => damagePerRestHit;

    /// <summary>
    /// Whether the boss can take only one hit per rest cycle.
    /// </summary>
    public bool OneHitPerRest => oneHitPerRest;
    
    /// <summary>
    /// Returns true if the action has a valid gargoyle controller.
    /// </summary>
    public override bool CanRun(BossContext context)
    {
        return context?.controller is GargoyleBossController;
    }

    /// <summary>
    /// Executes the gargoyle rest sequence.
    /// </summary>
    public override IEnumerator Execute(BossContext context)
    {
        var gargoyle = context?.controller as GargoyleBossController;
        if (gargoyle == null || context.grid == null)
            yield break;

        if (!TryFindRestAnchor(context, out Vector3Int anchor))
            yield break;

        gargoyle.SetRestAnchor(anchor);
        yield return RestRoutine(gargoyle, anchor);
    }

    private bool TryFindRestAnchor(BossContext context, out Vector3Int anchor)
    {
        anchor = default;

        var anchors = new List<Vector3Int>();
        var voidCells = new HashSet<Vector3Int>();
        var paintedGroundCells = context.grid.GetAllPaintedGroundCells();

        if (paintedGroundCells == null || paintedGroundCells.Count == 0)
            return false;

        for (int i = 0; i < paintedGroundCells.Count; i++)
        {
            var c = paintedGroundCells[i];
            if (!context.grid.IsPaintedGroundCell(c)) continue;

            if (context.grid.GetTileKind(c) == TileKind.Void)
                voidCells.Add(c);
        }

        for (int i = 0; i < paintedGroundCells.Count; i++)
        {
            var a = paintedGroundCells[i];
            if (!voidCells.Contains(a)) continue;

            var r = a + new Vector3Int(1, 0, 0);
            var d = a + new Vector3Int(0, -1, 0);
            var rd = a + new Vector3Int(1, -1, 0);

            if (voidCells.Contains(r) && voidCells.Contains(d) && voidCells.Contains(rd))
                anchors.Add(a);
        }

        if (anchors.Count == 0)
            return false;

        anchor = anchors[Random.Range(0, anchors.Count)];
        return true;
    }

    private IEnumerator RestRoutine(GargoyleBossController gargoyle, Vector3Int anchor)
    {
        Vector3 restWorld = gargoyle.GetFootprintCenterWorld(anchor);

        gargoyle.SetState(BossControllerBase.BossState.Flight);
        gargoyle.PlayAnimation("Fly");

        Vector3 hoverWorld = restWorld + Vector3.up * restHoverHeight;
        yield return FlyToWorldPoint(gargoyle, hoverWorld, GetFlySpeedForPhase(gargoyle));

        if (preRestHoverTime > 0f)
            yield return new WaitForSeconds(preRestHoverTime);

        yield return LandToWorldPoint(gargoyle, restWorld);

        gargoyle.SetState(BossControllerBase.BossState.Rest);
        gargoyle.TookDamageThisRest = false;
        gargoyle.PlayAnimation("Rest");

        float t = 0f;
        while (t < GetRestDuration(gargoyle))
        {
            if (gargoyle.State == BossControllerBase.BossState.Dead) yield break;

            if (gargoyle.TookDamageThisRest)
                break;

            t += Time.deltaTime;
            yield return null;
        }

        yield return null;
    }

    private IEnumerator FlyToWorldPoint(GargoyleBossController gargoyle, Vector3 targetWorld, float speed)
    {
        gargoyle.PlayAnimation("Fly");

        while (Vector3.Distance(gargoyle.transform.position, targetWorld) > arriveEpsilon)
        {
            if (gargoyle.State == BossControllerBase.BossState.Dead) yield break;

            gargoyle.UpdateFacingTowards(targetWorld);

            gargoyle.transform.position = Vector3.MoveTowards(
                gargoyle.transform.position,
                targetWorld,
                speed * Time.deltaTime
            );

            yield return null;
        }
    }

    private IEnumerator LandToWorldPoint(GargoyleBossController gargoyle, Vector3 targetWorld)
    {
        gargoyle.PlayAnimation("Land");

        while (Vector3.Distance(gargoyle.transform.position, targetWorld) > arriveEpsilon)
        {
            if (gargoyle.State == BossControllerBase.BossState.Dead) yield break;

            gargoyle.UpdateFacingTowards(targetWorld);

            gargoyle.transform.position = Vector3.MoveTowards(
                gargoyle.transform.position,
                targetWorld,
                landingSpeed * Time.deltaTime
            );

            yield return null;
        }
    }

    private GargoyleRestPhaseData GetPhaseData(GargoyleBossController gargoyle)
    {
        if (gargoyle == null || gargoyle.BossHealth == null || phaseTuning == null)
            return null;

        return phaseTuning.Get<GargoyleRestPhaseData>(gargoyle.BossHealth.CurrentPhase);
    }

    private float GetFlySpeedForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data.flyMoveSpeed;
    }

    private float GetRestDuration(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data.restDuration;
    }
}

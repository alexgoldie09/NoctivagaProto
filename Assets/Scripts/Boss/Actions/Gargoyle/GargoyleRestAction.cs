using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss action component that runs the Gargoyle rest sequence.
/// 
/// The boss searches for a valid 2x2 Void patch (DeathVoid), flies above it, lands, then enters Rest state.
/// During Rest, the player can deal damage by placing floor tiles onto the boss footprint.
/// 
/// </summary>
public class GargoyleRestAction : BossAction
{
    [Header("Rest Travel")]
    [Tooltip("Vertical hover offset above the chosen rest position before landing.")]
    [SerializeField] private float restHoverHeight = 2.0f;
    [Tooltip("Pause time after arriving at hover point before beginning landing.")]
    [SerializeField] private float preRestHoverTime = 0.10f;

    [Header("Rest Damage")]
    [Tooltip("Damage applied when player successfully places onto boss footprint during Rest.")]
    [SerializeField] private int damagePerRestHit = 1;
    [Tooltip("If true, the boss can only take one hit per rest cycle.")]
    [SerializeField] private bool oneHitPerRest = true;

    [Header("Phase Tuning")]
    [Tooltip("Phase tuning asset used to configure fly speed and rest duration per boss phase.")]
    [SerializeField] private BossPhaseTuning phaseTuning;
    
    [Header("Animation")]
    [Tooltip("Animator trigger to play when landing occurs.")]
    [SerializeField] private string landAnimationTrigger = "Land";
    [Tooltip("Animator trigger to play when resting..")]
    [SerializeField] private string restAnimationTrigger = "Rest";

    // ─────────────────────────────────────────────
    #region Public Properties

    /// <summary>
    /// Damage applied when the boss is hit during rest.
    /// </summary>
    public int DamagePerRestHit => damagePerRestHit;

    /// <summary>
    /// Whether the boss can take only one hit per rest cycle.
    /// </summary>
    public bool OneHitPerRest => oneHitPerRest;

    #endregion
    // ─────────────────────────────────────────────
    #region BossAction Overrides

    /// <summary>
    /// Returns true if the action has a valid gargoyle controller.
    /// </summary>
    /// <param name="context">Boss action context.</param>
    /// <returns>True if runnable; otherwise false.</returns>
    public override bool CanRun(BossContext context)
    {
        return context?.controller is GargoyleBossController;
    }

    /// <summary>
    /// Executes the gargoyle rest sequence:
    /// finds a valid rest anchor (2x2 void patch), flies above it, lands, then waits in Rest state
    /// until the phase-duration elapses or the boss takes damage (if One Hit Per Rest is enabled).
    /// </summary>
    /// <param name="context">Boss action context.</param>
    /// <returns>Coroutine enumerator.</returns>
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

    #endregion
    // ─────────────────────────────────────────────
    #region Anchor Selection

    /// <summary>
    /// Attempts to find a valid rest anchor cell (top-left) for a 2x2 block of Void cells.
    /// </summary>
    /// <param name="context">Boss action context.</param>
    /// <param name="anchor">Top-left anchor cell if found.</param>
    /// <returns>True if a valid anchor was found; otherwise false.</returns>
    private bool TryFindRestAnchor(BossContext context, out Vector3Int anchor)
    {
        anchor = default;

        var anchors = new List<Vector3Int>();
        var voidCells = new HashSet<Vector3Int>();

        var paintedGroundCells = context.grid.GetAllPaintedGroundCells();
        if (paintedGroundCells == null || paintedGroundCells.Count == 0)
            return false;

        // Build a set of all void cells.
        for (int i = 0; i < paintedGroundCells.Count; i++)
        {
            var c = paintedGroundCells[i];
            if (!context.grid.IsPaintedGroundCell(c))
                continue;

            if (context.grid.GetTileKind(c) == TileKind.Void)
                voidCells.Add(c);
        }

        // Find 2x2 void anchors.
        for (int i = 0; i < paintedGroundCells.Count; i++)
        {
            var a = paintedGroundCells[i];
            if (!voidCells.Contains(a))
                continue;

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

    #endregion
    // ─────────────────────────────────────────────
    #region Rest Routine

    /// <summary>
    /// Runs the full rest routine: fly to hover, pause, land, switch to Rest state and wait.
    /// </summary>
    /// <param name="gargoyle">Gargoyle boss controller.</param>
    /// <param name="anchor">Top-left footprint anchor.</param>
    private IEnumerator RestRoutine(GargoyleBossController gargoyle, Vector3Int anchor)
    {
        Vector3 restWorld = gargoyle.GetFootprintCenterWorld(anchor);

        gargoyle.SetState(BossControllerBase.BossState.Flight);

        Vector3 hoverWorld = restWorld + Vector3.up * restHoverHeight;
        yield return gargoyle.FlyToWorldPoint(hoverWorld, GetFlySpeedForPhase(gargoyle));

        if (preRestHoverTime > 0f)
            yield return new WaitForSeconds(preRestHoverTime);

        yield return gargoyle.FlyToWorldPoint(restWorld, GetFlySpeedForPhase(gargoyle), landAnimationTrigger);

        gargoyle.SetState(BossControllerBase.BossState.Rest);
        gargoyle.TookDamageThisRest = false;
        gargoyle.SetContactColliderActive(true);
        gargoyle.PlayAnimation(restAnimationTrigger);

        float t = 0f;
        float duration = GetRestDuration(gargoyle);

        while (t < duration)
        {
            if (gargoyle.State == BossControllerBase.BossState.Dead)
                yield break;

            if (gargoyle.TookDamageThisRest)
                break;

            t += Time.deltaTime;
            yield return null;
        }
        
        gargoyle.SetContactColliderActive(false);
        yield return null;
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Phase Tuning Helpers

    /// <summary>
    /// Returns phase-specific tuning data for the current boss phase.
    /// </summary>
    /// <param name="gargoyle">Gargoyle boss controller.</param>
    /// <returns>Phase data if available; otherwise null.</returns>
    private GargoyleRestPhaseData GetPhaseData(GargoyleBossController gargoyle)
    {
        if (gargoyle == null || gargoyle.BossHealth == null || phaseTuning == null)
            return null;

        return phaseTuning.Get<GargoyleRestPhaseData>(gargoyle.BossHealth.CurrentPhase);
    }

    /// <summary>
    /// Returns fly movement speed for the current phase.
    /// Falls back to a sensible default if tuning is missing.
    /// </summary>
    /// <param name="gargoyle">Gargoyle boss controller.</param>
    /// <returns>Fly speed in world units per second.</returns>
    private float GetFlySpeedForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data != null ? data.flyMoveSpeed : 3.0f;
    }

    /// <summary>
    /// Returns the rest duration for the current phase.
    /// Falls back to a short default if tuning is missing.
    /// </summary>
    /// <param name="gargoyle">Gargoyle boss controller.</param>
    /// <returns>Rest duration in seconds.</returns>
    private float GetRestDuration(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data != null ? data.restDuration : 1.0f;
    }

    #endregion
    // ─────────────────────────────────────────────
}

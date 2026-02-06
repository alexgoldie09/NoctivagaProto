using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss action component that runs the Vampire Queen bull charge phase (Phase 2+).
/// The queen telegraphs a row or column, then charges across it and can be hurt by trap tiles.
/// </summary>
public class VampireQueenChargeAction : BossAction
{
    private enum ChargeAxis
    {
        Row,
        Column
    }

    [Header("Telegraph")]
    [Tooltip("Telegraph color for the charge lane.")]
    [SerializeField] private Color telegraphColor = new(0.8f, 0.1f, 0.1f, 0.75f);

    [Header("Charge Tuning (Phase 2+ Micro Phases)")]
    [SerializeField] private VampireQueenChargePhaseData phaseData;

    [Header("Selection Bias")]
    [Tooltip("Chance to choose the player's row/column when available.")]
    [Range(0f, 1f)]
    [SerializeField] private float playerLanePreference = 0.8f;

    [Header("Telegraph Timing")]
    [Tooltip("If true, the telegraph plays in two steps (decoy flash -> final flash).")]
    [SerializeField] private bool enableTwoStepTelegraph = true;
    [Tooltip("Prefab spawned on each decoy telegraph tile (e.g., poison hazard).")]
    [SerializeField] private GameObject decoyHazardPrefab;

    [Header("Anchors")]
    [Tooltip("World-space anchor used when the boss retreats to recover after a trap hit.")]
    [SerializeField] private Transform recuperationAnchor;

    [Header("Animation")]
    [Tooltip("Animator trigger to play during charge windup.")]
    [SerializeField] private string windupAnimationTrigger = "Summon";
    [Tooltip("Animator trigger to play while charging.")]
    [SerializeField] private string chargeAnimationTrigger = "Fly";
    [Tooltip("Animator trigger to play during the hurt recovery.")]
    [SerializeField] private string hurtAnimationTrigger = "Hurt";
    [Tooltip("Animator trigger to play when returning to idle after charge.")]
    [SerializeField] private string idleAnimationTrigger = "Idle";
    [Tooltip("Seconds to hold on the hurt animation before flying to recuperation.")]
    [SerializeField] private float hurtAnimationHold = 0.35f;

    private struct ChargeLane
    {
        public ChargeAxis axis;
        public int fixedCoord;
        public List<Vector3Int> anchors;
        public Vector3Int startAnchor;
        public Vector3Int endAnchor;
        public List<Vector3Int> telegraphCells;
    }

    private bool hasLastPlayerCell;
    private Vector3Int lastPlayerCell;

    /// <summary>
    /// Returns whether this action can run given the current boss context.
    /// </summary>
    public override bool CanRun(BossContext context)
    {
        if (context?.controller is not VampireQueenBossController queen)
            return false;

        return context.health != null && context.health.CurrentPhase != BossPhase.Phase1;
    }

    /// <summary>
    /// Executes the charge loop: telegraph lane, charge across it, and recover if hurt.
    /// </summary>
    public override IEnumerator Execute(BossContext context)
    {
        var queen = context?.controller as VampireQueenBossController;
        if (queen == null || context.grid == null)
            yield break;

        SetPhaseObjectsActive(true);

        var data = phaseData;
        var micro = phaseData != null
            ? phaseData.GetMicroPhase(queen.MicroPhaseIndex)
            : default;

        float telegraphDuration = data != null ? Mathf.Max(0f, micro.telegraphDuration) : 1.25f;
        float preChargeDelay = data != null ? Mathf.Max(0f, micro.preChargeDelay) : 0.2f;
        float approachSpeed = data != null ? Mathf.Max(0.1f, micro.approachSpeed) : 4f;
        float chargeSpeed = GetChargeSpeed(data, micro);
        float postChargeDelay = data != null ? Mathf.Max(0f, micro.postChargeDelay) : 0.3f;
        float recoveryDuration = data != null ? Mathf.Max(0f, micro.hurtRecoveryDuration) : 5f;
        int trapDamage = data != null ? Mathf.Max(1, micro.trapDamage) : 1;

        if (!TryPickChargeLane(context, out var lane))
            yield break;

        List<Vector3Int> decoyCells = null;
        if (enableTwoStepTelegraph)
            decoyCells = TryPickDecoyTelegraph(context, lane);

        Vector3 startWorld = queen.GetFootprintCenterWorld(lane.startAnchor);

        queen.SetState(BossControllerBase.BossState.Flight);
        yield return queen.FlyToWorldPoint(startWorld, approachSpeed);

        if (!string.IsNullOrEmpty(windupAnimationTrigger))
            queen.PlayAnimation(windupAnimationTrigger);

        if (lane.telegraphCells != null && lane.telegraphCells.Count > 0)
            yield return PlayTelegraphSteps(context, decoyCells, lane.telegraphCells, telegraphDuration);

        if (preChargeDelay > 0f)
            yield return new WaitForSeconds(preChargeDelay);

        queen.SetContactColliderActive(true);
        if (!string.IsNullOrEmpty(chargeAnimationTrigger))
            queen.PlayAnimation(chargeAnimationTrigger);

        bool tookTrapDamage = false;
        yield return ChargeAlongLane(context, queen, lane, chargeSpeed, trapDamage, hit =>
        {
            tookTrapDamage = hit;
        });

        queen.SetContactColliderActive(false);

        if (tookTrapDamage)
        {
            if(queen.BossHealth.CurrentPhase == BossPhase.Phase2)
                queen.AdvanceMicroPhase();
            
            queen.SetState(BossControllerBase.BossState.Hurt);
            if (!string.IsNullOrEmpty(hurtAnimationTrigger))
                queen.PlayAnimation(hurtAnimationTrigger);

            if (recuperationAnchor != null)
            {
                if (hurtAnimationHold > 0f)
                    yield return new WaitForSeconds(hurtAnimationHold);

                yield return queen.FlyToWorldPoint(recuperationAnchor.position, approachSpeed);
                if (!string.IsNullOrEmpty(idleAnimationTrigger))
                    queen.PlayAnimation(idleAnimationTrigger);
            }

            if (recoveryDuration > 0f)
                yield return new WaitForSeconds(recoveryDuration);
        }
        else if (postChargeDelay > 0f)
        {
            yield return new WaitForSeconds(postChargeDelay);
        }

        if (!string.IsNullOrEmpty(idleAnimationTrigger))
            queen.PlayAnimation(idleAnimationTrigger);

        if (context.player != null)
        {
            hasLastPlayerCell = true;
            lastPlayerCell = context.player.CellPosition;
        }
    }

    private float GetChargeSpeed(
        VampireQueenChargePhaseData data,
        VampireQueenChargePhaseData.ChargeMicroPhase micro)
    {
        return data != null ? micro.chargeSpeed : 4.5f;
    }

    private IEnumerator ChargeAlongLane(
        BossContext context,
        VampireQueenBossController queen,
        ChargeLane lane,
        float chargeSpeed,
        int trapDamage,
        System.Action<bool> onTrapHit)
    {
        if (lane.anchors == null || lane.anchors.Count == 0)
            yield break;

        List<Vector3Int> anchors = lane.anchors;
        bool forward = anchors[0] == lane.startAnchor;
        int startIndex = forward ? 0 : anchors.Count - 1;
        int endIndex = forward ? anchors.Count : -1;
        int step = forward ? 1 : -1;

        for (int i = startIndex; i != endIndex; i += step)
        {
            Vector3Int anchor = anchors[i];
            Vector3 targetWorld = queen.GetFootprintCenterWorld(anchor);

            while (Vector3.Distance(queen.transform.position, targetWorld) > 0.05f)
            {
                if (queen.State == BossControllerBase.BossState.Dead)
                    yield break;

                queen.UpdateFacingTowards(targetWorld);
                queen.transform.position = Vector3.MoveTowards(
                    queen.transform.position,
                    targetWorld,
                    chargeSpeed * Time.deltaTime
                );
                yield return null;
            }

            if (IsTrapUnderFootprint(context, queen, anchor))
            {
                queen.ApplyChargeDamage(trapDamage);
                onTrapHit?.Invoke(true);
                yield break;
            }
        }
    }

    private bool IsTrapUnderFootprint(BossContext context, VampireQueenBossController queen, Vector3Int anchor)
    {
        if (context?.grid == null || queen == null)
            return false;

        var footprint = queen.GetFootprintCells(anchor);
        foreach (var cell in footprint)
        {
            var tile = context.grid.GetGroundGameTile(cell);
            if (tile == null)
                continue;

            if (tile.enterEffect != EnterEffect.None || tile.kind == TileKind.Void)
                return true;
        }

        return false;
    }

    private bool TryPickChargeLane(BossContext context, out ChargeLane lane)
    {
        lane = default;

        if (context?.grid == null || context.controller == null)
            return false;

        var candidates = context.grid.GetAllPaintedGroundCells();
        if (candidates == null || candidates.Count == 0)
            return false;

        List<Vector3Int> validAnchors = new();
        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[i];
            if (context.controller.IsFootprintValid(cell))
                validAnchors.Add(cell);
        }

        if (validAnchors.Count == 0)
            return false;

        var anchorsByRow = new Dictionary<int, List<Vector3Int>>();
        var anchorsByColumn = new Dictionary<int, List<Vector3Int>>();

        for (int i = 0; i < validAnchors.Count; i++)
        {
            var anchor = validAnchors[i];
            if (!anchorsByRow.TryGetValue(anchor.y, out var rowList))
            {
                rowList = new List<Vector3Int>();
                anchorsByRow[anchor.y] = rowList;
            }
            rowList.Add(anchor);

            if (!anchorsByColumn.TryGetValue(anchor.x, out var colList))
            {
                colList = new List<Vector3Int>();
                anchorsByColumn[anchor.x] = colList;
            }
            colList.Add(anchor);
        }

        if (anchorsByRow.Count == 0 && anchorsByColumn.Count == 0)
            return false;

        bool hasPlayer = context.player != null;
        Vector3Int playerCell = hasPlayer ? context.player.CellPosition : default;

        int? preferredRow = null;
        if (hasPlayer)
        {
            if (anchorsByRow.ContainsKey(playerCell.y))
                preferredRow = playerCell.y;
            else if (anchorsByRow.ContainsKey(playerCell.y + 1))
                preferredRow = playerCell.y + 1;
        }

        int? preferredColumn = null;
        if (hasPlayer)
        {
            if (anchorsByColumn.ContainsKey(playerCell.x))
                preferredColumn = playerCell.x;
            else if (anchorsByColumn.ContainsKey(playerCell.x - 1))
                preferredColumn = playerCell.x - 1;
        }

        bool chooseRow = anchorsByRow.Count > 0;
        bool chooseColumn = anchorsByColumn.Count > 0;

        ChargeAxis axisChoice;
        if (preferredRow.HasValue && preferredColumn.HasValue)
        {
            axisChoice = Random.value < 0.5f ? ChargeAxis.Row : ChargeAxis.Column;
        }
        else if (preferredRow.HasValue)
        {
            axisChoice = ChargeAxis.Row;
        }
        else if (preferredColumn.HasValue)
        {
            axisChoice = ChargeAxis.Column;
        }
        else if (chooseRow && chooseColumn)
        {
            axisChoice = Random.value < 0.5f ? ChargeAxis.Row : ChargeAxis.Column;
        }
        else
        {
            axisChoice = chooseRow ? ChargeAxis.Row : ChargeAxis.Column;
        }

        if (axisChoice == ChargeAxis.Row && anchorsByRow.Count == 0)
            axisChoice = ChargeAxis.Column;
        if (axisChoice == ChargeAxis.Column && anchorsByColumn.Count == 0)
            axisChoice = ChargeAxis.Row;

        if (axisChoice == ChargeAxis.Row)
        {
            int rowKey = PickPreferredKey(anchorsByRow, preferredRow, playerLanePreference);
            var anchors = anchorsByRow[rowKey];
            anchors.Sort((a, b) => a.x.CompareTo(b.x));

            if (!BuildLaneForRow(anchors, rowKey, playerCell, context, out lane))
                return false;
        }
        else
        {
            int columnKey = PickPreferredKey(anchorsByColumn, preferredColumn, playerLanePreference);
            var anchors = anchorsByColumn[columnKey];
            anchors.Sort((a, b) => a.y.CompareTo(b.y));

            if (!BuildLaneForColumn(anchors, columnKey, playerCell, context, out lane))
                return false;
        }

        return lane.anchors != null && lane.anchors.Count > 0;
    }

    private int PickPreferredKey(Dictionary<int, List<Vector3Int>> lookup, int? preferredKey, float preferenceChance)
    {
        if (preferredKey.HasValue && lookup.ContainsKey(preferredKey.Value) && Random.value <= preferenceChance)
            return preferredKey.Value;

        int index = Random.Range(0, lookup.Count);
        int i = 0;
        foreach (var key in lookup.Keys)
        {
            if (i == index)
                return key;
            i++;
        }

        foreach (var key in lookup.Keys)
            return key;

        return 0;
    }

    private bool BuildLaneForRow(
        List<Vector3Int> anchors,
        int row,
        Vector3Int playerCell, 
        BossContext context,
        out ChargeLane lane)
    {
        lane = default;
        if (anchors == null || anchors.Count == 0)
            return false;

        int minX = anchors[0].x;
        int maxX = anchors[anchors.Count - 1].x;
        float midX = (minX + maxX) * 0.5f;
        bool startFromMin = playerCell.x >= midX;

        lane.axis = ChargeAxis.Row;
        lane.fixedCoord = row;
        lane.anchors = anchors;
        lane.startAnchor = startFromMin ? anchors[0] : anchors[anchors.Count - 1];
        lane.endAnchor = startFromMin ? anchors[anchors.Count - 1] : anchors[0];
        lane.telegraphCells = BuildTelegraphCellsFromAnchors(context, anchors);
        return true;
    }

    private bool BuildLaneForColumn(
        List<Vector3Int> anchors,
        int column,
        Vector3Int playerCell,
        BossContext context,
        out ChargeLane lane)
    {
        lane = default;
        if (anchors == null || anchors.Count == 0)
            return false;

        int minY = anchors[0].y;
        int maxY = anchors[anchors.Count - 1].y;
        float midY = (minY + maxY) * 0.5f;
        bool startFromMin = playerCell.y >= midY;

        lane.axis = ChargeAxis.Column;
        lane.fixedCoord = column;
        lane.anchors = anchors;
        lane.startAnchor = startFromMin ? anchors[0] : anchors[anchors.Count - 1];
        lane.endAnchor = startFromMin ? anchors[anchors.Count - 1] : anchors[0];
        lane.telegraphCells = BuildTelegraphCellsFromAnchors(context, anchors);
        return true;
    }

    private List<Vector3Int> BuildTelegraphCellsFromAnchors(BossContext context, List<Vector3Int> anchors)
    {
        var result = new HashSet<Vector3Int>();
        if (anchors == null || context?.grid == null)
            return new List<Vector3Int>();

        var queen = GetComponent<VampireQueenBossController>();
        if (queen == null)
            return new List<Vector3Int>();

        for (int i = 0; i < anchors.Count; i++)
        {
            var footprint = queen.GetFootprintCells(anchors[i]);
            foreach (var cell in footprint)
            {
                if (context.grid.GetGroundGameTile(cell) != null)
                    result.Add(cell);
            }
        }

        return new List<Vector3Int>(result);
    }

    private IEnumerator PlayTelegraphSteps(
        BossContext context,
        List<Vector3Int> decoyCells,
        List<Vector3Int> finalCells,
        float duration)
    {
        if (context?.grid == null || finalCells == null || finalCells.Count == 0)
            yield break;

        if (!enableTwoStepTelegraph || duration <= 0f)
        {
            context.grid.SetTelegraphCellsForOwner(
                VampireQueenBossController.PREVIEW_OWNER_BOSS,
                finalCells,
                telegraphColor
            );
            if (duration > 0f)
                yield return new WaitForSeconds(duration);
            context.grid.ClearTelegraphForOwner(VampireQueenBossController.PREVIEW_OWNER_BOSS);
            yield break;
        }

        if (decoyCells == null || decoyCells.Count == 0)
        {
            context.grid.SetTelegraphCellsForOwner(
                VampireQueenBossController.PREVIEW_OWNER_BOSS,
                finalCells,
                telegraphColor
            );
            if (duration > 0f)
                yield return new WaitForSeconds(duration);
            context.grid.ClearTelegraphForOwner(VampireQueenBossController.PREVIEW_OWNER_BOSS);
            yield break;
        }

        context.grid.SetTelegraphCellsForOwner(
            VampireQueenBossController.PREVIEW_OWNER_BOSS,
            decoyCells,
            telegraphColor
        );
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        SpawnDecoyHazards(context, decoyCells);

        context.grid.ClearTelegraphForOwner(VampireQueenBossController.PREVIEW_OWNER_BOSS);

        context.grid.SetTelegraphCellsForOwner(
            VampireQueenBossController.PREVIEW_OWNER_BOSS,
            finalCells,
            telegraphColor
        );
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        context.grid.ClearTelegraphForOwner(VampireQueenBossController.PREVIEW_OWNER_BOSS);
    }

    private List<Vector3Int> TryPickDecoyTelegraph(BossContext context, ChargeLane currentLane)
    {
        if (context?.grid == null || context.controller == null)
            return null;

        var candidates = context.grid.GetAllPaintedGroundCells();
        if (candidates == null || candidates.Count == 0)
            return null;

        var anchorsByRow = new Dictionary<int, List<Vector3Int>>();
        var anchorsByColumn = new Dictionary<int, List<Vector3Int>>();

        for (int i = 0; i < candidates.Count; i++)
        {
            var anchor = candidates[i];
            if (!anchorsByRow.TryGetValue(anchor.y, out var rowList))
            {
                rowList = new List<Vector3Int>();
                anchorsByRow[anchor.y] = rowList;
            }
            rowList.Add(anchor);

            if (!anchorsByColumn.TryGetValue(anchor.x, out var colList))
            {
                colList = new List<Vector3Int>();
                anchorsByColumn[anchor.x] = colList;
            }
            colList.Add(anchor);
        }

        bool hasPlayer = context.player != null;
        Vector3Int playerCell = hasPlayer ? context.player.CellPosition : default;
        bool playerStayedPut = hasPlayer && hasLastPlayerCell && playerCell == lastPlayerCell;

        int? preferredRow = null;
        if (hasPlayer)
        {
            if (anchorsByRow.ContainsKey(playerCell.y))
                preferredRow = playerCell.y;
            else if (anchorsByRow.ContainsKey(playerCell.y + 1))
                preferredRow = playerCell.y + 1;
        }

        int? preferredColumn = null;
        if (hasPlayer)
        {
            if (anchorsByColumn.ContainsKey(playerCell.x))
                preferredColumn = playerCell.x;
            else if (anchorsByColumn.ContainsKey(playerCell.x - 1))
                preferredColumn = playerCell.x - 1;
        }

        if (!playerStayedPut)
        {
            if (currentLane.axis == ChargeAxis.Row)
                anchorsByRow.Remove(currentLane.fixedCoord);
            else
                anchorsByColumn.Remove(currentLane.fixedCoord);
        }

        if (anchorsByRow.Count == 0 && anchorsByColumn.Count == 0)
            return null;

        bool chooseRow = anchorsByRow.Count > 0;
        bool chooseColumn = anchorsByColumn.Count > 0;

        ChargeAxis axisChoice;
        if (preferredRow.HasValue && preferredColumn.HasValue)
        {
            axisChoice = Random.value < 0.5f ? ChargeAxis.Row : ChargeAxis.Column;
        }
        else if (preferredRow.HasValue)
        {
            axisChoice = ChargeAxis.Row;
        }
        else if (preferredColumn.HasValue)
        {
            axisChoice = ChargeAxis.Column;
        }
        else if (chooseRow && chooseColumn)
        {
            axisChoice = Random.value < 0.5f ? ChargeAxis.Row : ChargeAxis.Column;
        }
        else
        {
            axisChoice = chooseRow ? ChargeAxis.Row : ChargeAxis.Column;
        }

        if (axisChoice == ChargeAxis.Row && anchorsByRow.Count == 0)
            axisChoice = ChargeAxis.Column;
        if (axisChoice == ChargeAxis.Column && anchorsByColumn.Count == 0)
            axisChoice = ChargeAxis.Row;

        if (axisChoice == ChargeAxis.Row)
        {
            int rowKey = PickPreferredKey(anchorsByRow, preferredRow, playerLanePreference);
            var anchors = anchorsByRow[rowKey];
            anchors.Sort((a, b) => a.x.CompareTo(b.x));
            return BuildTelegraphCellsFromAnchors(context, anchors);
        }

        int columnKey = PickPreferredKey(anchorsByColumn, preferredColumn, playerLanePreference);
        var columnAnchors = anchorsByColumn[columnKey];
        columnAnchors.Sort((a, b) => a.y.CompareTo(b.y));
        return BuildTelegraphCellsFromAnchors(context, columnAnchors);
    }

    private void SpawnDecoyHazards(BossContext context, List<Vector3Int> cells)
    {
        if (decoyHazardPrefab == null || context?.grid == null || cells == null)
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 world = context.grid.CellToWorldCenter(cells[i]);
            var instance = Instantiate(decoyHazardPrefab, world, Quaternion.identity);
            if (instance == null)
                continue;

            var hazard = instance.GetComponent<VampireQueenDecoyHazard>();
            if (hazard != null)
                hazard.Arm();
        }
    }

}

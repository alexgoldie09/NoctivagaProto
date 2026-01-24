using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss action component that runs the gargoyle bombing sequence.
/// </summary>
public class GargoyleBombingAction : BossAction
{
    private enum BombAoePattern { Square, Cross }

    [Header("References")]
    [SerializeField] private Transform bombSpawnPoint;
    [SerializeField] private GameObject bombProjectilePrefab;
    [SerializeField] private GameObject bombExplosionVfxPrefab;
    [SerializeField] private GameObject shapePickupPrefab;

    [Header("Bomb Area-of-Effect")]
    [Range(0f, 1f)]
    [SerializeField] private float aoeChance = 0.45f;

    [Min(0)]
    [SerializeField] private int aoeRadius = 1;

    [Range(0f, 1f)]
    [SerializeField] private float crossPatternChance = 0.5f;

    [Tooltip("Optional cap on how many cells an AoE can affect (0 = no cap).")]
    [SerializeField] private int aoeMaxCells = 0;

    [Header("Phase Tuning")]
    [SerializeField] private BossPhaseTuning phaseTuning;

    [Header("Bomb Targeting - Near Bucket")]
    [SerializeField] private int nearRadius = 6;
    [SerializeField, Range(0f, 1f)] private float nearPickChance = 0.8f;
    [SerializeField] private int minDistanceFromPlayer = 0;
    [SerializeField] private bool allowBombingStartTile = true;
    [SerializeField] private bool allowBombPlayerCell = true;

    [Header("Bomb Delivery - Fly then Throw")]
    [SerializeField] private float hoverHeight = 2.0f;
    [SerializeField] private float preThrowHoverTime = 0.10f;
    [SerializeField] private float throwWindupTime = 0.12f;
    [SerializeField] private float arriveEpsilon = 0.05f;

    [Header("Bomb Cycle Feel")]
    [SerializeField] private float postThrowRecoverTime = 0.25f;
    [SerializeField] private float betweenBombMoveDelay = 0.15f;

    [Header("Shape Drop")]
    [Range(0f, 1f)]
    [SerializeField] private float shapeDropChance = 0.6f;

    [Header("Telegraph Color")]
    [SerializeField] private Color bombTelegraphColor = new (1f, 0.2f, 0.2f, 0.9f);

    /// <summary>
    /// Returns true if the action has a valid gargoyle controller.
    /// </summary>
    public override bool CanRun(BossContext context)
    {
        return context?.controller is GargoyleBossController;
    }

    /// <summary>
    /// Executes the gargoyle bombing sequence.
    /// </summary>
    public override IEnumerator Execute(BossContext context)
    {
        var gargoyle = context?.controller as GargoyleBossController;
        if (gargoyle == null || context.grid == null)
            yield break;

        gargoyle.SetState(BossControllerBase.BossState.Flight);
        gargoyle.PlayAnimation("Fly");

        int bombsToDrop = GetBombQuotaForPhase(gargoyle);

        for (int i = 0; i < bombsToDrop; i++)
        {
            if (gargoyle.State == BossControllerBase.BossState.Dead) yield break;

            yield return BombCycle(gargoyle, context);

            if (betweenBombMoveDelay > 0f)
                yield return new WaitForSeconds(betweenBombMoveDelay);
        }
    }

    private IEnumerator BombCycle(GargoyleBossController gargoyle, BossContext context)
    {
        float bombInterval = GetBombIntervalForPhase(gargoyle);
        float telegraph = GetTelegraphForPhase(gargoyle);

        if (!TryPickBombTarget(context, out Vector3Int targetCell))
            yield break;

        Vector3 targetWorld = context.grid.CellToWorldCenter(targetCell);
        Vector3 hoverWorld = targetWorld + Vector3.up * hoverHeight;

        gargoyle.SetState(BossControllerBase.BossState.Flight);
        gargoyle.PlayAnimation("Fly");
        yield return FlyToWorldPoint(gargoyle, context, hoverWorld);

        if (preThrowHoverTime > 0f)
            yield return new WaitForSeconds(preThrowHoverTime);

        bool isAoe = aoeRadius > 0 && Random.value <= aoeChance;

        BombAoePattern pattern = BombAoePattern.Square;
        if (isAoe)
            pattern = Random.value <= crossPatternChance ? BombAoePattern.Cross : BombAoePattern.Square;

        List<Vector3Int> affected = GetBombAffectedCells(context, targetCell, isAoe, pattern);

        context.grid.FlashTelegraphCellsForOwner(
            GargoyleBossController.PREVIEW_OWNER_BOSS,
            affected,
            bombTelegraphColor,
            telegraph
        );

        gargoyle.PlayAnimation("ThrowBomb");

        float windup = Mathf.Clamp(throwWindupTime, 0f, telegraph);
        if (windup > 0f)
            yield return new WaitForSeconds(windup);

        float travelTime = Mathf.Max(0.05f, telegraph - windup);
        SpawnBombProjectile(context, targetCell, travelTime);

        if (travelTime > 0f)
            yield return new WaitForSeconds(travelTime);

        DetonateBombCells(context, affected, targetCell);

        gargoyle.PlayAnimation("Idle");
        if (postThrowRecoverTime > 0f)
            yield return new WaitForSeconds(postThrowRecoverTime);

        if (bombInterval > 0f)
            yield return new WaitForSeconds(bombInterval);
    }

    private IEnumerator FlyToWorldPoint(GargoyleBossController gargoyle, BossContext context, Vector3 targetWorld)
    {
        gargoyle.PlayAnimation("Fly");

        while (Vector3.Distance(gargoyle.transform.position, targetWorld) > arriveEpsilon)
        {
            if (gargoyle.State == BossControllerBase.BossState.Dead) yield break;

            gargoyle.UpdateFacingTowards(targetWorld);

            gargoyle.transform.position = Vector3.MoveTowards(
                gargoyle.transform.position,
                targetWorld,
                GetFlySpeedForPhase(gargoyle) * Time.deltaTime
            );

            yield return null;
        }
    }

    private bool TryPickBombTarget(BossContext context, out Vector3Int cell)
    {
        cell = default;

        if (context.grid == null)
            return false;

        var paintedGroundCells = context.grid.GetAllPaintedGroundCells();
        if (paintedGroundCells == null || paintedGroundCells.Count == 0)
            return false;

        bool hasPlayer = context.player != null;
        Vector3Int playerCell = hasPlayer ? context.player.CellPosition : default;

        List<Vector3Int> near = new List<Vector3Int>(64);
        List<Vector3Int> far = new List<Vector3Int>(128);

        for (int i = 0; i < paintedGroundCells.Count; i++)
        {
            var c = paintedGroundCells[i];

            if (!context.grid.IsPaintedGroundCell(c))
                continue;

            TileKind k = context.grid.GetTileKind(c);

            bool isValidKind =
                k == TileKind.Floor ||
                (allowBombingStartTile && k == TileKind.Start);

            if (!isValidKind)
                continue;

            if (hasPlayer)
            {
                int dist = Mathf.Abs(c.x - playerCell.x) + Mathf.Abs(c.y - playerCell.y);

                if (minDistanceFromPlayer > 0 && dist < minDistanceFromPlayer)
                    continue;

                if (!allowBombPlayerCell && dist == 0)
                    continue;

                if (nearRadius > 0 && dist <= nearRadius)
                    near.Add(c);
                else
                    far.Add(c);
            }
            else
            {
                far.Add(c);
            }
        }

        if (near.Count > 0 && Random.value <= nearPickChance)
        {
            cell = near[Random.Range(0, near.Count)];
            return true;
        }

        if (far.Count > 0)
        {
            cell = far[Random.Range(0, far.Count)];
            return true;
        }

        if (near.Count > 0)
        {
            cell = near[Random.Range(0, near.Count)];
            return true;
        }

        return false;
    }

    private void SpawnBombProjectile(BossContext context, Vector3Int targetCell, float travelTime)
    {
        if (bombProjectilePrefab == null || bombSpawnPoint == null)
            return;

        Vector3 start = bombSpawnPoint.position;
        Vector3 end = context.grid.CellToWorldCenter(targetCell);

        var go = Object.Instantiate(bombProjectilePrefab, start, Quaternion.identity);
        StartCoroutine(MoveProjectileArc(go.transform, start, end, travelTime));
    }

    private IEnumerator MoveProjectileArc(Transform proj, Vector3 start, Vector3 end, float duration)
    {
        if (proj == null) yield break;

        float t = 0f;
        float height = 1.5f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);

            Vector3 p = Vector3.Lerp(start, end, a);
            float arc = 4f * height * a * (1f - a);
            p.y += arc;

            proj.position = p;
            yield return null;
        }

        if (proj != null)
            Object.Destroy(proj.gameObject);
    }

    private bool TrySpawnShapePickupNear(BossContext context, Vector3Int bombCell)
    {
        var options = new List<Vector3Int>(4)
        {
            bombCell + new Vector3Int(1, 0, 0),
            bombCell + new Vector3Int(-1, 0, 0),
            bombCell + new Vector3Int(0, 1, 0),
            bombCell + new Vector3Int(0, -1, 0),
        };

        for (int i = 0; i < options.Count; i++)
        {
            int j = Random.Range(i, options.Count);
            (options[i], options[j]) = (options[j], options[i]);
        }

        for (int i = 0; i < options.Count; i++)
        {
            var c = options[i];
            if (!context.grid.IsPaintedGroundCell(c)) continue;
            if (context.grid.GetTileKind(c) != TileKind.Floor) continue;
            if (context.player != null && c == context.player.CellPosition) continue;

            Vector3 w = context.grid.CellToWorldCenter(c);
            Object.Instantiate(shapePickupPrefab, w, Quaternion.identity);
            return true;
        }

        for (int tries = 0; tries < 25; tries++)
        {
            var paintedCells = context.grid.GetAllPaintedGroundCells();
            if (paintedCells == null || paintedCells.Count == 0)
                return false;
            var c = paintedCells[Random.Range(0, paintedCells.Count)];
            if (!context.grid.IsPaintedGroundCell(c)) continue;
            if (context.grid.GetTileKind(c) != TileKind.Floor) continue;
            if (context.player != null && c == context.player.CellPosition) continue;

            Vector3 w = context.grid.CellToWorldCenter(c);
            Object.Instantiate(shapePickupPrefab, w, Quaternion.identity);
            return true;
        }

        return false;
    }

    private List<Vector3Int> GetBombAffectedCells(
        BossContext context,
        Vector3Int center,
        bool isAoe,
        BombAoePattern pattern
    )
    {
        var cells = new List<Vector3Int>();

        if (!context.grid.IsPaintedGroundCell(center))
            return cells;

        if (!isAoe || aoeRadius <= 0)
        {
            cells.Add(center);
            return cells;
        }

        var set = new HashSet<Vector3Int>();

        if (pattern == BombAoePattern.Cross)
        {
            set.Add(center);
            for (int r = 1; r <= aoeRadius; r++)
            {
                set.Add(center + new Vector3Int(r, 0, 0));
                set.Add(center + new Vector3Int(-r, 0, 0));
                set.Add(center + new Vector3Int(0, r, 0));
                set.Add(center + new Vector3Int(0, -r, 0));
            }
        }
        else
        {
            for (int dx = -aoeRadius; dx <= aoeRadius; dx++)
            for (int dy = -aoeRadius; dy <= aoeRadius; dy++)
                set.Add(center + new Vector3Int(dx, dy, 0));
        }

        foreach (var c in set)
        {
            if (!context.grid.IsPaintedGroundCell(c)) continue;
            if (context.grid.GetTileKind(c) != TileKind.Floor && context.grid.GetTileKind(c) != TileKind.Start) continue;

            cells.Add(c);
        }

        if (aoeMaxCells > 0 && cells.Count > aoeMaxCells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                int j = Random.Range(i, cells.Count);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            cells.RemoveRange(aoeMaxCells, cells.Count - aoeMaxCells);
        }

        if (cells.Count == 0 && context.grid.GetTileKind(center) == TileKind.Floor)
            cells.Add(center);

        return cells;
    }

    private void DetonateBombCells(
        BossContext context,
        IReadOnlyList<Vector3Int> cells,
        Vector3Int centerCell
    )
    {
        if (cells == null || cells.Count == 0)
            return;

        if (context.controller != null && context.controller.AllowDamageShake && context.controller.DamageShakeForce > 0f)
            CameraShake.Instance?.Shake(context.controller.DamageShakeForce);

        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];

            if (context.grid.GetTileKind(c) == TileKind.Floor)
                context.grid.ToggleFloorVoidAt(c);

            if (bombExplosionVfxPrefab != null)
            {
                Vector3 w = context.grid.CellToWorldCenter(c);
                Object.Instantiate(bombExplosionVfxPrefab, w, Quaternion.identity);
            }

            if (context.player != null && context.player.CellPosition == c)
            {
                Vector3 w = context.grid.CellToWorldCenter(c);
                context.player.StartVoidFallDeath(w);
            }
        }

        if (shapePickupPrefab != null && Random.value <= shapeDropChance)
            TrySpawnShapePickupNear(context, centerCell);
    }

    private GargoyleBombingPhaseData GetPhaseData(GargoyleBossController gargoyle)
    {
        if (gargoyle == null || gargoyle.BossHealth == null || phaseTuning == null)
            return null;

        return phaseTuning.Get<GargoyleBombingPhaseData>(gargoyle.BossHealth.CurrentPhase);
    }

    private int GetBombQuotaForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data.bombsPerFlight;
    }

    private float GetBombIntervalForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data.bombInterval;
    }

    private float GetTelegraphForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data.telegraphDuration;
    }

    private float GetFlySpeedForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data.flyMoveSpeed;
    }
}

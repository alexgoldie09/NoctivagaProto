using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss action component that runs the Gargoyle bombing sequence.
/// Handles target selection, optional AoE patterning, telegraphing on the telegraph/preview layer,
/// projectile throw timing, detonation, and optional shape pickup drops.
/// </summary>
public class GargoyleBombingAction : BossAction
{
    /// <summary>
    /// Supported AoE patterns for the bomb detonation.
    /// </summary>
    private enum BombAoePattern { Square, Cross }

    [Header("References")]
    [Tooltip("World-space spawn point used as the bomb projectile origin (eg. the gargoyle's hand).")]
    [SerializeField] private Transform bombSpawnPoint;
    [Tooltip("Projectile prefab that arcs from spawn point to the target cell.")]
    [SerializeField] private GameObject bombProjectilePrefab;
    [Tooltip("VFX prefab spawned per affected cell at detonation time.")]
    [SerializeField] private GameObject bombExplosionVfxPrefab;
    [Tooltip("Optional pickup prefab spawned after detonation based on Shape Drop chance.")]
    [SerializeField] private GameObject shapePickupPrefab;
    [Tooltip("Void game tile to swap in for floor during bomb.")]
    [SerializeField] private GameTile voidTile;

    [Header("Bomb Area-of-Effect")]
    [Tooltip("Chance the bomb will use an AoE pattern (if AoE Radius > 0).")]
    [Range(0f, 1f)]
    [SerializeField] private float aoeChance = 0.45f;
    [Tooltip("AoE radius in cells for pattern-based bombs. 0 = single-cell bomb.")]
    [Min(0)]
    [SerializeField] private int aoeRadius = 1;
    [Tooltip("When AoE is used, chance to pick Cross pattern instead of Square.")]
    [Range(0f, 1f)]
    [SerializeField] private float crossPatternChance = 0.5f;
    [Tooltip("Optional cap on how many cells an AoE can affect (0 = no cap).")]
    [SerializeField] private int aoeMaxCells = 0;

    [Header("Phase Tuning")]
    [Tooltip("Phase tuning asset that returns bombing settings per boss phase.")]
    [SerializeField] private BossPhaseTuning phaseTuning;

    [Header("Bomb Targeting - Near Bucket")]
    [Tooltip("Manhattan radius considered 'near' the player (0 disables near bucket).")]
    [SerializeField] private int nearRadius = 6;
    [Tooltip("Chance to pick from the near bucket if it has any candidates.")]
    [SerializeField, Range(0f, 1f)] private float nearPickChance = 0.8f;
    [Tooltip("Minimum manhattan distance from the player required to be a valid target (0 = none).")]
    [SerializeField] private int minDistanceFromPlayer = 0;
    [Tooltip("If true, the boss may bomb the Start tile kind.")]
    [SerializeField] private bool allowBombingStartTile = true;
    [Tooltip("If true, the boss may bomb the player's current cell.")]
    [SerializeField] private bool allowBombPlayerCell = true;

    [Header("Bomb Delivery - Fly then Throw")]
    [Tooltip("Vertical hover offset above the target cell before throwing.")]
    [SerializeField] private float hoverHeight = 2.0f;
    [Tooltip("Pause time after arriving at hover point, before starting throw windup.")]
    [SerializeField] private float preThrowHoverTime = 0.10f;
    [Tooltip("Animation windup time before the projectile is spawned (clamped to telegraph duration).")]
    [SerializeField] private float throwWindupTime = 0.12f;

    [Header("Bomb Cycle Feel")]
    [Tooltip("Pause after detonation / throw before moving to the next bomb cycle.")]
    [SerializeField] private float postThrowRecoverTime = 0.25f;
    [Tooltip("Extra delay inserted between bomb cycles (movement feel).")]
    [SerializeField] private float betweenBombMoveDelay = 0.15f;

    [Header("Shape Drop")]
    [Tooltip("Chance to spawn a shape pickup near the bomb's center cell.")]
    [Range(0f, 1f)]
    [SerializeField] private float shapeDropChance = 0.6f;

    [Header("Telegraph Color")]
    [Tooltip("Color used for the bomb telegraph preview overlay.")]
    [SerializeField] private Color bombTelegraphColor = new(1f, 0.2f, 0.2f, 0.9f);
    
    [Header("Animation")]
    [Tooltip("Animator trigger to play throwing a bomb.")]
    [SerializeField] private string throwBombAnimationTrigger = "ThrowBomb";
    [Tooltip("Animator trigger to return to idle between bombs.")]
    [SerializeField] private string idleAnimationTrigger = "Idle";

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
    /// Executes the gargoyle bombing sequence: flies to targets, telegraphs impacted cells, throws a projectile,
    /// then detonates (voiding floor cells, spawning VFX, and optionally dropping a pickup).
    /// </summary>
    /// <param name="context">Boss action context.</param>
    /// <returns>Coroutine enumerator.</returns>
    public override IEnumerator Execute(BossContext context)
    {
        var gargoyle = context?.controller as GargoyleBossController;
        if (gargoyle == null || context.grid == null)
            yield break;

        gargoyle.SetState(BossControllerBase.BossState.Flight);

        int bombsToDrop = GetBombQuotaForPhase(gargoyle);

        for (int i = 0; i < bombsToDrop; i++)
        {
            if (gargoyle.State == BossControllerBase.BossState.Dead)
                yield break;

            yield return BombCycle(gargoyle, context);

            if (betweenBombMoveDelay > 0f)
                yield return new WaitForSeconds(betweenBombMoveDelay);
        }
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Bomb Cycle

    /// <summary>
    /// Single bomb cycle: choose target, fly to hover point, telegraph impacted cells, throw projectile, detonate.
    /// </summary>
    private IEnumerator BombCycle(GargoyleBossController gargoyle, BossContext context)
    {
        float bombInterval = GetBombIntervalForPhase(gargoyle);
        float telegraph = GetTelegraphForPhase(gargoyle);

        if (!TryPickBombTarget(context, out Vector3Int targetCell))
            yield break;

        Vector3 targetWorld = context.grid.CellToWorldCenter(targetCell);
        Vector3 hoverWorld = targetWorld + Vector3.up * hoverHeight;

        gargoyle.SetState(BossControllerBase.BossState.Flight);
        yield return gargoyle.FlyToWorldPoint(hoverWorld, GetFlySpeedForPhase(gargoyle));

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

        gargoyle.PlayAnimation(throwBombAnimationTrigger);

        float windup = Mathf.Clamp(throwWindupTime, 0f, telegraph);
        if (windup > 0f)
            yield return new WaitForSeconds(windup);

        float travelTime = Mathf.Max(0.05f, telegraph - windup);
        SpawnBombProjectile(context, targetCell, travelTime);

        if (travelTime > 0f)
            yield return new WaitForSeconds(travelTime);

        DetonateBombCells(context, affected, targetCell);

        gargoyle.PlayAnimation(idleAnimationTrigger);

        if (postThrowRecoverTime > 0f)
            yield return new WaitForSeconds(postThrowRecoverTime);

        if (bombInterval > 0f)
            yield return new WaitForSeconds(bombInterval);
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Target Selection

    /// <summary>
    /// Picks a valid bomb target cell from painted ground cells, with a near/far bucket bias.
    /// Applies TileKind filtering and player distance constraints.
    /// </summary>
    /// <param name="context">Boss action context.</param>
    /// <param name="cell">Chosen target cell if successful.</param>
    /// <returns>True if a valid target was found.</returns>
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

        List<Vector3Int> near = new(64);
        List<Vector3Int> far = new(128);

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

    #endregion
    // ─────────────────────────────────────────────
    #region Projectile / Detonation

    /// <summary>
    /// Spawns the bomb projectile and starts an arc movement coroutine toward the target cell.
    /// </summary>
    private void SpawnBombProjectile(BossContext context, Vector3Int targetCell, float travelTime)
    {
        if (bombProjectilePrefab == null || bombSpawnPoint == null)
            return;

        Vector3 start = bombSpawnPoint.position;
        Vector3 end = context.grid.CellToWorldCenter(targetCell);

        var go = Instantiate(bombProjectilePrefab, start, Quaternion.identity);
        StartCoroutine(MoveProjectileArc(go.transform, start, end, travelTime));
    }

    /// <summary>
    /// Moves a projectile transform along a simple parabola arc between two points.
    /// </summary>
    private IEnumerator MoveProjectileArc(Transform proj, Vector3 start, Vector3 end, float duration)
    {
        if (proj == null)
            yield break;

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
            Destroy(proj.gameObject);
    }

    /// <summary>
    /// Detonates all affected cells: toggles floor void, spawns VFX, and kills the player if impacted.
    /// Also optionally spawns a shape pickup near the center cell.
    /// </summary>
    private void DetonateBombCells(BossContext context, IReadOnlyList<Vector3Int> cells, Vector3Int centerCell)
    {
        if (cells == null || cells.Count == 0)
            return;

        if (context.controller != null && context.controller.AllowDamageShake && context.controller.DamageShakeForce > 0f)
            CameraShake.Instance?.Shake(context.controller.DamageShakeForce);

        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];

            if (context.grid.GetTileKind(c) == TileKind.Floor)
                context.grid.ToggleFloorVoidAt(c, voidTile);

            if (bombExplosionVfxPrefab != null)
            {
                Vector3 w = context.grid.CellToWorldCenter(c);
                Instantiate(bombExplosionVfxPrefab, w, Quaternion.identity);
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

    #endregion
    // ─────────────────────────────────────────────
    #region AoE Helpers

    /// <summary>
    /// Returns the set of cells impacted by the bomb for the chosen pattern and radius.
    /// Results are filtered to painted ground cells and valid TileKinds (Floor / Start).
    /// </summary>
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

            TileKind kind = context.grid.GetTileKind(c);
            if (kind != TileKind.Floor && kind != TileKind.Start) continue;

            cells.Add(c);
        }

        if (aoeMaxCells > 0 && cells.Count > aoeMaxCells)
        {
            // Shuffle then cap.
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

    /// <summary>
    /// Attempts to spawn a shape pickup near the detonation center, preferring adjacent valid floor cells.
    /// Falls back to random painted floor selection if adjacency fails.
    /// </summary>
    private bool TrySpawnShapePickupNear(BossContext context, Vector3Int bombCell)
    {
        var options = new List<Vector3Int>(4)
        {
            bombCell + new Vector3Int(1, 0, 0),
            bombCell + new Vector3Int(-1, 0, 0),
            bombCell + new Vector3Int(0, 1, 0),
            bombCell + new Vector3Int(0, -1, 0),
        };

        // Shuffle options so adjacency selection is random.
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
            Instantiate(shapePickupPrefab, w, Quaternion.identity);
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
            Instantiate(shapePickupPrefab, w, Quaternion.identity);
            return true;
        }

        return false;
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Phase Tuning Helpers

    /// <summary>
    /// Returns phase-specific tuning data for the current boss phase.
    /// </summary>
    /// <param name="gargoyle">Gargoyle boss controller.</param>
    /// <returns>Phase data if available; otherwise null.</returns>
    private GargoyleBombingPhaseData GetPhaseData(GargoyleBossController gargoyle)
    {
        if (gargoyle == null || gargoyle.BossHealth == null || phaseTuning == null)
            return null;

        return phaseTuning.Get<GargoyleBombingPhaseData>(gargoyle.BossHealth.CurrentPhase);
    }

    /// <summary>
    /// Returns bomb quota per Flight for the current phase. Falls back to 3 if tuning is missing.
    /// </summary>
    private int GetBombQuotaForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data != null ? data.bombsPerFlight : 3;
    }

    /// <summary>
    /// Returns delay between bombs for the current phase. Falls back to 1 if tuning is missing.
    /// </summary>
    private float GetBombIntervalForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data != null ? data.bombInterval : 1f;
    }

    /// <summary>
    /// Returns telegraph duration for the current phase. Falls back to a large default if tuning is missing.
    /// </summary>
    private float GetTelegraphForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data != null ? data.telegraphDuration : 0.8f;
    }

    /// <summary>
    /// Returns fly movement speed for the current phase. Falls back to a sensible default if tuning is missing.
    /// </summary>
    private float GetFlySpeedForPhase(GargoyleBossController gargoyle)
    {
        var data = GetPhaseData(gargoyle);
        return data != null ? data.flyMoveSpeed : 6.0f;
    }

    #endregion
    // ─────────────────────────────────────────────
}

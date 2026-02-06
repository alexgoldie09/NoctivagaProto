using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Phase 3 action for the Vampire Queen: swaps in the meteor body, enables a shield,
/// and repeatedly drops meteors that void floor tiles while scaling bomb speed with health.
/// </summary>
public class VampireQueenMeteorFieldAction : BossAction
{
    [Header("Body Swap")]
    [Tooltip("Existing body root to disable when phase 3 begins.")]
    [SerializeField] private GameObject previousBodyRoot;
    [Tooltip("Meteor body root to enable for phase 3.")]
    [SerializeField] private GameObject meteorBodyRoot;
    [Tooltip("Animator for the meteor body (optional override).")]
    [SerializeField] private Animator meteorAnimator;
    [Tooltip("SpriteRenderer for the meteor body (optional override).")]
    [SerializeField] private SpriteRenderer meteorSpriteRenderer;
    [Tooltip("Delay before the body swap begins (gives player time to react).")]
    [SerializeField] private float swapDelay = 0.6f;
    [Tooltip("Vertical drop height above the landing point.")]
    [SerializeField] private float entryHeight = 6f;
    [Tooltip("Move speed while dropping into the arena.")]
    [SerializeField] private float entrySpeed = 8f;
    [Tooltip("Top-left anchor marker for the phase 3 body footprint.")]
    [SerializeField] private Transform landingAnchorMarker;
    [Tooltip("Contact collider used for the phase 3 body.")]
    [SerializeField] private Collider2D meteorContactCollider;
    
    [Header("Melee Powerup")]
    [Tooltip("Powerup prefab spawned when the meteor phase begins.")]
    [SerializeField] private GameObject meleePowerupPrefab;

    [Header("Meteor Bombing")]
    [Tooltip("Projectile prefab that falls straight down onto the target cell.")]
    [SerializeField] private GameObject meteorProjectilePrefab;
    [Tooltip("Impact VFX prefab spawned on detonation.")]
    [SerializeField] private GameObject meteorImpactVfxPrefab;
    [Tooltip("Void game tile to swap in for floor during meteor impacts.")]
    [SerializeField] private GameTile voidTile;
    [Tooltip("Color used for the telegraph preview overlay.")]
    [SerializeField] private Color telegraphColor = new(1f, 0.3f, 0.2f, 0.9f);

    [Header("Meteor Timing (scaled by boss health)")]
    [Tooltip("Bomb interval when boss is at full health (seconds).")]
    [SerializeField] private float maxBombInterval = 1.1f;
    [Tooltip("Bomb interval when boss is at low health (seconds).")]
    [SerializeField] private float minBombInterval = 0.35f;
    [Tooltip("Fall speed when boss is at full health (world units/sec).")]
    [SerializeField] private float minFallSpeed = 6f;
    [Tooltip("Fall speed when boss is at low health (world units/sec).")]
    [SerializeField] private float maxFallSpeed = 14f;
    [Tooltip("Minimum telegraph duration (seconds).")]
    [SerializeField] private float minTelegraphDuration = 0.2f;

    [Header("Targeting")]
    [Tooltip("If true, meteors cannot target Start tiles.")]
    [SerializeField] private bool disallowStartTiles = false;
    [Tooltip("Markers that define untargetable spaces (e.g., shield area).")]
    [SerializeField] private List<Transform> untargetableMarkers = new();
    [Tooltip("Optional transform whose position/scale defines the occupied area.")]
    [SerializeField] private Transform occupiedAreaTransform;
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawLabels = true;
    
    [Header("Post FX Hue Shift (Optional)")]
    [SerializeField] private bool animateHueShift = true;
    [SerializeField, Range(-180f, 180f)] private float hueMin = -25f;
    [SerializeField, Range(-180f, 180f)] private float hueMax = 25f;
    [SerializeField, Min(0.01f)] private float hueCycleSeconds = 2.0f;

    private readonly List<Vector3Int> untargetableCells = new();
    private Vector3Int damageableAnchorCell;
    private GameObject activeMeleePowerup;

    public override bool CanRun(BossContext context)
    {
        if (context?.controller is not VampireQueenBossController)
            return false;

        return context.health != null && context.health.CurrentPhase == BossPhase.Phase3;
    }

    public override IEnumerator Execute(BossContext context)
    {
        var queen = context?.controller as VampireQueenBossController;
        if (queen == null || context.grid == null)
            yield break;

        SetPhaseObjectsActive(false);

        if (swapDelay > 0f)
            yield return new WaitForSeconds(swapDelay);

        if (previousBodyRoot != null)
            previousBodyRoot.SetActive(false);

        if (meteorBodyRoot != null)
            meteorBodyRoot.SetActive(true);

        if (meteorAnimator != null || meteorSpriteRenderer != null)
            queen.SetVisuals(meteorAnimator, meteorSpriteRenderer);

        if (meteorContactCollider != null)
            queen.SetContactCollider(meteorContactCollider);
        
        if (queen.FogFx != null)
            queen.FogFx.SetActive(true);
        
        queen.PostFx?.SetPhase3LookActive(true);
        
        Coroutine hueRoutine = null;

        if (animateHueShift && queen.PostFx != null &&
            queen.PostFx.TryGetColorAdjustments(out var color, addIfMissing: false))
        {
            // Run until the action is no longer active (phase change or death)
            hueRoutine = StartCoroutine(HueShiftLoop(
                color,
                hueMin,
                hueMax,
                hueCycleSeconds,
                keepRunning: () =>
                    queen != null &&
                    queen.State != BossControllerBase.BossState.Dead &&
                    context != null &&
                    context.health != null &&
                    context.health.CurrentPhase == BossPhase.Phase3
            ));
        }
        
        ResolveLandingAnchor(context, queen);

        Vector3 landingWorld = queen.GetFootprintCenterWorld(damageableAnchorCell);
        Vector3 entryWorld = landingWorld + Vector3.up * Mathf.Max(0f, entryHeight);

        queen.SetState(BossControllerBase.BossState.Flight);
        queen.transform.position = entryWorld;
        yield return queen.FlyToWorldPoint(landingWorld, entrySpeed, "");
        
        queen.SetDamageableAnchor(damageableAnchorCell);

        queen.SetContactColliderActive(true);

        // Cinemachine Impulse shake
        if (queen.DamageShakeForce > 0f && queen.AllowDamageShake)
            CameraShake.Instance?.Shake(queen.DamageShakeForce);

        activeMeleePowerup = TrySpawnPowerup(context);

        queen.BindShield();

        while (queen.State != BossControllerBase.BossState.Dead &&
               context.health != null &&
               context.health.CurrentPhase == BossPhase.Phase3)
        {
            if (!TryPickMeteorTarget(context, out var targetCell))
            {
                EnsureMeleePowerup(context);
                yield return null;
                continue;
            }

            float health01 = context.health.Health01;
            float interval = Mathf.Lerp(maxBombInterval, minBombInterval, 1f - health01);
            float fallSpeed = Mathf.Lerp(minFallSpeed, maxFallSpeed, 1f - health01);

            float travelTime = Mathf.Max(0.05f, entryHeight / Mathf.Max(0.1f, fallSpeed));
            float telegraphDuration = Mathf.Max(minTelegraphDuration, travelTime);

            yield return DropMeteor(context, targetCell, telegraphDuration, travelTime);

            if (interval > 0f)
                yield return new WaitForSeconds(interval);

            EnsureMeleePowerup(context);
        }
    }


    private void ResolveLandingAnchor(BossContext context, VampireQueenBossController queen)
    {
        damageableAnchorCell = default;

        if (context.grid == null)
            return;

        if (landingAnchorMarker != null)
        {
            damageableAnchorCell = context.grid.WorldToCell(landingAnchorMarker.position);
            return;
        }

        if (queen.TryPickFootprintAnchor(out var anchor))
        {
            damageableAnchorCell = anchor;
            return;
        }

        damageableAnchorCell = context.grid.WorldToCell(queen.transform.position);
    }

    private bool TryPickMeteorTarget(BossContext context, out Vector3Int targetCell)
    {
        targetCell = default;
        if (context.grid == null)
            return false;

        RebuildUntargetableCells(context);

        var candidates = context.grid.GetAllPaintedGroundCells();
        if (candidates == null || candidates.Count == 0)
            return false;

        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[Random.Range(0, candidates.Count)];
            if (!context.grid.IsPaintedGroundCell(cell))
                continue;

            var kind = context.grid.GetTileKind(cell);
            if (kind != TileKind.Floor && !(kind == TileKind.Start && !disallowStartTiles))
                continue;

            if (untargetableCells.Contains(cell))
                continue;

            targetCell = cell;
            return true;
        }

        return false;
    }

    private IEnumerator DropMeteor(BossContext context, Vector3Int targetCell, float telegraphDuration, float travelTime)
    {
        var cells = new List<Vector3Int> { targetCell };

        context.grid.FlashTelegraphCellsForOwner(
            VampireQueenBossController.PREVIEW_OWNER_BOSS,
            cells,
            telegraphColor,
            telegraphDuration
        );

        Vector3 targetWorld = context.grid.CellToWorldCenter(targetCell);
        Vector3 spawnWorld = targetWorld + Vector3.up * Mathf.Max(0f, entryHeight);

        if (meteorProjectilePrefab != null)
        {
            var meteor = Instantiate(meteorProjectilePrefab, spawnWorld, Quaternion.identity);
            if (meteor != null)
                StartCoroutine(MoveMeteorDown(meteor.transform, spawnWorld, targetWorld, travelTime));
        }

        if (travelTime > 0f)
            yield return new WaitForSeconds(travelTime);

        if (context.grid.GetTileKind(targetCell) == TileKind.Floor)
            context.grid.ToggleFloorVoidAt(targetCell, voidTile);

        if (meteorImpactVfxPrefab != null)
            Instantiate(meteorImpactVfxPrefab, targetWorld, Quaternion.identity);

        if (context.player != null && context.player.CellPosition == targetCell)
            context.player.StartVoidFallDeath(targetWorld);
    }

    private IEnumerator MoveMeteorDown(Transform meteor, Vector3 start, Vector3 end, float duration)
    {
        if (meteor == null)
            yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            meteor.position = Vector3.Lerp(start, end, a);
            yield return null;
        }

        if (meteor != null)
            Destroy(meteor.gameObject);
    }

    private void EnsureMeleePowerup(BossContext context)
    {
        if (activeMeleePowerup == null)
            activeMeleePowerup = TrySpawnPowerup(context);
    }

    private GameObject TrySpawnPowerup(BossContext context)
    {
        if (meleePowerupPrefab == null || context?.grid == null)
            return null;

        RebuildUntargetableCells(context);

        var candidates = context.grid.GetAllPaintedGroundCells();
        if (candidates == null || candidates.Count == 0)
            return null;

        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[Random.Range(0, candidates.Count)];
            if (!context.grid.CanEnterCell(cell))
                continue;

            if (untargetableCells.Contains(cell))
                continue;

            Vector3 spawn = context.grid.CellToWorldCenter(cell);
            return Instantiate(meleePowerupPrefab, spawn, Quaternion.identity);
        }

        return null;
    }
    
    private IEnumerator HueShiftLoop(ColorAdjustments color, float min, float max, float cycleSeconds,
        System.Func<bool> keepRunning)
    {
        // Ensure override participates in blending
        color.active = true;

        // Make sure Hue Shift is overridden (so our values apply even if the profile has it unchecked)
        color.hueShift.overrideState = true;

        float t = 0f;
        float dur = Mathf.Max(0.01f, cycleSeconds);

        while (keepRunning())
        {
            t += Time.deltaTime;
            // 0..1..0 over time
            float a = Mathf.PingPong(t / dur, 1f);
            float value = Mathf.Lerp(min, max, a);
            color.hueShift.Override(value);
            yield return null;
        }
    }


    private void RebuildUntargetableCells(BossContext context)
    {
        untargetableCells.Clear();
        if (context.grid == null)
            return;

        foreach (var marker in untargetableMarkers)
        {
            if (marker == null)
                continue;

            var cell = context.grid.WorldToCell(marker.position);
            if (!untargetableCells.Contains(cell))
                untargetableCells.Add(cell);
        }

        if (occupiedAreaTransform != null)
            AddCellsFromBounds(untargetableCells, GetTransformBounds(occupiedAreaTransform), context.grid);
    }

    private static Bounds GetTransformBounds(Transform source)
    {
        Vector3 size = source.lossyScale;
        size.x = Mathf.Abs(size.x);
        size.y = Mathf.Abs(size.y);
        size.z = 0.01f;
        return new Bounds(source.position, size);
    }

    private static void AddCellsFromBounds(List<Vector3Int> cells, Bounds bounds, TilemapGridManager grid)
    {
        const float epsilon = 0.001f;
        Vector3Int minCell = grid.WorldToCell(bounds.min);
        Vector3Int maxCell = grid.WorldToCell(bounds.max - new Vector3(epsilon, epsilon, 0f));

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!cells.Contains(cell))
                    cells.Add(cell);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        var grid = TilemapGridManager.Instance;
        if (grid == null)
            grid = FindFirstObjectByType<TilemapGridManager>();

        if (grid == null)
            return;

        RebuildUntargetableCells(new BossContext { grid = grid, controller = GetComponent<VampireQueenBossController>() });

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);

        foreach (var cell in untargetableCells)
        {
            Vector3 center = grid.CellToWorldCenter(cell);
            Vector3 size = Vector3.one * 0.9f;
            Gizmos.DrawWireCube(center, size);

            if (drawLabels)
                Handles.Label(center + Vector3.up * 0.3f, $"{cell.x},{cell.y}");
        }
    }
#endif
}

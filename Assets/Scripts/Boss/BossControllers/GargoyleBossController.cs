using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss controller for the Gargoyle boss.
/// Flight state: bombs random floor tiles (telegraph -> detonate -> turn to DeathVoid).
/// Rest state: lands only on existing 2x2 DeathVoid patches; player can place floor onto footprint to deal damage.
/// Phases scale bomb quota (<=66%) and bomb speed (<=33%), and also shrink telegraph duration.
/// </summary>
public class GargoyleBossController : MonoBehaviour
{
    private const int PREVIEW_OWNER_BOSS = 7001;

    public enum BossState { Enter, Flight, Rest, Hurt, Dead }
    
    private enum BombAoePattern { Square, Cross }

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private TilemapGridManager grid;
    [SerializeField] private PlayerController player;
    [SerializeField] private ShapePlacer shapePlacer;

    [Header("Boss Footprint")]
    [Tooltip("Top-left anchored 2x2 footprint (cells: anchor, anchor+R, anchor+D, anchor+R+D).")]
    [SerializeField] private Vector2Int footprintSize = new (2, 2);
    
    [Header("Bomb Area-of-Effect")]
    [Range(0f, 1f)]
    [SerializeField] private float aoeChance = 0.45f;

    [Min(0)]
    [SerializeField] private int aoeRadius = 1; // 1 => 3x3, 2 => 5x5

    [Range(0f, 1f)]
    [SerializeField] private float crossPatternChance = 0.5f;

    [Tooltip("Optional cap on how many cells an AoE can affect (0 = no cap).")]
    [SerializeField] private int aoeMaxCells = 0;

    [Header("Tuning - Bombing (per phase)")]
    [Tooltip("How many bombs are dropped during Flight before attempting Rest (Phase1/2/3).")]
    [SerializeField] private int bombsPerFlight_P1 = 6;
    [SerializeField] private int bombsPerFlight_P2 = 10;
    [SerializeField] private int bombsPerFlight_P3 = 10;
    
    [Tooltip("Fly move speed for (Phase1/2/3).")]
    [SerializeField] private float flyMoveSpeed_P1 = 6f;
    [SerializeField] private float flyMoveSpeed_P2 = 6f;
    [SerializeField] private float flyMoveSpeed_P3 = 12f;
    
    [Tooltip("Time between detonations (Phase1/2/3). Telegraph time is separate.")]
    [SerializeField] private float bombInterval_P1 = 1.0f;
    [SerializeField] private float bombInterval_P2 = 1.0f;
    [SerializeField] private float bombInterval_P3 = 0.6f;

    [Tooltip("Telegraph duration before detonation (Phase1/2/3).")]
    [SerializeField] private float telegraphDuration_P1 = 0.8f;
    [SerializeField] private float telegraphDuration_P2 = 0.6f;
    [SerializeField] private float telegraphDuration_P3 = 0.45f;

    [Header("Tuning - Rest")]
    [SerializeField] private float restDuration = 6.0f;
    [Tooltip("Damage applied when player successfully places onto boss footprint during Rest.")]
    [SerializeField] private int damagePerRestHit = 1;
    [Tooltip("Boss can only take one hit per rest cycle.")]
    [SerializeField] private bool oneHitPerRest = true;
    
    [Header("Rest Travel")]
    [SerializeField] private float restHoverHeight = 2.0f;
    [SerializeField] private float landingSpeed = 10f;
    [SerializeField] private float preRestHoverTime = 0.10f;

    [Header("Bomb Targeting - Near Bucket")]
    [Tooltip("Manhattan distance of the nearby cells.")]
    [SerializeField] private int nearRadius = 6;               // Manhattan distance in cells
    [Tooltip("Chance to pick from nearby tiles.")]
    [SerializeField, Range(0f, 1f)] private float nearPickChance = 0.8f;       // chance to pick from near tiles if any exist
    [Tooltip("Optional: avoid bombing tiles too close to player (in cells). 0 = no minimum.")]
    [SerializeField] private int minDistanceFromPlayer = 0;
    [Tooltip("Check to allow bomb on start tile.")]
    [SerializeField] private bool allowBombingStartTile = true; // you currently allow Start as candidate
    [Tooltip("Check to allow bomb on player current tile.")]
    [SerializeField] private bool allowBombPlayerCell = true;
    
    [Header("Bomb Delivery - Fly then Throw")]
    [SerializeField] private float hoverHeight = 2.0f;              // world units above target cell
    [SerializeField] private float preThrowHoverTime = 0.10f;        // small pause after arriving
    [SerializeField] private float throwWindupTime = 0.12f;          // delay before bomb spawns (matches anim)
    [SerializeField] private float arriveEpsilon = 0.05f;            // how close counts as arrived
    
    [Header("Bomb Cycle Feel")]
    [SerializeField] private float postThrowRecoverTime = 0.25f; // pause after detonation before moving again
    [SerializeField] private float betweenBombMoveDelay = 0.15f; // pause before choosing next target (adds rhythm)

    [Header("Shape Drop")]
    [Range(0f, 1f)]
    [SerializeField] private float shapeDropChance = 0.6f;

    [SerializeField] private GameObject shapePickupPrefab;

    [Header("Visuals")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform bombSpawnPoint;
    [SerializeField] private GameObject bombProjectilePrefab;
    [SerializeField] private GameObject bombExplosionVfxPrefab;
    
    [Header("Damage Force")]
    [Tooltip("If true, allow camera shake.")]
    [SerializeField] protected bool allowDamageShake = true;
    [Tooltip("Shake force for camera on damage.")]
    [SerializeField] protected float damageShakeForce = 0.8f;

    [Header("Telegraph Color")]
    [SerializeField] private Color bombTelegraphColor = new (1f, 0.2f, 0.2f, 0.9f);

    // Runtime
    public BossState State { get; private set; } = BossState.Enter;

    private List<Vector3Int> paintedGroundCells;
    private bool tookDamageThisRest;
    private Vector3Int restAnchorCell; // top-left of 2x2
    private Coroutine mainRoutine;

    private void Awake()
    {
        if (grid == null) 
            grid = TilemapGridManager.Instance;
        
        if (player == null) 
            player = FindAnyObjectByType<PlayerController>();
        
        if (shapePlacer == null) 
            shapePlacer = FindAnyObjectByType<ShapePlacer>();
        
        if (bossHealth == null) 
            bossHealth = GetComponent<BossHealth>();
        
        if(spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (shapePlacer != null)
            shapePlacer.OnShapePlaced += HandleShapePlaced;

        if (bossHealth != null)
        {
            bossHealth.OnDied += HandleBossDied;
            bossHealth.OnPlayerDied += HandlePlayerDied;
        }
    }

    private void OnDisable()
    {
        if (shapePlacer != null)
            shapePlacer.OnShapePlaced -= HandleShapePlaced;

        if (bossHealth != null)
        {
            bossHealth.OnDied -= HandleBossDied;
            bossHealth.OnPlayerDied -= HandlePlayerDied;
        }
    }

    private void Start()
    {
        if (grid == null)
        {
            Debug.LogError("[GargoyleBossController] TilemapGridManager missing.");
            enabled = false;
            return;
        }

        paintedGroundCells = grid.GetAllPaintedGroundCells();

        // Kick off boss logic
        mainRoutine = StartCoroutine(MainLoop());
    }

    private IEnumerator MainLoop()
    {
        State = BossState.Enter;

        // If you want an intro delay, add a serialized float and wait here.
        yield return new WaitForSeconds(3f);

        while (State != BossState.Dead)
        {
            // Flight
            yield return FlightRoutine();

            // Attempt rest (must have a valid 2x2 death void patch)
            bool rested = TryFindRestAnchor(out restAnchorCell);
            if (rested)
                yield return RestRoutine();
            else
            {
                // No valid 2x2 death void yet: keep flying and bombing.
                // This enforces your rule that rest only happens on existing 2x2 death void.
                continue;
            }
        }
    }

    private IEnumerator FlightRoutine()
    {
        State = BossState.Flight;
        TriggerAnim("Fly");

        int bombsToDrop = GetBombQuotaForPhase();

        for (int i = 0; i < bombsToDrop; i++)
        {
            if (State == BossState.Dead) yield break;

            // Run ONE full bomb action as its own "enter state"
            yield return BombCycle();

            if (betweenBombMoveDelay > 0f)
                yield return new WaitForSeconds(betweenBombMoveDelay);
        }
    }
    
    private IEnumerator BombCycle()
    {
        float bombInterval = GetBombIntervalForPhase();
        float telegraph = GetTelegraphForPhase();

        // 1) pick target
        if (!TryPickBombTarget(out Vector3Int targetCell))
            yield break;

        // 2) fly to hover point
        Vector3 targetWorld = grid.CellToWorldCenter(targetCell);
        Vector3 hoverWorld = targetWorld + Vector3.up * hoverHeight;

        State = BossState.Flight;
        TriggerAnim("Fly");
        yield return FlyToWorldPoint(hoverWorld);

        if (preThrowHoverTime > 0f)
            yield return new WaitForSeconds(preThrowHoverTime);

        // 3) telegraph
        bool isAoe = aoeRadius > 0 && Random.value <= aoeChance;

        BombAoePattern pattern = BombAoePattern.Square;
        if (isAoe)
            pattern = Random.value <= crossPatternChance ? BombAoePattern.Cross : BombAoePattern.Square;

        List<Vector3Int> affected = GetBombAffectedCells(targetCell, isAoe, pattern);

        grid.FlashTelegraphCellsForOwner(PREVIEW_OWNER_BOSS, affected, bombTelegraphColor, telegraph);

        // 4) throw (enter bomb action)
        TriggerAnim("ThrowBomb");

        float windup = Mathf.Clamp(throwWindupTime, 0f, telegraph);
        if (windup > 0f)
            yield return new WaitForSeconds(windup);

        // 5) spawn projectile so it arrives at detonation time
        float travelTime = Mathf.Max(0.05f, telegraph - windup);
        SpawnBombProjectile(targetCell, travelTime);

        if (travelTime > 0f)
            yield return new WaitForSeconds(travelTime);

        // 6) detonate
        DetonateBombCells(affected, targetCell);

        // 7) recover (this is the “not loop-y” feel)
        TriggerAnim("Idle");
        if (postThrowRecoverTime > 0f)
            yield return new WaitForSeconds(postThrowRecoverTime);

        // 8) pacing to next action (this replaces the old tight loop vibe)
        if (bombInterval > 0f)
            yield return new WaitForSeconds(bombInterval);
    }
    
    private IEnumerator FlyToWorldPoint(Vector3 targetWorld)
    {
        // Ensure we’re in Fly anim while traveling
        TriggerAnim("Fly");

        while (Vector3.Distance(transform.position, targetWorld) > arriveEpsilon)
        {
            if (State == BossState.Dead) yield break;
            
            UpdateFacingTowards(targetWorld);

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetWorld,
                GetFlySpeedForPhase() * Time.deltaTime
            );

            yield return null;
        }
    }
    
    private void UpdateFacingTowards(Vector3 targetWorld)
    {
        if (spriteRenderer == null) 
            return;

        float dx = targetWorld.x - transform.position.x;

        // Avoid jitter when basically vertical movement
        if (Mathf.Abs(dx) < 0.01f) 
            return;

        // Default faces LEFT, so flipX when we want RIGHT
        spriteRenderer.flipX = dx > 0f;
    }
    
    private IEnumerator LandToWorldPoint(Vector3 targetWorld)
    {
        // Ensure we’re in Land anim while traveling
        TriggerAnim("Land");
        
        while (Vector3.Distance(transform.position, targetWorld) > arriveEpsilon)
        {
            if (State == BossState.Dead) yield break;
            
            UpdateFacingTowards(targetWorld);

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetWorld,
                landingSpeed * Time.deltaTime
            );

            yield return null;
        }
    }

    private IEnumerator RestRoutine()
    {
        // Compute rest position first
        Vector3 restWorld = GetFootprintCenterWorld(restAnchorCell);

        // Travel to rest location BEFORE resting
        State = BossState.Flight;
        TriggerAnim("Fly");

        Vector3 hoverWorld = restWorld + Vector3.up * restHoverHeight;
        yield return FlyToWorldPoint(hoverWorld);

        if (preRestHoverTime > 0f)
            yield return new WaitForSeconds(preRestHoverTime);

        // Land onto the footprint center
        yield return LandToWorldPoint(restWorld);

        // Now actually rest
        State = BossState.Rest;
        tookDamageThisRest = false;
        TriggerAnim("Rest");

        float t = 0f;
        while (t < restDuration)
        {
            if (State == BossState.Dead) yield break;

            if (tookDamageThisRest)
                break;

            t += Time.deltaTime;
            yield return null;
        }

        //TriggerAnim("Fly");
        yield return null;
    }

    // ─────────────────────────────────────────────────────────────
    // Bomb logic
    // ─────────────────────────────────────────────────────────────
    private bool TryPickBombTarget(out Vector3Int cell)
    {
        cell = default;

        if (paintedGroundCells == null || paintedGroundCells.Count == 0 || grid == null)
            return false;

        bool hasPlayer = player != null;
        Vector3Int playerCell = hasPlayer ? player.CellPosition : default;

        List<Vector3Int> near = new List<Vector3Int>(64);
        List<Vector3Int> far = new List<Vector3Int>(128);

        for (int i = 0; i < paintedGroundCells.Count; i++)
        {
            var c = paintedGroundCells[i];

            if (!grid.IsPaintedGroundCell(c))
                continue;

            TileKind k = grid.GetTileKind(c);

            bool isValidKind =
                k == TileKind.Floor ||
                (allowBombingStartTile && k == TileKind.Start);

            if (!isValidKind)
                continue;

            if (hasPlayer)
            {
                int dist = Mathf.Abs(c.x - playerCell.x) + Mathf.Abs(c.y - playerCell.y);

                // If you set minDistanceFromPlayer > 0, this will exclude the player cell (dist=0) too.
                if (minDistanceFromPlayer > 0 && dist < minDistanceFromPlayer)
                    continue;

                // Only skip the exact player cell if we don't allow it
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

    private void SpawnBombProjectile(Vector3Int targetCell, float travelTime)
    {
        if (bombProjectilePrefab == null || bombSpawnPoint == null)
            return;

        Vector3 start = bombSpawnPoint.position;
        Vector3 end = grid.CellToWorldCenter(targetCell);

        var go = Instantiate(bombProjectilePrefab, start, Quaternion.identity);

        // If the prefab has its own mover script, great. If not, we do a simple arc move here.
        // We’ll do it here to keep the system self-contained.
        StartCoroutine(MoveProjectileArc(go.transform, start, end, travelTime));
    }

    private IEnumerator MoveProjectileArc(Transform proj, Vector3 start, Vector3 end, float duration)
    {
        if (proj == null) yield break;

        float t = 0f;
        float height = 1.5f; // purely visual arc height (tune or expose later)

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);

            // Parabolic arc
            Vector3 p = Vector3.Lerp(start, end, a);
            float arc = 4f * height * a * (1f - a);
            p.y += arc;

            proj.position = p;
            yield return null;
        }

        if (proj != null)
            Destroy(proj.gameObject);
    }

    private bool TrySpawnShapePickupNear(Vector3Int bombCell)
    {
        // Prefer 4-neighbor safe floor cells.
        var options = new List<Vector3Int>(4)
        {
            bombCell + new Vector3Int(1, 0, 0),
            bombCell + new Vector3Int(-1, 0, 0),
            bombCell + new Vector3Int(0, 1, 0),
            bombCell + new Vector3Int(0, -1, 0),
        };

        // Shuffle options
        for (int i = 0; i < options.Count; i++)
        {
            int j = Random.Range(i, options.Count);
            (options[i], options[j]) = (options[j], options[i]);
        }

        for (int i = 0; i < options.Count; i++)
        {
            var c = options[i];
            if (!grid.IsPaintedGroundCell(c)) continue;
            if (grid.GetTileKind(c) != TileKind.Floor) continue;
            if (player != null && c == player.CellPosition) continue;

            Vector3 w = grid.CellToWorldCenter(c);
            Instantiate(shapePickupPrefab, w, Quaternion.identity);
            return true;
        }

        // Fallback: random floor
        for (int tries = 0; tries < 25; tries++)
        {
            var c = paintedGroundCells[Random.Range(0, paintedGroundCells.Count)];
            if (!grid.IsPaintedGroundCell(c)) continue;
            if (grid.GetTileKind(c) != TileKind.Floor) continue;
            if (player != null && c == player.CellPosition) continue;

            Vector3 w = grid.CellToWorldCenter(c);
            Instantiate(shapePickupPrefab, w, Quaternion.identity);
            return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // Rest / damage logic
    // ─────────────────────────────────────────────────────────────

    private bool TryFindRestAnchor(out Vector3Int anchor)
    {
        anchor = default;

        // Find anchors that form a full 2x2 patch of painted VOID tiles.
        var anchors = new List<Vector3Int>();

        // Build a quick lookup of void painted cells (for fast 2x2 checking)
        // Note: "death void" in this scene should be the only void tile type you paint.
        var voidCells = new HashSet<Vector3Int>();

        for (int i = 0; i < paintedGroundCells.Count; i++)
        {
            var c = paintedGroundCells[i];
            if (!grid.IsPaintedGroundCell(c)) continue;

            if (grid.GetTileKind(c) == TileKind.Void)
                voidCells.Add(c);
        }

        // Scan for top-left anchors for a 2x2 footprint
        for (int i = 0; i < paintedGroundCells.Count; i++)
        {
            var a = paintedGroundCells[i];
            if (!voidCells.Contains(a)) continue;

            // top-left anchor: need a, a+R, a+D, a+R+D
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

    private void HandleShapePlaced(IReadOnlyList<Vector3Int> placedCells)
    {
        if (State != BossState.Rest)
            return;

        if (oneHitPerRest && tookDamageThisRest)
            return;

        if (placedCells == null || placedCells.Count == 0)
            return;

        // Compute current footprint cells from restAnchorCell
        // Anchor is top-left of the 2x2 (x+, y-).
        var footprint = GetFootprintCells(restAnchorCell);

        for (int i = 0; i < placedCells.Count; i++)
        {
            if (footprint.Contains(placedCells[i]))
            {
                // Apply one fixed hit, then end rest immediately.
                if (bossHealth != null)
                    bossHealth.ApplyDamage(Mathf.Max(1, damagePerRestHit));

                tookDamageThisRest = true;

                TriggerAnim("Hurt");
                // You said first damage immediately ends rest: we set the flag and RestRoutine will exit.
                return;
            }
        }
    }

    private HashSet<Vector3Int> GetFootprintCells(Vector3Int anchorTopLeft)
    {
        // 2x2 by default: (0,0), (1,0), (0,-1), (1,-1)
        var set = new HashSet<Vector3Int>();

        for (int x = 0; x < footprintSize.x; x++)
        {
            for (int y = 0; y < footprintSize.y; y++)
            {
                // y goes downward in grid space
                set.Add(anchorTopLeft + new Vector3Int(x, -y, 0));
            }
        }

        return set;
    }

    private Vector3 GetFootprintCenterWorld(Vector3Int anchorTopLeft)
    {
        // Average of the footprint cell centers
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int x = 0; x < footprintSize.x; x++)
        {
            for (int y = 0; y < footprintSize.y; y++)
            {
                sum += grid.CellToWorldCenter(anchorTopLeft + new Vector3Int(x, -y, 0));
                count++;
            }
        }

        return count > 0 ? sum / count : grid.CellToWorldCenter(anchorTopLeft);
    }

    // ─────────────────────────────────────────────────────────────
    // Boss Health / Phase params
    // ─────────────────────────────────────────────────────────────

    private int GetBombQuotaForPhase()
    {
        if (bossHealth == null) return bombsPerFlight_P1;

        switch (bossHealth.CurrentPhase)
        {
            case BossPhase.Phase2: return bombsPerFlight_P2;
            case BossPhase.Phase3: return bombsPerFlight_P3;
            default: return bombsPerFlight_P1;
        }
    }

    private float GetBombIntervalForPhase()
    {
        if (bossHealth == null) return bombInterval_P1;

        switch (bossHealth.CurrentPhase)
        {
            case BossPhase.Phase2: return bombInterval_P2;
            case BossPhase.Phase3: return bombInterval_P3;
            default: return bombInterval_P1;
        }
    }

    private float GetTelegraphForPhase()
    {
        if (bossHealth == null) return telegraphDuration_P1;

        switch (bossHealth.CurrentPhase)
        {
            case BossPhase.Phase2: return telegraphDuration_P2;
            case BossPhase.Phase3: return telegraphDuration_P3;
            default: return telegraphDuration_P1;
        }
    }
    
    private float GetFlySpeedForPhase()
    {
        if (bossHealth == null) return flyMoveSpeed_P1;

        switch (bossHealth.CurrentPhase)
        {
            case BossPhase.Phase2: return flyMoveSpeed_P2;
            case BossPhase.Phase3: return flyMoveSpeed_P3;
            default: return flyMoveSpeed_P1;
        }
    }


    private void HandleBossDied()
    {
        if (State == BossState.Dead) return;

        State = BossState.Dead;
        TriggerAnim("Death");

        // Clear boss telegraphs
        grid.ClearTelegraphForOwner(PREVIEW_OWNER_BOSS);

        // Stop all boss behavior
        if (mainRoutine != null)
            StopCoroutine(mainRoutine);

        // End level after a short delay to allow death anim (tune later)
        StartCoroutine(EndAfterDeathDelay(3.0f));
    }

    private IEnumerator EndAfterDeathDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameManager.Instance?.BossDefeated();
    }
    
    private void HandlePlayerDied()
    {
        if (State == BossState.Dead) return;

        State = BossState.Dead;
        TriggerAnim("Death");

        // Clear boss telegraphs
        grid.ClearTelegraphForOwner(PREVIEW_OWNER_BOSS);

        // Stop all boss behavior
        if (mainRoutine != null)
            StopCoroutine(mainRoutine);

        // End level after a short delay to allow death anim (tune later)
        StartCoroutine(EndAfterPlayerDelay(3.0f));
    }

    private IEnumerator EndAfterPlayerDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameManager.Instance?.PlayerKilled();
    }

    private void TriggerAnim(string trigger)
    {
        if (animator != null && !string.IsNullOrEmpty(trigger))
            animator.SetTrigger(trigger);
    }
    
    private List<Vector3Int> GetBombAffectedCells(Vector3Int center, bool isAoe, BombAoePattern pattern)
    {
        // Always include the center cell if it's within the painted footprint
        var cells = new List<Vector3Int>();

        if (!grid.IsPaintedGroundCell(center))
            return cells;

        if (!isAoe || aoeRadius <= 0)
        {
            cells.Add(center);
            return cells;
        }

        // Collect candidate cells
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

        // Filter to painted ground footprint only
        foreach (var c in set)
        {
            if (!grid.IsPaintedGroundCell(c)) continue;

            // Telegraphing only floor cells is usually clearer (void already deadly).
            // If you'd rather telegraph ALL painted cells, remove this check.
            if (grid.GetTileKind(c) != TileKind.Floor && grid.GetTileKind(c) != TileKind.Start) continue;

            cells.Add(c);
        }

        // Optional cap to reduce extremely large AoEs
        if (aoeMaxCells > 0 && cells.Count > aoeMaxCells)
        {
            // Shuffle then trim
            for (int i = 0; i < cells.Count; i++)
            {
                int j = Random.Range(i, cells.Count);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            cells.RemoveRange(aoeMaxCells, cells.Count - aoeMaxCells);
        }

        // Ensure at least the center is included if it's floor (prevents empty AoE in weird cases)
        if (cells.Count == 0 && grid.GetTileKind(center) == TileKind.Floor)
            cells.Add(center);

        return cells;
    }

    private void DetonateBombCells(IReadOnlyList<Vector3Int> cells, Vector3Int centerCell)
    {
        if (cells == null || cells.Count == 0)
            return;
        
        // Cinemachine Impulse shake
        if (damageShakeForce > 0f && allowDamageShake)
            CameraShake.Instance?.Shake(damageShakeForce);

        // Convert affected FLOOR -> VOID (death void, since boss scene voidTile is DeathVoid GameTile)
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];

            // Only convert floors (never toggle void back to floor)
            if (grid.GetTileKind(c) == TileKind.Floor)
                grid.ToggleFloorVoidAt(c);

            // Per-cell explosion VFX (still the one prefab)
            if (bombExplosionVfxPrefab != null)
            {
                Vector3 w = grid.CellToWorldCenter(c);
                Instantiate(bombExplosionVfxPrefab, w, Quaternion.identity);
            }

            // Player death if standing on any detonated cell
            if (player != null && player.CellPosition == c)
            {
                Vector3 w = grid.CellToWorldCenter(c);
                player.StartVoidFallDeath(w);
            }
        }

        // Shape drop once per bomb event (not per cell) — spawn near the center
        if (shapePickupPrefab != null && Random.value <= shapeDropChance)
            TrySpawnShapePickupNear(centerCell);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss controller for the Gargoyle boss.
/// Flight state: bombs random floor tiles (telegraph -> detonate -> turn to DeathVoid).
/// Rest state: lands only on existing 2x2 DeathVoid patches; player can place floor onto footprint to deal damage.
/// Phases scale bomb quota (<=66%) and bomb speed (<=33%), and also shrink telegraph duration.
/// </summary>
public class GargoyleBossController : BossControllerBase
{
    public const int PREVIEW_OWNER_BOSS = 7001;
    
    [Header("References")]
    [SerializeField] private TilemapGridManager grid;
    [SerializeField] private PlayerController player;
    [SerializeField] private ShapePlacer shapePlacer;

    [Header("Boss Footprint")]
    [Tooltip("Top-left anchored 2x2 footprint (cells: anchor, anchor+R, anchor+D, anchor+R+D).")]
    [SerializeField] private Vector2Int footprintSize = new (2, 2);

    [Header("Actions")]
    [SerializeField] private BossAction bombingAction;
    [SerializeField] private BossAction restAction;
    
    // Runtime
    private bool tookDamageThisRest;
    private Vector3Int restAnchorCell; // top-left of 2x2
    private Coroutine mainRoutine;

    /// <summary>
    /// Flag indicating if the boss took damage during the current rest cycle.
    /// </summary>
    public bool TookDamageThisRest
    {
        get => tookDamageThisRest;
        set => tookDamageThisRest = value;
    }

    protected override void Awake()
    {
        if (grid == null) 
            grid = TilemapGridManager.Instance;
        
        if (player == null) 
            player = FindAnyObjectByType<PlayerController>();
        
        if (shapePlacer == null) 
            shapePlacer = FindAnyObjectByType<ShapePlacer>();
        
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (shapePlacer != null)
            shapePlacer.OnShapePlaced += HandleShapePlaced;
    }

    protected override void OnDisable()
    {
        if (shapePlacer != null)
            shapePlacer.OnShapePlaced -= HandleShapePlaced;
        
        base.OnDisable();
    }

    private void Start()
    {
        if (grid == null)
        {
            Debug.LogError("[GargoyleBossController] TilemapGridManager missing.");
            enabled = false;
            return;
        }

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
            yield return FlightRoutine();
            yield return TryRestRoutine();
        }
    }

    /// <summary>
    /// Builds the boss context for action modules.
    /// </summary>
    private BossContext BuildBossContext()
    {
        return new BossContext
        {
            controller = this,
            health = bossHealth,
            grid = grid,
            player = player,
            animator = animator,
            spriteRenderer = spriteRenderer,
        };
    }

    /// <summary>
    /// Sets the current rest anchor for rest routines.
    /// </summary>
    public void SetRestAnchor(Vector3Int anchor)
    {
        restAnchorCell = anchor;
    }

    // index 0 = bombing, index 1 = rest
    private IEnumerator TryRestRoutine()
    {
        var restContext = BuildBossContext();
        var bossAction = GetActionByNameOrFirst("Rest", restContext);
        
        if (bossAction == null)
            bossAction = restAction;
        
        if (bossAction == null || !bossAction.CanRun(restContext))
            yield break;

        yield return bossAction.Execute(restContext);
    }

    private IEnumerator FlightRoutine()
    {
        var context = BuildBossContext();
        var bossAction = GetActionByNameOrFirst("Bombing", context);
        
        if (bossAction == null)
            bossAction = bombingAction;
        
        if (bossAction == null || !bossAction.CanRun(context))
            yield break;

        yield return bossAction.Execute(context);
    }
    
    private void HandleShapePlaced(IReadOnlyList<Vector3Int> placedCells)
    {
        if (State != BossState.Rest)
            return;

        var action = GetRestAction();
        if (action == null)
            return;

        if (action.OneHitPerRest && tookDamageThisRest)
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
                TakeDamage(action.DamagePerRestHit);

                tookDamageThisRest = true;

                TriggerAnim("Hurt");
                // You said first damage immediately ends rest: we set the flag and RestRoutine will exit.
                return;
            }
        }
    }
    
    private GargoyleRestAction GetRestAction()
    {
        var context = BuildBossContext();
        
        var bossAction = GetActionByNameOrFirst("Rest", context);
        
        if (bossAction == null)
            bossAction = restAction;
        
        return bossAction as GargoyleRestAction;
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

    internal Vector3 GetFootprintCenterWorld(Vector3Int anchorTopLeft)
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


    protected override void OnBossDeathStarted()
    {
        ClearBossTelegraphsAndStop();
    }

    protected override void OnPlayerDeathStarted()
    {
        ClearBossTelegraphsAndStop();
    }

    private void ClearBossTelegraphsAndStop()
    {
        if (grid != null)
            grid.ClearTelegraphForOwner(PREVIEW_OWNER_BOSS);

        if (mainRoutine != null)
            StopCoroutine(mainRoutine);
    }
    
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss controller for the Gargoyle boss.
/// <para>
/// Flight state: bombs random floor tiles (telegraph -> detonate -> turn to DeathVoid).
/// </para>
/// <para>
/// Rest state: lands only on existing 2x2 DeathVoid patches; the player can place floor onto the footprint
/// to deal damage (typically one hit per rest, depending on the active rest action settings).
/// </para>
/// <para>
/// Phases scale bomb quota (<=66%) and bomb speed (<=33%), and also shrink telegraph duration.
/// Phase logic itself is driven by <see cref="BossHealth"/> and the active action modules.
/// </para>
/// </summary>
public class GargoyleBossController : BossControllerBase
{
    /// <summary>
    /// Owner id used when placing/clearing boss telegraph previews in the grid preview/telegraph layer.
    /// </summary>
    public const int PREVIEW_OWNER_BOSS = 7001;

    [Header("Gargoyle References")]
    [Tooltip("Shape placer used to detect placed shapes during Rest phase. If null, will be auto-found in Awake().")]
    [SerializeField] private ShapePlacer shapePlacer;
    
    [Header("Intro")]
    [Tooltip("Delay (seconds) before the main loop starts after the enter animation.")]
    [SerializeField] private float enterDelay = 4.5f;

    [Header("Actions (Optional Overrides)")]
    [Tooltip("Explicit bombing action override (used if action lookup by name fails).")]
    [SerializeField] private BossAction bombingAction;
    [Tooltip("Explicit rest action override (used if action lookup by name fails).")]
    [SerializeField] private BossAction restAction;

    private bool tookDamageThisRest; // True if the boss already took damage during the current rest cycle.
    private Vector3Int restAnchorCell; // Top-left anchor cell for the current rest footprint (2x2 by default).
    private Coroutine mainRoutine; // Main boss loop coroutine handle.
    
    // ─────────────────────────────────────────────
    #region Unity Events

    /// <summary>
    /// Auto-wires references if missing, then defers to base initialization.
    /// </summary>
    protected override void Awake()
    {
        if (shapePlacer == null)
            shapePlacer = FindAnyObjectByType<ShapePlacer>();

        base.Awake();
    }

    /// <summary>
    /// Validates required references and starts the main boss loop.
    /// </summary>
    private void Start()
    {
        if (grid == null)
        {
            Debug.LogError("[GargoyleBossController] TilemapGridManager missing.");
            enabled = false;
            return;
        }

        // Kick off boss logic.
        mainRoutine = StartCoroutine(MainLoop());
    }

    /// <summary>
    /// Subscribes to shape placement events and base boss health events.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();

        if (shapePlacer != null)
            shapePlacer.OnShapePlaced += HandleShapePlaced;
    }

    /// <summary>
    /// Unsubscribes from shape placement events and base boss health events.
    /// </summary>
    protected override void OnDisable()
    {
        if (shapePlacer != null)
            shapePlacer.OnShapePlaced -= HandleShapePlaced;

        base.OnDisable();
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Main Loop / Routines

    /// <summary>
    /// Main boss state machine loop: intro delay, then alternates Flight and Rest until death.
    /// </summary>
    private IEnumerator MainLoop()
    {
        State = BossState.Enter;

        // If you want an intro delay, add a serialized float and wait here.
        yield return new WaitForSeconds(enterDelay);

        while (State != BossState.Dead)
        {
            yield return FlightRoutine();
            yield return TryRestRoutine();
        }
    }

    /// <summary>
    /// Executes the Rest action if available and runnable.
    /// </summary>
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

    /// <summary>
    /// Executes the Bombing action if available and runnable.
    /// </summary>
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

    #endregion
    // ─────────────────────────────────────────────
    #region Rest Damage Hook

    /// <summary>
    /// Called by <see cref="ShapePlacer"/> when a shape is placed. During Rest, checks whether any placed
    /// cell overlaps the boss footprint and applies damage if allowed.
    /// </summary>
    /// <param name="placedCells">Cells placed by the shape placement event.</param>
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

        // Compute current footprint cells from restAnchorCell.
        // Anchor is top-left of the footprint (x+, y-).
        var footprint = GetFootprintCells(restAnchorCell);

        for (int i = 0; i < placedCells.Count; i++)
        {
            if (!footprint.Contains(placedCells[i]))
                continue;

            // Apply one fixed hit, then allow the active Rest routine to exit based on its rules.
            TakeDamage(action.DamagePerRestHit);

            tookDamageThisRest = true;

            PlayAnimation("Hurt");
            return;
        }
    }

    /// <summary>
    /// Resolves the active rest action for this boss, preferring named lookup, then falling back to the inspector override.
    /// </summary>
    /// <returns>The resolved rest action as <see cref="GargoyleRestAction"/>, or null if unavailable.</returns>
    private GargoyleRestAction GetRestAction()
    {
        var context = BuildBossContext();

        var bossAction = GetActionByNameOrFirst("Rest", context);
        if (bossAction == null)
            bossAction = restAction;

        return bossAction as GargoyleRestAction;
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Boss Finishes

    /// <summary>
    /// Called when boss death begins. Clears telegraphs and stops the main routine.
    /// </summary>
    protected override void OnBossDeathStarted()
    {
        postFx?.DisableAllOverrides(keepBloom: true);

        ClearBossTelegraphsAndStop();
    }

    /// <summary>
    /// Called when player death begins. Clears telegraphs and stops the main routine.
    /// </summary>
    protected override void OnPlayerDeathStarted()
    {
        postFx?.DisableAllOverrides(keepBloom: true);

        ClearBossTelegraphsAndStop();
    }

    /// <summary>
    /// Clears boss-owned telegraphs and stops the main loop coroutine.
    /// </summary>
    private void ClearBossTelegraphsAndStop()
    {
        if (grid != null)
            grid.ClearTelegraphForOwner(PREVIEW_OWNER_BOSS);

        if (mainRoutine != null)
            StopCoroutine(mainRoutine);
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Getters and Setters

    /// <summary>
    /// Flag indicating if the boss took damage during the current rest cycle.
    /// Used by actions to determine whether rest should end early or block further damage.
    /// </summary>
    public bool TookDamageThisRest
    {
        get => tookDamageThisRest;
        set => tookDamageThisRest = value;
    }

    /// <summary>
    /// Sets the current rest anchor cell used for footprint computations during Rest.
    /// </summary>
    /// <param name="anchor">Top-left anchor cell of the boss footprint.</param>
    public void SetRestAnchor(Vector3Int anchor)
    {
        restAnchorCell = anchor;
    }
    #endregion
    // ─────────────────────────────────────────────
}

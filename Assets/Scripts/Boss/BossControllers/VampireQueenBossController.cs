using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss controller for the Vampire Queen.
/// Phase 1 runs a micro-loop of Summon -> Vulnerable -> Retreat until phase transition.
/// </summary>
public class VampireQueenBossController : BossControllerBase
{
    [Header("Intro")]
    [Tooltip("Delay (seconds) before the main loop starts after the enter animation.")]
    [SerializeField] private float enterDelay = 3f;

    [Header("Actions (Optional Overrides)")]
    [Tooltip("Explicit summon action override (used if action lookup by name fails).")]
    [SerializeField] private BossAction summonAction;
    [Tooltip("Explicit attack action override (used if action lookup by name fails).")]
    [SerializeField] private BossAction attackAction;

    private Coroutine mainRoutine;
    private bool isVulnerable;
    private bool vulnerabilityHit;
    private Vector3Int vulnerableAnchorCell;
    private bool hasVulnerableAnchor;
    private int microPhaseIndex;

    // ─────────────────────────────────────────────
    #region Unity Events

    /// <summary>
    /// Validates required references and starts the main boss loop.
    /// </summary>
    private void Start()
    {
        if (grid == null)
        {
            Debug.LogError("[VampireQueenBossController] TilemapGridManager missing.");
            enabled = false;
            return;
        }
        microPhaseIndex = 0;
        mainRoutine = StartCoroutine(MainLoop());
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Main Loop

    /// <summary>
    /// Main boss state machine loop: enter animation delay, then Phase 1 micro-loop.
    /// </summary>
    private IEnumerator MainLoop()
    {
        State = BossState.Enter;

        yield return new WaitForSeconds(enterDelay);

        while (State != BossState.Dead)
        {
            yield return SummonRoutine();
            yield return AttackRoutine();
        }
    }

    private IEnumerator SummonRoutine()
    {
        var context = BuildBossContext();

        var bossAction = GetActionByNameOrFirst("Summon", context);
        if (bossAction == null)
            bossAction = summonAction;

        if (bossAction == null || !bossAction.CanRun(context))
            yield break;

        yield return bossAction.Execute(context);
    }

    private IEnumerator AttackRoutine()
    {
        var context = BuildBossContext();

        var bossAction = GetActionByNameOrFirst("Attack", context);
        if (bossAction == null)
            bossAction = attackAction;

        if (bossAction == null || !bossAction.CanRun(context))
            yield break;

        yield return bossAction.Execute(context);
    }

    #endregion
    
    // ─────────────────────────────────────────────
    #region Vulnerability

    /// <summary>
    /// Returns true when the boss is currently vulnerable to hits.
    /// </summary>
    public bool IsVulnerable => isVulnerable;

    /// <summary>
    /// Returns true when a vulnerability hit has been registered in the current window.
    /// </summary>
    public bool VulnerabilityHit => vulnerabilityHit;

    /// <summary>
    /// Starts the vulnerability window and clears the previous hit flag.
    /// </summary>
    public void BeginVulnerability()
    {
        isVulnerable = true;
        vulnerabilityHit = false;
    }

    /// <summary>
    /// Ends the vulnerability window.
    /// </summary>
    public void EndVulnerability()
    {
        isVulnerable = false;
    }

    /// <summary>
    /// Registers a vulnerability hit and applies damage once per window.
    /// </summary>
    public bool TryRegisterVulnerabilityHit(int damage = 1)
    {
        if (!isVulnerable || vulnerabilityHit)
            return false;

        TakeDamage(damage);
        vulnerabilityHit = true;
        return true;
    }

    /// <summary>
    /// Stores the anchor cell used for the vulnerable footprint.
    /// </summary>
    /// <param name="anchorCell">Top-left anchor cell of the vulnerable footprint.</param>
    public void SetVulnerableAnchor(Vector3Int anchorCell)
    {
        vulnerableAnchorCell = anchorCell;
        hasVulnerableAnchor = true;
    }

    /// <summary>
    /// Returns true if the provided cell overlaps the current vulnerable footprint.
    /// </summary>
    /// <param name="cell">Cell to test.</param>
    public bool IsCellInVulnerableFootprint(Vector3Int cell)
    {
        if (!isVulnerable || !hasVulnerableAnchor)
            return false;

        for (int x = 0; x < footprintSize.x; x++)
        {
            for (int y = 0; y < footprintSize.y; y++)
            {
                var footprintCell = vulnerableAnchorCell + new Vector3Int(x, -y, 0);
                if (cell == footprintCell)
                    return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// Current micro-phase index for Phase 1 looping behavior.
    /// </summary>
    public int MicroPhaseIndex => microPhaseIndex;

    /// <summary>
    /// Advances the micro-phase index for the Phase 1 loop.
    /// </summary>
    public void AdvanceMicroPhase()
    {
        microPhaseIndex++;
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Boss Finishes

    /// <summary>
    /// Called when boss death begins. Clears telegraphs and stops the main routine.
    /// </summary>
    protected override void OnBossDeathStarted()
    {
        StopMainLoop();
    }

    /// <summary>
    /// Called when player death begins. Clears telegraphs and stops the main routine.
    /// </summary>
    protected override void OnPlayerDeathStarted()
    {
        StopMainLoop();
    }

    /// <summary>
    /// Stops the main loop coroutine.
    /// </summary>
    private void StopMainLoop()
    {
        if (mainRoutine != null)
            StopCoroutine(mainRoutine);
    }

    #endregion
}

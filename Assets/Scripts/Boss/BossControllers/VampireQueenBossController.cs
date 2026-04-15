using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss controller for the Vampire Queen.
/// Phase 1 runs a micro-loop of Summon -> Vulnerable -> Retreat until phase transition.
/// </summary>
public class VampireQueenBossController : BossControllerBase
{
    /// <summary>
    /// Owner id used when placing/clearing boss telegraph previews in the grid telegraph layer.
    /// </summary>
    public const int PREVIEW_OWNER_BOSS = 7101;
    
    [Header("Intro")]
    [Tooltip("Delay (seconds) before the main loop starts after the enter animation.")]
    [SerializeField] private float enterDelay = 3f;

    [Header("Actions (Optional Overrides)")]
    [Tooltip("Explicit summon action override (used if action lookup by name fails).")]
    [SerializeField] private BossAction summonAction;
    [Tooltip("Explicit attack action override (used if action lookup by name fails).")]
    [SerializeField] private BossAction attackAction;
    [Tooltip("Explicit charge action override (used if action lookup by name fails).")]
    [SerializeField] private BossAction chargeAction;
    [Tooltip("Explicit meteor action override (used if action lookup by name fails).")]
    [SerializeField] private BossAction meteorAction;

    [Header("Phase 3 References")]
    [Tooltip("Transform whose position/scale defines the direct damage area.")]
    [SerializeField] private Transform damageableAreaTransform;
    [Tooltip("Optional object to enable in phase 3.")]
    [SerializeField] private GameObject fogFx;

    private Coroutine mainRoutine;
    private bool isVulnerable;
    private bool vulnerabilityHit;
    private Vector3Int vulnerableAnchorCell;
    private bool hasVulnerableAnchor;
    private int microPhaseIndex;
    private BossPhase lastPhase = BossPhase.Phase1;
    private Vector3Int damageableAnchorCell;
    private bool hasDamageableAnchor;
    private bool directDamageEnabled;
    private bool directHit;

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
        lastPhase = bossHealth != null ? bossHealth.CurrentPhase : BossPhase.Phase1;
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
        
        AudioManager.Instance?.PlaySFX("vamp_stage_enter", 0.6f);

        yield return new WaitForSeconds(enterDelay);

        while (State != BossState.Dead)
        {
            var phase = bossHealth != null ? bossHealth.CurrentPhase : BossPhase.Phase1;

            if (phase == BossPhase.Phase1)
            {
                yield return SummonRoutine();
                yield return AttackRoutine();
            }
            else if (phase == BossPhase.Phase2)
            {
                yield return ChargeRoutine();
            }
            else
            {
                yield return MeteorRoutine();
            }
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

    private IEnumerator ChargeRoutine()
    {
        var context = BuildBossContext();

        var bossAction = GetActionByNameOrFirst("Charge", context);
        if (bossAction == null)
            bossAction = chargeAction;

        if (bossAction == null || !bossAction.CanRun(context))
            yield break;

        yield return bossAction.Execute(context);
    }

    private IEnumerator MeteorRoutine()
    {
        var context = BuildBossContext();

        var bossAction = GetActionByNameOrFirst("Meteor", context);
        if (bossAction == null)
            bossAction = meteorAction;

        if (bossAction == null || !bossAction.CanRun(context))
            yield break;

        yield return bossAction.Execute(context);
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Phase Transitions

    protected override void OnEnable()
    {
        base.OnEnable();

        if (bossHealth != null)
            bossHealth.OnPhaseChanged += HandlePhaseChanged;
    }

    protected override void OnDisable()
    {
        if (bossHealth != null)
            bossHealth.OnPhaseChanged -= HandlePhaseChanged;

        base.OnDisable();
    }

    private void HandlePhaseChanged(BossPhase newPhase)
    {
        if (newPhase == lastPhase)
            return;

        microPhaseIndex = 0;
        lastPhase = newPhase;

        if (newPhase != BossPhase.Phase3)
        {
            directDamageEnabled = false;
            hasDamageableAnchor = false;
        }
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
        return IsCellInFootprint(vulnerableAnchorCell, cell);
    }

    /// <summary>
    /// Sets the anchor cell for a direct damage footprint (phase 3 body).
    /// </summary>
    public void SetDamageableAnchor(Vector3Int anchorCell)
    {
        damageableAnchorCell = anchorCell;
        hasDamageableAnchor = true;
    }

    /// <summary>
    /// Clears the direct damage footprint anchor.
    /// </summary>
    public void ClearDamageableAnchor()
    {
        hasDamageableAnchor = false;
    }

    /// <summary>
    /// Enables or disables direct damage (used after shield break).
    /// </summary>
    public override void SetDirectDamageEnabled(bool allowed)
    {
        directDamageEnabled = allowed;
    }

    /// <summary>
    /// Returns true if the provided cell overlaps the direct damage footprint.
    /// </summary>
    public bool IsCellInDamageableFootprint(Vector3Int cell)
    {
        if (!directDamageEnabled)
            return false;

        if (damageableAreaTransform != null)
            return IsCellInBounds(damageableAreaTransform, cell);

        if (!hasDamageableAnchor)
            return false;

        return IsCellInFootprint(damageableAnchorCell, cell);
    }

    private bool IsCellInFootprint(Vector3Int anchorCell, Vector3Int cell)
    {
        for (int x = 0; x < footprintSize.x; x++)
        {
            for (int y = 0; y < footprintSize.y; y++)
            {
                var footprintCell = anchorCell + new Vector3Int(x, -y, 0);
                if (cell == footprintCell)
                    return true;
            }
        }

        return false;
    }

    private bool IsCellInBounds(Transform source, Vector3Int cell)
    {
        if (grid == null || source == null)
            return false;

        Vector3 size = source.lossyScale;
        size.x = Mathf.Abs(size.x);
        size.y = Mathf.Abs(size.y);
        size.z = 0.01f;
        var bounds = new Bounds(source.position, size);

        const float epsilon = 0.001f;
        Vector3Int minCell = grid.WorldToCell(bounds.min);
        Vector3Int maxCell = grid.WorldToCell(bounds.max - new Vector3(epsilon, epsilon, 0f));

        return cell.x >= minCell.x &&
               cell.x <= maxCell.x &&
               cell.y >= minCell.y &&
               cell.y <= maxCell.y;
    }
    
    /// <summary>
    /// Returns true if a direct damage hit has been registered in the current window.
    /// </summary>
    public bool DirectHit => directHit;

    /// <summary>
    /// Clears the direct hit flag for phase 3 attacks (called by telegraph after hit is processed).
    /// </summary>
    public void ClearDirectHit() => directHit = false;
    
    /// <summary>
    /// Applies direct damage for Phase 3 hits.
    /// </summary>
    public bool TryRegisterDirectHit(int damage = 1)
    {
        if (!directDamageEnabled)
            return false;

        TakeDamage(damage);
        directHit = true;  // add this line
        return true;
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

    /// <summary>
    /// Applies direct damage to the boss (used by Phase 2 traps).
    /// </summary>
    public void ApplyChargeDamage(int damage)
    {
        TakeDamage(damage);
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
        
        fogFx?.SetActive(false);

        SetContactColliderActive(false);
        ClearDamageableAnchor();
        SetDirectDamageEnabled(false);

        StopMainLoop();
    }
    
    protected override void HandlePlayerDied()
    {
        if (bossHealth != null && bossHealth.CurrentPhase == BossPhase.Phase3)
        {
            var meteorAction = GetComponent<VampireQueenMeteorFieldAction>();
            meteorAction?.RestoreOriginalVisuals(this);
        }

        base.HandlePlayerDied();
    }

    /// <summary>
    /// Called when player death begins. Clears telegraphs and stops the main routine.
    /// </summary>
    protected override void OnPlayerDeathStarted()
    {
        postFx?.DisableAllOverrides(keepBloom: true);
        
        fogFx?.SetActive(false);

        SetContactColliderActive(false);
        ClearDamageableAnchor();
        SetDirectDamageEnabled(false);
        
        StopMainLoop();
    }

    /// <summary>
    /// Stops the main loop coroutine.
    /// </summary>
    private void StopMainLoop()
    {
        if (grid != null)
            grid.ClearTelegraphForOwner(PREVIEW_OWNER_BOSS);
        
        if (mainRoutine != null)
            StopCoroutine(mainRoutine);
    }

    #endregion

    #region Accessors
    // ─────────────────────────────────────────────
    public GameObject FogFx => fogFx;

    #endregion
}

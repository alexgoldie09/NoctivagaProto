using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base boss controller responsible for shared boss lifecycle, animation triggers, action lookup,
/// damage handling, and end-of-fight flow (boss death vs player death).
/// </summary>
public abstract class BossControllerBase : MonoBehaviour
{
    /// <summary>
    /// High-level boss state used by actions and boss logic.
    /// </summary>
    public enum BossState
    {
        Enter,
        Flight,
        Rest,
        Hurt,
        Dead
    }

    [Header("Base References")]
    [Tooltip("Boss health component used for damage and death events. If null, it will be auto-fetched in Awake().")]
    [SerializeField] protected BossHealth bossHealth;
    [Tooltip("Animator used for boss animation triggers. If null, it will be auto-fetched in Awake().")]
    [SerializeField] protected Animator animator;
    [Tooltip("SpriteRenderer used for boss facing direction. If null, it will be auto-fetched in Awake().")]
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [Tooltip("Primary grid manager used for cell queries, world conversions, and telegraph clearing.")]
    [SerializeField] protected TilemapGridManager grid;
    [Tooltip("Player reference used by action context. If null, will be auto-found in Awake().")]
    [SerializeField] protected PlayerController player;
    [Tooltip("Post FX Processing reference used by action context. If null, will be auto-found in Awake().")]
    [SerializeField] protected BossPostFxController postFx;
    [Tooltip("If true, the sprite faces left by default (flipX false). If false, sprite faces right by default.")]
    [SerializeField] protected bool spriteFacesLeftByDefault = true;

    [Header("Actions")]
    [Tooltip("Action modules available to this boss. If Auto Collect Actions is enabled, this list is rebuilt from components at runtime.")]
    [SerializeField] private List<BossAction> actions = new();
    [Tooltip("If true, clears and collects BossAction components on this GameObject at runtime (Awake).")]
    [SerializeField] private bool autoCollectActions = true;
    
    [Header("Boss Footprint")]
    [Tooltip("Size of the boss footprint in cells. Anchor is top-left; footprint covers (x+, y-).")]
    [SerializeField] protected Vector2Int footprintSize = new(2, 2);
    [Tooltip("Draw footprint gizmos when selected.")]
    [SerializeField] private bool drawFootprintGizmos = true;
    [SerializeField] private bool drawFootprintLabels = true;
    
    [Header("Shield (Optional)")]
    [SerializeField] protected BossShieldHealth shieldHealth;
    [Tooltip("Optional object to disable when the shield breaks (e.g. bubble sprite root).")]
    [SerializeField] protected GameObject shieldRoot;

    [Header("Damage Force")]
    [Tooltip("If true, allow camera shake when the boss takes damage.")]
    [SerializeField] protected bool allowDamageShake = true;
    [Tooltip("Shake force applied when damage occurs (if Allow Damage Shake is enabled).")]
    [SerializeField] protected float damageShakeForce = 0.8f;

    [Header("Damage Flash")]
    [SerializeField] private Color damageFlashColor = Color.white;
    [Min(0f)]
    [SerializeField] private float damageFlashTotalDuration = 0.5f;
    [Min(0.01f)]
    [SerializeField] private float damageFlashInterval = 0.1f;
    
    [Header("Contact Damage")]
    [Tooltip("Optional collider used to damage the player on contact.")]
    [SerializeField] private Collider2D contactCollider;

    private Coroutine damageFlashRoutine;
    private Color initialSpriteColor = Color.white;
    private bool shieldBound;

    // ─────────────────────────────────────────────
    #region Properties
    /// <summary>
    /// Current boss state.
    /// </summary>
    public BossState State { get; protected set; } = BossState.Enter;

    /// <summary>
    /// Boss health reference for derived controllers and action modules.
    /// </summary>
    public BossHealth BossHealth => bossHealth;

    /// <summary>
    /// Action modules configured for this boss.
    /// </summary>
    public IReadOnlyList<BossAction> Actions => actions;
    
    /// <summary>
    /// Grid manager reference for derived controllers and action modules.
    /// </summary>
    public TilemapGridManager Grid => grid;

    /// <summary>
    /// Player reference for derived controllers and action modules.
    /// </summary>
    public PlayerController Player => player;
    
    /// <summary>
    /// Post Fx Controller reference for action modules.
    /// </summary>
    public BossPostFxController PostFx => postFx;

    /// <summary>
    /// Boss footprint size in cells.
    /// </summary>
    public Vector2Int FootprintSize => footprintSize;

    /// <summary>
    /// Whether the boss is allowed to trigger camera shake on damage.
    /// </summary>
    public bool AllowDamageShake => allowDamageShake;

    /// <summary>
    /// Shake force used when damage occurs.
    /// </summary>
    public float DamageShakeForce => damageShakeForce;

    #endregion
    // ─────────────────────────────────────────────
    #region Unity Lifecycle

    /// <summary>
    /// Initializes shared component references if not assigned, and optionally auto-collects action modules.
    /// </summary>
    protected virtual void Awake()
    {
        if (bossHealth == null)
            bossHealth = GetComponent<BossHealth>();
        
        if (postFx == null)
            postFx = GetComponent<BossPostFxController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            initialSpriteColor = spriteRenderer.color;
        
        if (grid == null)
            grid = TilemapGridManager.Instance;

        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        
        if (contactCollider == null)
            contactCollider = GetComponent<Collider2D>();

        if (contactCollider != null)
        {
            contactCollider.isTrigger = true;
            contactCollider.enabled = false;
        }

        if (!autoCollectActions)
            return;

        actions.Clear();
        GetComponents(actions);
    }

    /// <summary>
    /// Subscribes to boss health events.
    /// </summary>
    protected virtual void OnEnable()
    {
        if (bossHealth == null)
            return;

        bossHealth.OnDied += HandleBossDied;
        bossHealth.OnPlayerDied += HandlePlayerDied;
    }

    /// <summary>
    /// Unsubscribes from boss health events.
    /// </summary>
    protected virtual void OnDisable()
    {
        if (bossHealth == null)
            return;

        bossHealth.OnDied -= HandleBossDied;
        bossHealth.OnPlayerDied -= HandlePlayerDied;
        
        if (contactCollider != null)
            contactCollider.enabled = false;
        
        UnbindShield();
    }
    
    /// <summary>
    /// Enables or disables the optional contact damage collider.
    /// </summary>
    /// <param name="active">Whether contact damage should be active.</param>
    public void SetContactColliderActive(bool active)
    {
        if (contactCollider != null)
            contactCollider.enabled = active;
    }

    /// <summary>
    /// Overrides the active contact collider (useful for body swaps).
    /// </summary>
    public void SetContactCollider(Collider2D newCollider)
    {
        if (contactCollider != null)
            contactCollider.enabled = false;

        contactCollider = newCollider;

        if (contactCollider != null)
            contactCollider.isTrigger = true;
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Death Flow

    /// <summary>
    /// Handles boss death and triggers the end-of-boss flow.
    /// </summary>
    protected virtual void HandleBossDied()
    {
        if (State == BossState.Dead)
            return;

        State = BossState.Dead;

        TriggerAnim("Death");
        UnbindShield();
        OnBossDeathStarted();

        StartCoroutine(EndAfterDeathDelay(GetBossDeathDelay()));
    }

    /// <summary>
    /// Handles player death and triggers boss cleanup/end-of-fight flow.
    /// </summary>
    protected virtual void HandlePlayerDied()
    {
        if (State == BossState.Dead)
            return;

        State = BossState.Dead;

        TriggerAnim("Tease");
        UnbindShield();
        OnPlayerDeathStarted();

        StartCoroutine(EndAfterPlayerDelay(GetPlayerDeathDelay()));
    }

    /// <summary>
    /// Hook for boss-specific cleanup when boss death begins.
    /// </summary>
    protected virtual void OnBossDeathStarted() { }

    /// <summary>
    /// Hook for boss-specific cleanup when player death begins.
    /// </summary>
    protected virtual void OnPlayerDeathStarted() { }

    /// <summary>
    /// Returns the delay (in seconds) before ending the boss fight after boss death.
    /// </summary>
    /// <returns>Delay in seconds.</returns>
    protected virtual float GetBossDeathDelay() => 3.0f;

    /// <summary>
    /// Returns the delay (in seconds) before ending the fight after player death.
    /// </summary>
    /// <returns>Delay in seconds.</returns>
    protected virtual float GetPlayerDeathDelay() => 3.0f;

    /// <summary>
    /// Ends the boss fight after the specified delay.
    /// </summary>
    /// <param name="delay">Delay (in seconds) before ending the fight.</param>
    protected virtual IEnumerator EndAfterDeathDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameManager.Instance?.BossDefeated();
    }

    /// <summary>
    /// Ends the fight after the specified delay following player death.
    /// </summary>
    /// <param name="delay">Delay (in seconds) before ending the fight.</param>
    protected virtual IEnumerator EndAfterPlayerDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameManager.Instance?.PlayerKilled();
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Animation / Damage

    /// <summary>
    /// Triggers an animation by name if an animator is assigned.
    /// </summary>
    /// <param name="trigger">Animator trigger parameter name.</param>
    protected void TriggerAnim(string trigger)
    {
        if (animator != null && !string.IsNullOrEmpty(trigger))
            animator.SetTrigger(trigger);
    }

    /// <summary>
    /// Applies damage to the boss health component.
    /// </summary>
    /// <param name="amount">Requested damage amount (clamped to at least 1).</param>
    protected virtual void TakeDamage(int amount)
    {
        if (bossHealth == null)
            return;

        bossHealth.ApplyDamage(Mathf.Max(1, amount));
        FlashDamage();
    }

    private void FlashDamage()
    {
        if (spriteRenderer == null || damageFlashTotalDuration <= 0f || damageFlashInterval <= 0f)
            return;

        if (damageFlashRoutine != null)
            StopCoroutine(damageFlashRoutine);

        damageFlashRoutine = StartCoroutine(FlashDamageRoutine());
    }

    private IEnumerator FlashDamageRoutine()
    {
        if (spriteRenderer == null)
            yield break;

        float elapsed = 0f;
        bool useFlash = true;

        while (elapsed < damageFlashTotalDuration)
        {
            spriteRenderer.color = useFlash ? damageFlashColor : initialSpriteColor;
            useFlash = !useFlash;

            float step = Mathf.Min(damageFlashInterval, damageFlashTotalDuration - elapsed);
            elapsed += step;
            yield return new WaitForSeconds(step);
        }

        spriteRenderer.color = initialSpriteColor;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (GameManager.Instance != null && other.CompareTag("Player"))
            GameManager.Instance.PlayerKilled();
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Action Lookup Helpers

    /// <summary>
    /// Returns the action at the specified index if it exists.
    /// </summary>
    /// <param name="index">Index into the configured action list.</param>
    /// <returns>The action at the index, or null if invalid.</returns>
    protected BossAction GetActionAt(int index)
    {
        if (actions == null || index < 0 || index >= actions.Count)
            return null;

        return actions[index];
    }

    /// <summary>
    /// Returns the first action with the matching name.
    /// </summary>
    /// <param name="actionName">Action name to match.</param>
    /// <returns>The first matching action, or null if none found.</returns>
    private BossAction GetActionByName(string actionName)
    {
        if (actions == null || string.IsNullOrEmpty(actionName))
            return null;

        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action != null && action.ActionName == actionName)
                return action;
        }

        return null;
    }

    /// <summary>
    /// Returns the named action or falls back to the first runnable action for the provided context.
    /// </summary>
    /// <param name="actionName">Action name to attempt first.</param>
    /// <param name="context">Context used to evaluate runnable actions.</param>
    /// <returns>A named action if present, otherwise the first runnable action for the context, or null.</returns>
    protected BossAction GetActionByNameOrFirst(string actionName, BossContext context)
    {
        var named = GetActionByName(actionName);
        if (named != null)
            return named;

        if (actions == null || context == null)
            return null;

        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action != null && action.CanRun(context))
                return action;
        }

        return null;
    }

    /// <summary>
    /// Returns an action by base name and phase suffix (e.g., Base_Phase2, Base_P2).
    /// </summary>
    /// <param name="baseName">Base action name prefix.</param>
    /// <param name="phase">Phase to resolve suffix for.</param>
    /// <returns>Resolved action for the phase, or null if not found.</returns>
    protected BossAction GetActionByPhase(string baseName, BossPhase phase)
    {
        if (string.IsNullOrEmpty(baseName))
            return null;

        var phaseName = phase.ToString();
        var shortName = phase switch
        {
            BossPhase.Phase2 => "P2",
            BossPhase.Phase3 => "P3",
            _ => "P1",
        };

        return GetActionByName($"{baseName}_{phaseName}") ?? GetActionByName($"{baseName}_{shortName}");
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Movement Helpers

    /// <summary>
    /// Flies the boss toward a world point, updating facing along the way.
    /// </summary>
    /// <param name="targetWorld">Target world position.</param>
    /// <param name="speed">Movement speed in world units per second.</param>
    /// <param name="animationTrigger">Optional animation trigger to play when movement starts.</param>
    /// <param name="arriveEpsilon">Distance threshold for arrival.</param>
    public IEnumerator FlyToWorldPoint(
        Vector3 targetWorld,
        float speed,
        string animationTrigger = "Fly",
        float arriveEpsilon = 0.05f)
    {
        if (!string.IsNullOrEmpty(animationTrigger))
            TriggerAnim(animationTrigger);

        if (speed <= 0f)
        {
            transform.position = targetWorld;
            yield break;
        }

        while (Vector3.Distance(transform.position, targetWorld) > arriveEpsilon)
        {
            if (State == BossState.Dead)
                yield break;

            UpdateFacingTowards(targetWorld);

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetWorld,
                speed * Time.deltaTime
            );

            yield return null;
        }
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Contact Damage
    /// <summary>
    /// True if this boss has a shield component assigned or found.
    /// </summary>
    public bool HasShield => shieldHealth != null;

    /// <summary>
    /// Current shield component if present (may be null).
    /// </summary>
    public BossShieldHealth ShieldHealth => shieldHealth;

    /// <summary>
    /// Override in bosses that support direct damage gating (e.g. shield must break first).
    /// Default is no-op so bosses without this feature are safe.
    /// </summary>
    public virtual void SetDirectDamageEnabled(bool allowed) { }

    /// <summary>
    /// Binds optional shield logic (subscribes to break event and updates direct damage gating).
    /// Safe if no shield exists.
    /// </summary>
    public void BindShield()
    {
        if (shieldHealth == null)
            shieldHealth = GetComponentInChildren<BossShieldHealth>();

        // No shield: allow direct damage (safe default).
        if (shieldHealth == null)
        {
            SetDirectDamageEnabled(true);
            return;
        }

        // With shield: allow damage only if shield already broken.
        SetDirectDamageEnabled(shieldHealth.IsBroken);
        
        if (shieldHealth.IsBroken)
            HandleShieldBroken();

        if (!shieldBound)
        {
            shieldHealth.OnShieldBroken += HandleShieldBroken;
            shieldBound = true;
        }
    }

    /// <summary>
    /// Unbinds shield event subscription (safe).
    /// </summary>
    public void UnbindShield()
    {
        if (shieldHealth == null || !shieldBound)
            return;

        shieldHealth.OnShieldBroken -= HandleShieldBroken;
        shieldBound = false;
    }

    /// <summary>
    /// Called when shield breaks: hides root and enables direct damage.
    /// </summary>
    protected virtual void HandleShieldBroken()
    {
        if (shieldRoot != null)
            shieldRoot.SetActive(false);

        SetDirectDamageEnabled(true);
    }
    
    #endregion
    // ─────────────────────────────────────────────
    #region Footprint Helpers

    /// <summary>
    /// Builds the set of cells covered by the boss footprint for a given top-left anchor cell.
    /// Default footprint is 2x2: (0,0), (1,0), (0,-1), (1,-1).
    /// </summary>
    /// <param name="anchorTopLeft">Top-left anchor cell.</param>
    /// <returns>Set of all cells covered by the footprint.</returns>
    public HashSet<Vector3Int> GetFootprintCells(Vector3Int anchorTopLeft)
    {
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

    /// <summary>
    /// Returns the world-space center of the footprint based on its cell centers.
    /// </summary>
    /// <param name="anchorTopLeft">Top-left footprint anchor cell.</param>
    /// <returns>Average world-space center of footprint cell centers.</returns>
    public Vector3 GetFootprintCenterWorld(Vector3Int anchorTopLeft)
    {
        if (grid == null)
            return transform.position;

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

    /// <summary>
    /// Attempts to pick a valid footprint anchor from painted ground cells.
    /// </summary>
    public bool TryPickFootprintAnchor(out Vector3Int anchorCell)
    {
        anchorCell = default;
        if (grid == null)
            return false;

        var candidates = grid.GetAllPaintedGroundCells();
        if (candidates == null || candidates.Count == 0)
            return false;

        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[Random.Range(0, candidates.Count)];
            if (!IsFootprintValid(cell))
                continue;

            anchorCell = cell;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the footprint can occupy the grid starting at the provided anchor cell.
    /// </summary>
    public bool IsFootprintValid(Vector3Int anchorTopLeft)
    {
        if (grid == null)
            return false;

        for (int x = 0; x < footprintSize.x; x++)
        {
            for (int y = 0; y < footprintSize.y; y++)
            {
                var cell = anchorTopLeft + new Vector3Int(x, -y, 0);
                if (!grid.CanEnemyEnterCell(cell))
                    return false;
            }
        }

        return true;
    }

    protected virtual Vector3Int GetFootprintAnchorForGizmos()
    {
        return grid != null ? grid.WorldToCell(transform.position) : Vector3Int.zero;
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Action-Facing API

    /// <summary>
    /// Sets the boss state from action modules.
    /// </summary>
    /// <param name="state">State to set.</param>
    public void SetState(BossState state)
    {
        State = state;
    }

    /// <summary>
    /// Plays an animation trigger from action modules.
    /// </summary>
    /// <param name="trigger">Animator trigger parameter name.</param>
    public void PlayAnimation(string trigger)
    {
        TriggerAnim(trigger);
    }

    /// <summary>
    /// Overrides the active animator and sprite renderer (useful for body swaps).
    /// </summary>
    public void SetVisuals(Animator newAnimator, SpriteRenderer newSpriteRenderer)
    {
        if (newAnimator != null)
            animator = newAnimator;

        if (newSpriteRenderer != null)
        {
            spriteRenderer = newSpriteRenderer;
            initialSpriteColor = spriteRenderer.color;
        }
    }

    /// <summary>
    /// Updates sprite facing direction toward a world point.
    /// Default sprite faces left; flipX is enabled when target is to the right.
    /// </summary>
    /// <param name="targetWorld">World position to face toward.</param>
    public void UpdateFacingTowards(Vector3 targetWorld)
    {
        if (spriteRenderer == null)
            return;

        float dx = targetWorld.x - transform.position.x;

        if (Mathf.Abs(dx) < 0.01f)
            return;

        bool faceRight = dx > 0f;
        spriteRenderer.flipX = spriteFacesLeftByDefault ? faceRight : !faceRight;
    }
    
    /// <summary>
    /// Builds a <see cref="BossContext"/> for action modules.
    /// </summary>
    /// <returns>Context containing shared references needed by boss actions.</returns>
    protected BossContext BuildBossContext()
    {
        return new BossContext
        {
            controller = this,
            health = bossHealth,
            grid = grid,
            player = player,
            animator = animator,
            spriteRenderer = spriteRenderer,
            postFx = postFx,
            shieldHealth = shieldHealth
        };
    }
    #endregion
    // ─────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawFootprintGizmos)
            return;

        var activeGrid = grid;
        if (activeGrid == null)
            activeGrid = TilemapGridManager.Instance;

        if (activeGrid == null)
            return;

        Vector3Int anchor = GetFootprintAnchorForGizmos();
        var cells = GetFootprintCells(anchor);

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.blueViolet;

        foreach (var cell in cells)
        {
            Vector3 center = activeGrid.CellToWorldCenter(cell);
            Vector3 size = Vector3.one * 0.9f;
            Gizmos.DrawWireCube(center, size);

            if (drawFootprintLabels)
                UnityEditor.Handles.Label(center + Vector3.up * 0.3f, $"{cell.x},{cell.y}");
        }
    }
#endif
}

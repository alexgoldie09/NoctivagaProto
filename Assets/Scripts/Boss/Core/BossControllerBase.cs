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

    [Header("Actions")]
    [Tooltip("Action modules available to this boss. If Auto Collect Actions is enabled, this list is rebuilt from components at runtime.")]
    [SerializeField] private List<BossAction> actions = new();
    [Tooltip("If true, clears and collects BossAction components on this GameObject at runtime (Awake).")]
    [SerializeField] private bool autoCollectActions = true;

    [Header("Damage Force")]
    [Tooltip("If true, allow camera shake when the boss takes damage.")]
    [SerializeField] protected bool allowDamageShake = true;
    [Tooltip("Shake force applied when damage occurs (if Allow Damage Shake is enabled).")]
    [SerializeField] protected float damageShakeForce = 0.8f;

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

        if (animator == null)
            animator = GetComponent<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

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

        TriggerAnim("Death");
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
        if (bossHealth != null)
            bossHealth.ApplyDamage(Mathf.Max(1, amount));
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

        spriteRenderer.flipX = dx > 0f;
    }

    #endregion
    // ─────────────────────────────────────────────
}

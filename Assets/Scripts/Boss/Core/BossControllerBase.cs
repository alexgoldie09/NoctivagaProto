using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base boss controller that centralizes shared lifecycle, animation, and damage handling.
/// </summary>
public abstract class BossControllerBase : MonoBehaviour
{
    public enum BossState { Enter, Flight, Rest, Hurt, Dead }

    [Header("Base References")]
    [SerializeField] protected BossHealth bossHealth;
    [SerializeField] protected Animator animator;
    [SerializeField] protected SpriteRenderer spriteRenderer;
    
    [Header("Actions")]
    [SerializeField] private List<BossAction> actions = new();
    [SerializeField] private bool autoCollectActions = true;

    [Header("Damage Force")]
    [Tooltip("If true, allow camera shake.")]
    [SerializeField] protected bool allowDamageShake = true;
    [Tooltip("Shake force for camera on damage.")]
    [SerializeField] protected float damageShakeForce = 0.8f;

    public BossState State { get; protected set; } = BossState.Enter;

    /// <summary>
    /// Initializes shared component references if they are not assigned.
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
        if (bossHealth != null)
        {
            bossHealth.OnDied += HandleBossDied;
            bossHealth.OnPlayerDied += HandlePlayerDied;
        }
    }

    /// <summary>
    /// Unsubscribes from boss health events.
    /// </summary>
    protected virtual void OnDisable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnDied -= HandleBossDied;
            bossHealth.OnPlayerDied -= HandlePlayerDied;
        }
    }

    /// <summary>
    /// Handles boss death and triggers end-of-boss flow.
    /// </summary>
    protected virtual void HandleBossDied()
    {
        if (State == BossState.Dead) return;

        State = BossState.Dead;
        TriggerAnim("Death");
        OnBossDeathStarted();
        StartCoroutine(EndAfterDeathDelay(GetBossDeathDelay()));
    }

    /// <summary>
    /// Handles player death and triggers boss cleanup.
    /// </summary>
    protected virtual void HandlePlayerDied()
    {
        if (State == BossState.Dead) return;

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
    protected virtual float GetBossDeathDelay() => 3.0f;

    /// <summary>
    /// Returns the delay (in seconds) before ending the fight after player death.
    /// </summary>
    protected virtual float GetPlayerDeathDelay() => 3.0f;

    /// <summary>
    /// Ends the boss fight after the specified delay.
    /// </summary>
    protected virtual IEnumerator EndAfterDeathDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameManager.Instance?.BossDefeated();
    }

    /// <summary>
    /// Ends the fight after the specified delay following player death.
    /// </summary>
    protected virtual IEnumerator EndAfterPlayerDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameManager.Instance?.PlayerKilled();
    }

    /// <summary>
    /// Triggers an animation by name if an animator is assigned.
    /// </summary>
    protected void TriggerAnim(string trigger)
    {
        if (animator != null && !string.IsNullOrEmpty(trigger))
            animator.SetTrigger(trigger);
    }

    /// <summary>
    /// Applies damage to the boss health component.
    /// </summary>
    protected virtual void TakeDamage(int amount)
    {
        if (bossHealth != null)
            bossHealth.ApplyDamage(Mathf.Max(1, amount));
    }

    /// <summary>
    /// Returns the boss health component for derived behaviors.
    /// </summary>
    public BossHealth BossHealth => bossHealth;
    
    /// <summary>
    /// Returns the configured action list for this boss.
    /// </summary>
    public IReadOnlyList<BossAction> Actions => actions;

    /// <summary>
    /// Returns the action at the specified index if it exists.
    /// </summary>
    protected BossAction GetActionAt(int index)
    {
        if (actions == null || index < 0 || index >= actions.Count)
            return null;

        return actions[index];
    }
    
    /// <summary>
    /// Returns the first action with the matching name.
    /// </summary>
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
    /// Returns the named action or falls back to the first runnable action for the context.
    /// </summary>
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

    /// <summary>
    /// Whether the boss can trigger camera shake on damage.
    /// </summary>
    public bool AllowDamageShake => allowDamageShake;

    /// <summary>
    /// Shake force used when damage occurs.
    /// </summary>
    public float DamageShakeForce => damageShakeForce;
    
    /// <summary>
    /// Sets the boss state from action modules.
    /// </summary>
    public void SetState(BossState state)
    {
        State = state;
    }

    /// <summary>
    /// Plays an animation trigger from action modules.
    /// </summary>
    public void PlayAnimation(string trigger)
    {
        TriggerAnim(trigger);
    }

    /// <summary>
    /// Updates sprite facing direction toward a world point.
    /// </summary>
    public void UpdateFacingTowards(Vector3 targetWorld)
    {
        if (spriteRenderer == null)
            return;

        float dx = targetWorld.x - transform.position.x;

        if (Mathf.Abs(dx) < 0.01f)
            return;

        spriteRenderer.flipX = dx > 0f;
    }
}

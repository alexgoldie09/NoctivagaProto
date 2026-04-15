using System.Collections;
using UnityEngine;

/// <summary>
/// Boss action component that runs the Vampire Queen vulnerable attack window.
/// </summary>
public class VampireQueenAttackAction : BossAction
{
    private enum ShotPattern
    {
        Cardinal,
        Diagonal
    }

    [Header("Projectile Attacks")]
    [Tooltip("Projectile prefab fired during the vulnerable window.")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Spawn offset for projectiles relative to the queen.")]
    [SerializeField] private Vector2 projectileSpawnOffset = Vector2.zero;

    [Header("Powerup")]
    [Tooltip("Powerup prefab spawned when the queen becomes vulnerable.")]
    [SerializeField] private GameObject meleePowerupPrefab;

    [Header("Animation")]
    [Tooltip("Animator trigger to play when the queen becomes vulnerable.")]
    [SerializeField] private string vulnerableAnimationTrigger = "Idle";
    [Tooltip("Animator trigger to play when the queen becomes hurt.")]
    [SerializeField] private string hurtAnimationTrigger = "Hurt";
    [Tooltip("Seconds to hold on the hurt animation before retreating.")]
    [SerializeField] private float hurtAnimationHold = 0.35f; 
    [Tooltip("Animator trigger to play when the queen fires projectiles.")]
    [SerializeField] private string projectileFireTrigger = "Summon";
    [Tooltip("Delay after firing projectiles before returning to vulnerable animation.")]
    [SerializeField] private float projectileFireHold = 0.8f;
    [Tooltip("Animator trigger to play when the queen is retreating.")]
    [SerializeField] private string retreatAnimationTrigger = "Fly";

    [Header("Phase Data (Phase 1 Only)")]
    [SerializeField] private VampireQueenAttackPhaseData phaseData;

    /// <summary>
    /// Returns whether this action can run given the current boss context.
    /// </summary>
    public override bool CanRun(BossContext context)
    {
        return context?.controller is VampireQueenBossController;
    }

    /// <summary>
    /// Executes the vulnerable window: descend, spawn powerup, fire projectiles, wait for hit or timeout.
    /// </summary>
    public override IEnumerator Execute(BossContext context)
    {
        var queen = context?.controller as VampireQueenBossController;
        if (queen == null)
            yield break;
        
        SetPhaseObjectsActive(false);

        var data = phaseData;
        var micro = phaseData != null
            ? phaseData.GetMicroPhase(queen.MicroPhaseIndex)
            : default;

        float vulnerableDuration = data != null ? micro.vulnerableDuration : 3f;
        float retreatDelay = data != null ? micro.retreatDelay : 0.3f;
        float descentSpeed = data != null ? micro.descentSpeed : 3f;
        int shotBursts = data != null ? micro.shotBursts : 2;
        float shotInterval = data != null ? micro.shotInterval : 0.4f;

        if (TryGetVulnerableAnchor(context, out var anchorCell, out var targetWorld))
        {
            queen.SetVulnerableAnchor(anchorCell);
            queen.SetState(BossControllerBase.BossState.Flight);
            AudioManager.Instance?.PlaySFX("vamp_flying", 0.7f);
            yield return queen.FlyToWorldPoint(targetWorld, descentSpeed);
        }

        queen.SetState(BossControllerBase.BossState.Rest);
        if (!string.IsNullOrEmpty(vulnerableAnimationTrigger))
            queen.PlayAnimation(vulnerableAnimationTrigger);

        queen.BeginVulnerability();
        queen.SetContactColliderActive(true);

        TrySpawnPowerup(context);

        float elapsed = 0f;
        float nextShotTime = 0f;
        int burstsRemaining = Mathf.Max(0, shotBursts);

        while (elapsed < vulnerableDuration)
        {
            if (queen.State == BossControllerBase.BossState.Dead)
                yield break;

            if (queen.VulnerabilityHit)
            {
                if (!string.IsNullOrEmpty(hurtAnimationTrigger))
                    queen.PlayAnimation(hurtAnimationTrigger);
                break;
            }

            if (burstsRemaining > 0 && elapsed >= nextShotTime)
            {
                FireBurst();
                if (projectileFireHold > 0f)
                    yield return new WaitForSeconds(projectileFireHold);

                if (!string.IsNullOrEmpty(vulnerableAnimationTrigger))
                    queen.PlayAnimation(vulnerableAnimationTrigger);

                burstsRemaining--;
                nextShotTime = elapsed + Mathf.Max(0f, shotInterval);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        queen.EndVulnerability();
        
        if (queen.VulnerabilityHit && queen.BossHealth.CurrentPhase == BossPhase.Phase1)
            queen.AdvanceMicroPhase();
        
        queen.SetContactColliderActive(false);
        
        if (queen.VulnerabilityHit && hurtAnimationHold > 0f)
            yield return new WaitForSeconds(hurtAnimationHold);

        if (queen.VulnerabilityHit && retreatDelay > 0f)
            yield return new WaitForSeconds(retreatDelay);

        if (!string.IsNullOrEmpty(retreatAnimationTrigger))
            queen.PlayAnimation(retreatAnimationTrigger);
    }

    private void FireBurst()
    {
        if (projectilePrefab == null)
            return;

        ShotPattern pattern = Random.value <= 0.5f ? ShotPattern.Cardinal : ShotPattern.Diagonal;
        Vector2[] dirs = pattern == ShotPattern.Cardinal
            ? new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left }
            : new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1), new Vector2(-1, 1) };

        Vector3 spawn = transform.position + (Vector3)projectileSpawnOffset;

        foreach (var dir in dirs)
        {
            var projectileObject = Instantiate(projectilePrefab, spawn, Quaternion.identity);
            var projectile = projectileObject != null ? projectileObject.GetComponent<EnemyStraightProjectile>() : null;
            if (projectile != null)
                projectile.Initialize(dir);
        }

        var queen = GetComponent<VampireQueenBossController>();
        if (queen != null && !string.IsNullOrEmpty(projectileFireTrigger))
            queen.PlayAnimation(projectileFireTrigger);
    }

    private bool TryGetVulnerableAnchor(BossContext context, out Vector3Int anchorCell, out Vector3 world)
    {
        anchorCell = default;
        world = Vector3.zero;

        if (context?.grid == null || context.controller == null)
            return false;

        if (context.controller.TryPickFootprintAnchor(out anchorCell))
        {
            world = context.controller.GetFootprintCenterWorld(anchorCell);
            return true;
        }

        return false;
    }

    private void TrySpawnPowerup(BossContext context)
    {
        if (meleePowerupPrefab == null || context?.grid == null)
            return;

        var candidates = context.grid.GetAllPaintedGroundCells();
        if (candidates == null || candidates.Count == 0)
            return;

        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[Random.Range(0, candidates.Count)];
            if (!context.grid.CanEnterCell(cell))
                continue;

            Vector3 spawn = context.grid.CellToWorldCenter(cell);
            Instantiate(meleePowerupPrefab, spawn, Quaternion.identity);
            return;
        }
    }
}

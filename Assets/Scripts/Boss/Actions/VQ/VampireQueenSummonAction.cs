using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss action component that runs the Vampire Queen summon sequence.
/// Tracks spawned enemies and completes once the wave is cleared.
/// </summary>
public class VampireQueenSummonAction : BossAction
{
    [Header("Summon Anchors")]
    [Tooltip("World-space anchor where the Queen floats while summoning.")]
    [SerializeField] private Transform summonAnchor;
    [Tooltip("Optional spawn points for enemies. If empty, random valid grid cells are used.")]
    [SerializeField] private Transform[] enemySpawnPoints;
    [Tooltip("Optional portal prefab spawned before the enemy appears.")]
    [SerializeField] private GameObject summonPortalPrefab;
    [Tooltip("Delay after spawning the portal before the enemy appears.")]
    [SerializeField] private float portalSpawnDelay = 0.2f;

    [Header("Summon Settings")]
    [Tooltip("Enemy prefabs available for summoning.")]
    [SerializeField] private List<GameObject> enemyPrefabs = new();
    [Tooltip("Fallback wave size if no phase tuning is assigned.")]
    [SerializeField] private int defaultWaveSize = 4;
    [Tooltip("Fallback summon interval if no phase tuning is assigned.")]
    [SerializeField] private float defaultSummonInterval = 1.25f;
    [Tooltip("Maximum number of concurrent enemies; 0 or less means no cap.")]
    [SerializeField] private int defaultMaxConcurrent = 0;
    [Tooltip("Fallback fly movement speed if no phase tuning is assigned.")]
    [SerializeField] private float defaultFlyMoveSpeed = 3.0f;

    [Header("Animation")]
    [Tooltip("Animator trigger to play when a summon occurs.")]
    [SerializeField] private string summonAnimationTrigger = "Summon";
    [Tooltip("Animator trigger to return to idle between summons.")]
    [SerializeField] private string idleAnimationTrigger = "Idle";
    [Tooltip("Optional delay after playing the summon animation before returning to idle.")]
    [SerializeField] private float summonAnimationHold = 0.1f;

    [Header("Phase Data (Phase 1 Only)")]
    [SerializeField] private VampireQueenSummonPhaseData phaseData;

    private readonly List<GameObject> activeEnemies = new();

    /// <summary>
    /// Returns whether this action can run given the current boss context.
    /// </summary>
    public override bool CanRun(BossContext context)
    {
        return context?.controller is VampireQueenBossController;
    }

    /// <summary>
    /// Executes the summon wave and waits for all spawned enemies to be cleared.
    /// </summary>
    public override IEnumerator Execute(BossContext context)
    {
        var queen = context?.controller as VampireQueenBossController;
        if (queen == null)
            yield break;
        
        SetPhaseObjectsActive(true);

        var data = phaseData;
        var micro = phaseData != null
            ? phaseData.GetMicroPhase(queen.MicroPhaseIndex)
            : default;

        int waveSize = data != null ? Mathf.Max(1, micro.waveSize) : Mathf.Max(1, defaultWaveSize);
        float summonInterval = data != null ? Mathf.Max(0f, micro.summonInterval) : Mathf.Max(0f, defaultSummonInterval);
        int maxConcurrent = data != null ? micro.maxConcurrentEnemies : defaultMaxConcurrent;
        float flyMoveSpeed = data != null ? micro.flyMoveSpeed : defaultFlyMoveSpeed;
        
        if (summonAnchor != null)
        {
            queen.SetState(BossControllerBase.BossState.Flight);
            AudioManager.Instance?.PlaySFX("vamp_flying", 0.7f);
            yield return queen.FlyToWorldPoint(summonAnchor.position, flyMoveSpeed);
        }

        queen.SetState(BossControllerBase.BossState.Flight);
        if (!string.IsNullOrEmpty(idleAnimationTrigger))
            queen.PlayAnimation(idleAnimationTrigger);

        activeEnemies.Clear();
        
        queen.UpdateFacingTowards(context.player.transform.position);

        int spawned = 0;
        while (spawned < waveSize)
        {
            if (queen.State == BossControllerBase.BossState.Dead)
                yield break;

            CleanupActiveEnemies();

            if (maxConcurrent > 0 && activeEnemies.Count >= maxConcurrent)
            {
                yield return null;
                continue;
            }

            if (TrySpawnEnemy(context))
            {
                spawned++;

                if (!string.IsNullOrEmpty(summonAnimationTrigger))
                    queen.PlayAnimation(summonAnimationTrigger);
            }

            if (summonAnimationHold > 0f)
                yield return new WaitForSeconds(summonAnimationHold);

            if (!string.IsNullOrEmpty(idleAnimationTrigger))
                queen.PlayAnimation(idleAnimationTrigger);

            if (summonInterval > 0f)
                yield return new WaitForSeconds(summonInterval);
            else
                yield return null;
        }

        while (activeEnemies.Count > 0)
        {
            if (queen.State == BossControllerBase.BossState.Dead)
                yield break;

            CleanupActiveEnemies();
            yield return null;
        }
    }

    private void CleanupActiveEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }
    }

    private bool TrySpawnEnemy(BossContext context)
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            return false;

        var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
        if (prefab == null)
            return false;

        Vector3 spawnWorld;
        if (!TryPickSpawnWorld(context, out spawnWorld))
            return false;

        StartCoroutine(SpawnEnemyWithPortal(prefab, spawnWorld));
        return true;
    }

    private IEnumerator SpawnEnemyWithPortal(GameObject prefab, Vector3 spawnWorld)
    {
        if (summonPortalPrefab != null)
        {
            AudioManager.Instance?.PlaySFX("vamp_portal_summon", 0.7f);
            Instantiate(summonPortalPrefab, spawnWorld, Quaternion.identity);
        }

        if (portalSpawnDelay > 0f)
            yield return new WaitForSeconds(portalSpawnDelay);

        var enemy = Instantiate(prefab, spawnWorld, Quaternion.identity);
        if (enemy != null)
            activeEnemies.Add(enemy);
    }

    private bool TryPickSpawnWorld(BossContext context, out Vector3 spawnWorld)
    {
        spawnWorld = Vector3.zero;

        if (enemySpawnPoints != null && enemySpawnPoints.Length > 0)
        {
            var point = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Length)];
            if (point != null)
            {
                spawnWorld = point.position;
                return true;
            }
        }

        if (context?.grid == null)
            return false;

        var candidates = context.grid.GetAllPaintedGroundCells();
        if (candidates == null || candidates.Count == 0)
            return false;

        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[Random.Range(0, candidates.Count)];
            if (!context.grid.CanEnemyEnterCell(cell))
                continue;

            spawnWorld = context.grid.CellToWorldCenter(cell);
            return true;
        }

        return false;
    }
}

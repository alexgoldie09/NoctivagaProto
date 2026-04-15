using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rhythm-driven, grid-aware enemy base.
/// - Subscribes to RhythmManager.OnBeat and acts every N beats.
/// - Uses GridManager/Tile rules to move only onto walkable tiles.
/// - Uses Unity Physics for instant contact with the Player.
/// 
/// Derive and override:
///   - OnBeatAction() : what the enemy does on its active beat (move, attack, idle).
///   - OnPlayerContact(PlayerController player) : custom contact behavior (default = reset).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] protected bool snapToGridOnStart = true;

    [Header("Rhythm")]
    [Tooltip("If true, the enemy acts on rhythm beats. If false, it acts on a fixed time interval.")]
    [SerializeField] protected bool useRhythm = true;
    [Tooltip("Act every N beats (1 = every beat, 2 = every second beat, etc).")]
    [SerializeField] protected int beatsPerAction = 1;
    [Tooltip("Offset in beats before first action (0 = act on first eligible beat).")]
    [SerializeField] protected int beatOffset = 0;
    [Tooltip("Seconds between actions when Use Rhythm is disabled.")]
    [SerializeField] protected float actionInterval = 0.5f;
    [Tooltip("Optional delay (seconds) before the first action when Use Rhythm is disabled.")]
    [SerializeField] protected float actionStartDelay = 0f;

    [Header("Contact")]
    [Tooltip("If true, contacting the player triggers a level reset.")]
    [SerializeField] protected bool lethalOnContact = true;

    [Header("Damage Force")]
    [Tooltip("If true, allow camera shake.")]
    [SerializeField] protected bool allowDamageShake = true;
    [Tooltip("Shake force for camera on damage.")]
    [SerializeField] protected float damageShakeForce = 0.8f;

    [Header("Void Elimination")]
    [Tooltip("How long the enemy takes to shrink/fall before being destroyed.")]
    [SerializeField] protected float voidDeathDuration = 0.25f;
    [Tooltip("How far the enemy sinks down while falling.")]
    [SerializeField] protected float voidDeathDropDistance = 0.25f;
    
    [Header("Death Reward")]
    [Tooltip("If true, this enemy spawns a reward object when it dies.")]
    [SerializeField] protected bool spawnsReward = false;
    [Tooltip("Prefabs spawned as a reward on death when Spawns Reward is enabled.")]
    [SerializeField] protected GameObject[] rewardPrefabs;
    [Tooltip("Maximum search distance in cells for a safe reward spawn location.")]
    [SerializeField] protected int rewardSpawnSearchRadius = 4;

    protected TilemapGridManager grid;
    protected PlayerController player;
    protected Animator animator;

    protected Vector3Int cellPos;

    private int localBeatCounter;
    private bool isDying;
    private bool hasSpawnedReward;
    private Coroutine actionRoutine;
    private Camera mainCam;
    private static readonly float SCREEN_BUFFER = 0.15f;

    // ─────────────────────────────────────────────────────────────
    #region Unity lifecycle
    /// <summary>
    /// Subscribes to the rhythm beat event when the enemy becomes active.
    /// </summary>
    protected virtual void OnEnable()
    {
        if (useRhythm)
            RhythmManager.OnBeat += HandleBeat;
        else
            actionRoutine = StartCoroutine(ActionIntervalLoop());
    }
    
    /// <summary>
    /// Unsubscribes from the rhythm beat event when the enemy is disabled.
    /// </summary>
    protected virtual void OnDisable()
    {
        if (useRhythm)
            RhythmManager.OnBeat -= HandleBeat;

        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }
        
        if (grid != null)
            grid.ClearTelegraphForOwner(GetInstanceID());
    }
    
    /// <summary>
    /// Resolves grid, animator, and player references and snaps to the grid if requested.
    /// </summary>
    protected virtual void Start()
    {
        grid = TilemapGridManager.Instance;
        animator = GetComponent<Animator>();
        player = GameManager.Instance != null ? GameManager.Instance.player : null;

        // Ensure collider works for triggers
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        if (grid != null)
        {
            cellPos = grid.WorldToCell(transform.position);
            if (snapToGridOnStart)
                transform.position = grid.CellToWorldCenter(cellPos);
        }
        
        mainCam = Camera.main;
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Rhythm
    /// <summary>
    /// Handles rhythm beat callbacks and triggers actions on the configured cadence.
    /// </summary>
    private void HandleBeat()
    {
        if (Utilities.IsGameFrozen) 
            return; // skip beat if frozen
        
        if (isDying) 
            return;

        int phase = localBeatCounter + beatOffset;
        
        if (beatsPerAction <= 0) 
            beatsPerAction = 1;

        if (phase % beatsPerAction == 0)
        {
            OnBeatAction();
        }

        localBeatCounter++;
    }

    /// <summary>
    /// Override in subclasses to perform actions on active beats.
    /// </summary>
    protected abstract void OnBeatAction();

    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Interval Loop

    private IEnumerator ActionIntervalLoop()
    {
        // Wait one frame so Start() has run and all references (player, grid) are initialized
        yield return null;

        if (actionStartDelay > 0f)
            yield return new WaitForSeconds(actionStartDelay);

        float interval = Mathf.Max(0.05f, actionInterval);

        while (true)
        {
            if (Utilities.IsGameFrozen || isDying)
            {
                yield return null;
                continue;
            }

            OnBeatAction();
            yield return new WaitForSeconds(interval);
        }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Movement helpers
    /// <summary>
    /// Attempts to move the enemy by a grid direction if the destination is valid.
    /// </summary>
    /// <param name="dir">Grid direction delta.</param>
    /// <returns>True if the move succeeds.</returns>
    protected bool TryMove(Vector3Int dir)
    {
        if (isDying) return false;

        Vector3Int next = cellPos + dir;
        if (!CanEnter(next)) return false;

        cellPos = next;
        transform.position = grid.CellToWorldCenter(cellPos);
        
        grid.HandleEnteredCellEnemy(cellPos, this);
        return true;
    }

    /// <summary>
    /// Checks if the enemy can enter the requested grid cell.
    /// </summary>
    /// <param name="cell">Grid coordinate to test.</param>
    /// <returns>True if the cell is walkable for enemies.</returns>
    protected bool CanEnter(Vector3Int cell)
    {
        if (grid == null) 
            return false;
        
        return grid.CanEnemyEnterCell(cell);
    }
    
    /// <summary>
    /// Instantly moves the enemy to a grid cell without interpolation.
    /// </summary>
    /// <param name="cell">Target grid coordinate.</param>
    public void WarpTo(Vector3Int cell)
    {
        if (isDying) 
            return;

        cellPos = cell;
        if (grid != null)
            transform.position = grid.CellToWorldCenter(cellPos);
    }
    
    /// <summary>
    /// Gets the current grid cell position of the enemy.
    /// </summary>
    public Vector3Int CellPosition => cellPos;
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Void elimination
    /// <summary>
    /// Called when the tile under the enemy becomes Void.
    /// Plays a fall/shrink animation and destroys the enemy.
    /// </summary>
    public void KillByVoidFall(Vector3 fallStartWorld)
    {
        if (isDying) return;
        StartCoroutine(VoidDeathRoutine(fallStartWorld));
    }
    
    /// <summary>
    /// Instantly kills the enemy from a melee strike.
    /// </summary>
    public void KillByMeleeStrike()
    {
        if (isDying) 
            return;

        isDying = true;
        lethalOnContact = false;
        TrySpawnDeathReward();
        Destroy(gameObject);
    }

    /// <summary>
    /// Runs a shrink-and-fall animation before destroying the enemy.
    /// </summary>
    /// <param name="fallStartWorld">World-space start position for the fall.</param>
    private IEnumerator VoidDeathRoutine(Vector3 fallStartWorld)
    {
        isDying = true;

        // stop participating in gameplay
        lethalOnContact = false;

        // snap to the cell where the void happened (visual consistency)
        transform.position = fallStartWorld;

        Vector3 startScale = transform.localScale;
        Vector3 startPos = fallStartWorld;
        Vector3 endPos = startPos + Vector3.down * voidDeathDropDistance;

        float t = 0f;
        while (t < voidDeathDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / voidDeathDuration);
            float eased = a * a; // ease-in

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            transform.position = Vector3.Lerp(startPos, endPos, eased);

            yield return null;
        }

        TrySpawnDeathReward();
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Spawns a reward prefab at the nearest safe, player-reachable cell if configured.
    /// </summary>
    protected void TrySpawnDeathReward()
    {
        if (hasSpawnedReward || !spawnsReward || rewardPrefabs.Length <= 0 || grid == null)
            return;
        
        var randomRewardPrefab = rewardPrefabs[Random.Range(0, rewardPrefabs.Length)];
        Vector3Int spawnCell = FindNearestSafeRewardCell();
        Vector3 spawnWorld = grid.CellToWorldCenter(spawnCell);
        Instantiate(randomRewardPrefab, spawnWorld, Quaternion.identity);
        hasSpawnedReward = true;
    }

    /// <summary>
    /// Finds a safe spawn cell close to the enemy's current position.
    /// Prioritizes direct neighbors first, then expands outward.
    /// </summary>
    private Vector3Int FindNearestSafeRewardCell()
    {
        Vector3Int origin = grid.WorldToCell(transform.position);
        if (grid.CanEnterCell(origin))
            return origin;

        Vector3Int[] cardinalDirections =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0)
        };

        foreach (var dir in cardinalDirections)
        {
            Vector3Int neighbor = origin + dir;
            if (grid.CanEnterCell(neighbor))
                return neighbor;
        }

        Vector3Int[] diagonalDirections =
        {
            new(1, 1, 0), new(-1, 1, 0),
            new(1, -1, 0), new(-1, -1, 0)
        };

        foreach (var dir in diagonalDirections)
        {
            Vector3Int neighbor = origin + dir;
            if (grid.CanEnterCell(neighbor))
                return neighbor;
        }

        int maxRadius = Mathf.Max(1, rewardSpawnSearchRadius);
        var visited = new HashSet<Vector3Int> { origin };
        var queue = new Queue<(Vector3Int cell, int dist)>();
        queue.Enqueue((origin, 0));

        Vector3Int[] allDirections =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0),
            new(1, 1, 0), new(-1, 1, 0), new(1, -1, 0), new(-1, -1, 0)
        };

        while (queue.Count > 0)
        {
            var (cell, dist) = queue.Dequeue();
            if (dist >= maxRadius)
                continue;

            foreach (var dir in allDirections)
            {
                Vector3Int next = cell + dir;
                if (!visited.Add(next))
                    continue;

                if (grid.CanEnterCell(next))
                    return next;

                queue.Enqueue((next, dist + 1));
            }
        }

        return origin;
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Contact handling (Physics)
    /// <summary>
    /// Detects trigger contact with the player and applies contact behavior.
    /// </summary>
    /// <param name="other">Collider that entered the trigger.</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!lethalOnContact) return;

        if (other.CompareTag("Player"))
        {
            OnPlayerContact();
        }
    }

    /// <summary>
    /// Default contact behavior: reset the level.
    /// Override to customize (knockback, SFX, etc).
    /// </summary>
    protected void OnPlayerContact()
    {
        if (player.IsShadowMode || player.IsDead)
            return;
        
        if (GameManager.Instance != null)
            GameManager.Instance.PlayerKilled();
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region VFX Culling
    /// <summary>
    /// Returns true if this enemy is within the visible screen area (plus a small buffer).
    /// Use to skip feedback-only effects like VFX, SFX, and camera shake.
    /// Never use to skip gameplay logic like contact checks or movement.
    /// </summary>
    protected bool IsOnScreen()
    {
        if (mainCam == null) return true; // fail open — don't skip if camera missing
        Vector3 vp = mainCam.WorldToViewportPoint(transform.position);
        return vp.x > -SCREEN_BUFFER && vp.x < 1f + SCREEN_BUFFER &&
               vp.y > -SCREEN_BUFFER && vp.y < 1f + SCREEN_BUFFER;
    }
    #endregion
}

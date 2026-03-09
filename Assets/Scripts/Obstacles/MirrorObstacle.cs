using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mirror obstacle that casts diagonal beam lines and blocks tiles along the beam path.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MirrorObstacle : ObstacleBase
{
    /// <summary>
    /// The type of mirror this gameobject is.
    /// </summary>
    public enum MirrorType
    {
        Reflector,
        Emitter,
        PuzzleRewarder
    }
    /// <summary>
    /// Cardinal diagonal directions for beam emission.
    /// </summary>
    public enum MirrorDirection
    {
        UpRight,   // ↗
        UpLeft,    // ↖
        DownRight, // ↘
        DownLeft   // ↙
    }
    
    /// <summary>
    /// Action to perform for puzzle rewarders
    /// </summary>
    public enum PuzzleRewardAction
    {
        SpawnPrefab,
        ActivateObject
    }

    [Header("Mirror Settings")]
    [SerializeField] private MirrorDirection direction = MirrorDirection.UpRight;
    
    [Tooltip("Emitter starts beam chains, Reflector bounces beams, PuzzleRewarder ends the chain and can spawn a reward.")]
    [SerializeField] private MirrorType mirrorType = MirrorType.Reflector;
    
    [Tooltip("If false, this mirror will not emit even if it is hit.")]
    [SerializeField] private bool beamActive = true;

    [Tooltip("Safety cap: max tiles the beam can travel before stopping.")]
    [SerializeField] private int maxSteps = 100;
    
    [Tooltip("If true, enemies touching the beam are killed instead of pushed away.")]
    [SerializeField] private bool killEnemiesOnBeam = false;

    [Header("Puzzle Reward")]
    [Tooltip("Choose whether this PuzzleRewarder spawns a prefab or activates an existing object.")]
    [SerializeField] private PuzzleRewardAction puzzleRewardAction = PuzzleRewardAction.SpawnPrefab;
    [Tooltip("Prefab to spawn when action is SpawnPrefab and this mirror receives the correct beam direction.")]
    [SerializeField] private GameObject puzzleRewardPrefab;
    [Tooltip("Optional spawn marker. If null, this mirror's position is used.")]
    [SerializeField] private Transform puzzleRewardSpawnPoint;
    [Tooltip("Existing object to enable when action is ActivateObject.")]
    [SerializeField] private GameObject puzzleRewardTargetObject;
    [Tooltip("If true, this puzzle reward can only trigger once.")]
    [SerializeField] private bool spawnRewardOnlyOnce = true;
    
    [Header("Mirror Visuals")]
    [SerializeField] private Sprite upRightSprite;
    [SerializeField] private Sprite downRightSprite;
    [SerializeField] private Sprite downLeftSprite;
    [SerializeField] private Sprite upLeftSprite;
    
    [Header("Beam Visuals")]
    [SerializeField] private float beamWidth = 0.1f;

    private LineRenderer line;
    private bool rewardSpawned;

    private int OwnerId => GetInstanceID();

    /// <summary>
    /// Initializes the line renderer used to draw beam visuals.
    /// </summary>
    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 0;
        line.startWidth = beamWidth;
        line.endWidth = beamWidth;
        line.useWorldSpace = true;
        
        sr = GetComponent<SpriteRenderer>();
        
        // Changes visuals
        if (sr != null)
            ChangeSprite();
    }

    // /// <summary>
    // /// Waits one frame to ensure obstacles register before rebuilding beams.
    // /// </summary>
    // private IEnumerator Start()
    // {
    //     // Ensures all mirrors have Awake() called and have registered their cells
    //     yield return null;
    //     RebuildAllBeams();
    // }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        RebuildAllBeams();
    }


    /// <summary>
    /// Clears this mirror's beam, unregisters it, and rebuilds beam chains.
    /// </summary>
    protected override void OnDisable()
    {
        // 1) Clear this mirror's beam cells first (or after, either is fine)
        if (grid != null)
            grid.ClearBeamCellsForOwner(GetInstanceID());

        // 2) Clear visuals
        ClearLine();

        // 3) Unregister obstacle cell mapping
        base.OnDisable();

        // 4) Rebuild beams because chains may have changed
        RebuildAllBeams();
    }

    /// <summary>
    /// Rotates the mirror and rebuilds all beams.
    /// </summary>
    public override void Interact()
    {
        base.Interact();
        
        RotateClockwise();
        RebuildAllBeams();
    }

    /// <summary>
    /// Rotates the mirror direction clockwise.
    /// </summary>
    private void RotateClockwise()
    {
        // Cycle directions clockwise: UR -> DR -> DL -> UL -> UR
        switch (direction)
        {
            case MirrorDirection.UpRight: direction = MirrorDirection.DownRight; break;
            case MirrorDirection.DownRight: direction = MirrorDirection.DownLeft; break;
            case MirrorDirection.DownLeft: direction = MirrorDirection.UpLeft; break;
            case MirrorDirection.UpLeft: direction = MirrorDirection.UpRight; break;
        }
        
        // Changes visuals
        if (sr != null)
            ChangeSprite();
    }
    
    /// <summary>
    /// Swap sprite visuals based on Mirror Direction
    /// </summary>
    private void ChangeSprite()
    {
        if (sr == null) 
            return;

        sr.sprite = direction switch
        {
            MirrorDirection.UpRight   => upRightSprite,
            MirrorDirection.DownRight => downRightSprite,
            MirrorDirection.DownLeft  => downLeftSprite,
            MirrorDirection.UpLeft    => upLeftSprite,
            _ => upRightSprite
        };

        if (sr.sprite == null)
            Debug.LogWarning($"[MirrorObstacle] Missing sprite for {direction} on {name}", this);
    }

    /// <summary>
    /// Rebuilds beam chains for all mirrors, starting from emitters.
    /// </summary>
    public static void RebuildAllBeams()
    {
        var g = TilemapGridManager.Instance;
        if (g == null) return;

        var mirrors = FindObjectsByType<MirrorObstacle>(FindObjectsSortMode.None);

        // Clear everything first (prevents stale beams when chains change)
        foreach (var m in mirrors)
        {
            g.ClearBeamCellsForOwner(m.OwnerId);
            m.ClearLine();
        }

        // Cast from emitters, and let hits energize the next mirrors.
        // Use a global visited set so each mirror emits at most once per rebuild.
        var visited = new HashSet<int>();

        foreach (var m in mirrors)
        {
            if (m.mirrorType != MirrorType.Emitter) continue;
            m.CastIfEnergized(visited);
        }

        bool shouldKillEnemies = false;
        foreach (var mirror in mirrors)
        {
            if (mirror != null && mirror.killEnemiesOnBeam)
            {
                shouldKillEnemies = true;
                break;
            }
        }

        // After beams update, resolve occupants if trapped on a beam cell.
        ResolveOccupantsIfOnBeam(g, shouldKillEnemies);
    }
    
    /// <summary>
    /// Emits a beam from this mirror if energized, chaining to mirrors hit.
    /// </summary>
    /// <param name="visited">Set of mirror instance IDs already processed.</param>
    private void CastIfEnergized(HashSet<int> visited)
    {
        if (!visited.Add(OwnerId)) 
            return;

        if (!beamActive)
            return;
        
        if (grid == null) 
            return;

        Vector3Int originCell = grid.WorldToCell(transform.position);
        Vector3 originWorld = grid.CellToWorldCenter(originCell);
        Vector3Int step = GetStep(direction);

        // Gather beam-blocked cells for THIS mirror only
        var blockedCells = new List<Vector3Int>();

        Vector3Int current = originCell;
        Vector3Int lastValid = originCell;
        Vector3 endWorld = originWorld;

        for (int i = 0; i < maxSteps; i++)
        {
            current += step;

            if (!grid.IsInBounds(current))
            {
                // End at the OUTER edge of the last valid cell
                endWorld = grid.GetCellOuterEdgeWorld(lastValid, step);
                break;
            }
            
            lastValid = current;

            // Stop at first blocking tile (wall/gate)
            if (grid.IsBlockingTile(current))
            {
                endWorld = grid.GetCellEdgeWorld(current, step);
                break;
            }

            // Stop if we hit another mirror obstacle
            if (grid.TryGetObstacle(current, out var obs) && obs is MirrorObstacle hitMirror && hitMirror != this)
            {
                endWorld = grid.CellToWorldCenter(current);

                if (hitMirror.mirrorType == MirrorType.PuzzleRewarder)
                {
                    hitMirror.TryResolvePuzzleReward(direction);
                }
                else
                {
                    // Energize the hit mirror: it emits its own beam in its own direction.
                    hitMirror.CastIfEnergized(visited);
                }
                
                break;
            }

            // Otherwise: this cell is blocked by the beam
            blockedCells.Add(current);
            endWorld = grid.CellToWorldCenter(current);
        }

        // Apply beam blocking for this mirror
        grid.SetBeamCellsForOwner(OwnerId, blockedCells);

        // Draw segment
        DrawLine(originWorld, endWorld);
    }

    /// <summary>
    /// Moves the player or enemies off beam cells if possible, otherwise resolves failures.
    /// </summary>
    /// <param name="g">Grid manager used for movement checks.</param>
    private static void ResolveOccupantsIfOnBeam(TilemapGridManager g, bool killEnemies)
    {
        // 1) Resolve player
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            ResolveSingleOccupant(
                g,
                player.CellPosition,
                canEnter: g.CanEnterCell,
                onMove: player.TeleportToCell,
                onFail: () =>
                {
                    Vector3 fallStartWorld = g.CellToWorldCenter(player.CellPosition);
                    player.StartVoidFallReset(g.GetStartCell(), fallStartWorld);
                }
            );
        }

        // 2) Resolve enemies
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e == null) continue;
            
            if (killEnemies && g.IsBeamBlocked(e.CellPosition))
            {
                Vector3 fallStartWorld = g.CellToWorldCenter(e.CellPosition);
                e.KillByVoidFall(fallStartWorld);
                continue;
            }

            ResolveSingleOccupant(
                g,
                e.CellPosition,
                canEnter: g.CanEnemyEnterCell,
                onMove: e.WarpTo,
                onFail: () =>
                {
                    // If an enemy is completely trapped, you can decide what to do.
                    // For now: do nothing (they'll remain in place), or optionally kill them.
                    Destroy(e.gameObject);
                }
            );
        }
    }

    /// <summary>
    /// Attempts to move a single occupant off a beam, falling back to onFail if blocked.
    /// </summary>
    /// <param name="g">Grid manager for beam checks.</param>
    /// <param name="currentCell">Current occupant cell.</param>
    /// <param name="canEnter">Function that checks if a cell is enterable.</param>
    /// <param name="onMove">Action to invoke when a valid cell is found.</param>
    /// <param name="onFail">Action to invoke when no escape cell is found.</param>
    private static void ResolveSingleOccupant(
        TilemapGridManager g,
        Vector3Int currentCell,
        System.Func<Vector3Int, bool> canEnter,
        System.Action<Vector3Int> onMove,
        System.Action onFail)
    {
        if (!g.IsBeamBlocked(currentCell))
            return;

        // Try neighbors (4-dir first feels like a "push")
        Vector3Int[] dirs4 =
        {
            new(1,0,0), new(-1,0,0),
            new(0,1,0), new(0,-1,0)
        };

        foreach (var d in dirs4)
        {
            var nc = currentCell + d;
            if (canEnter(nc))
            {
                onMove(nc);
                return;
            }
        }

        // Then try diagonals as backup
        Vector3Int[] dirsDiag =
        {
            new(1,1,0), new(-1,1,0),
            new(1,-1,0), new(-1,-1,0)
        };

        foreach (var d in dirsDiag)
        {
            var nc = currentCell + d;
            if (canEnter(nc))
            {
                onMove(nc);
                return;
            }
        }

        // Optional: small BFS search radius so we can "unstick" in tighter spaces
        const int maxRadius = 4;
        var visited = new HashSet<Vector3Int> { currentCell };
        var queue = new Queue<(Vector3Int cell, int dist)>();
        queue.Enqueue((currentCell, 0));

        Vector3Int[] dirs8 =
        {
            new(1,0,0), new(-1,0,0), new(0,1,0), new(0,-1,0),
            new(1,1,0), new(-1,1,0), new(1,-1,0), new(-1,-1,0)
        };

        while (queue.Count > 0)
        {
            var (c, dist) = queue.Dequeue();
            if (dist >= maxRadius) continue;

            foreach (var d in dirs8)
            {
                var nc = c + d;
                if (!visited.Add(nc)) continue;

                if (canEnter(nc))
                {
                    onMove(nc);
                    return;
                }

                queue.Enqueue((nc, dist + 1));
            }
        }

        onFail?.Invoke();
    }

    /// <summary>
    /// Converts a mirror direction into a diagonal step vector.
    /// </summary>
    /// <param name="dir">Mirror direction enum.</param>
    /// <returns>Diagonal step vector.</returns>
    private Vector3Int GetStep(MirrorDirection dir)
    {
        return dir switch
        {
            MirrorDirection.UpRight => new Vector3Int(1, 1, 0),
            MirrorDirection.UpLeft => new Vector3Int(-1, 1, 0),
            MirrorDirection.DownRight => new Vector3Int(1, -1, 0),
            MirrorDirection.DownLeft => new Vector3Int(-1, -1, 0),
            _ => new Vector3Int(1, 1, 0)
        };
    }
    
    /// <summary>
    /// Handles puzzle-reward logic when this mirror is configured as a PuzzleRewarder.
    /// </summary>
    private void TryResolvePuzzleReward(MirrorDirection incomingDirection)
    {
        if (mirrorType != MirrorType.PuzzleRewarder || !beamActive)
            return;

        if (direction != GetOppositeDirection(incomingDirection))
            return;

        if (spawnRewardOnlyOnce && rewardSpawned)
            return;

        bool rewardTriggered = false;

        switch (puzzleRewardAction)
        {
            case PuzzleRewardAction.SpawnPrefab:
                if (puzzleRewardPrefab == null)
                {
                    Debug.Log($"[MirrorObstacle] PuzzleRewarder on {name} is missing reward prefab.", this);
                    return;
                }

                Vector3 spawnWorld = puzzleRewardSpawnPoint != null ? puzzleRewardSpawnPoint.position : transform.position;
                Instantiate(puzzleRewardPrefab, spawnWorld, Quaternion.identity);
                rewardTriggered = true;
                break;

            case PuzzleRewardAction.ActivateObject:
                if (puzzleRewardTargetObject == null)
                {
                    Debug.Log($"[MirrorObstacle] PuzzleRewarder on {name} is missing target object to activate.", this);
                    return;
                }

                puzzleRewardTargetObject.SetActive(true);
                rewardTriggered = true;
                break;
        }

        if (rewardTriggered)
            rewardSpawned = true;
    }

    /// <summary>
    /// Returns the opposite diagonal direction.
    /// </summary>
    private MirrorDirection GetOppositeDirection(MirrorDirection dir)
    {
        return dir switch
        {
            MirrorDirection.UpRight => MirrorDirection.DownLeft,
            MirrorDirection.UpLeft => MirrorDirection.DownRight,
            MirrorDirection.DownRight => MirrorDirection.UpLeft,
            MirrorDirection.DownLeft => MirrorDirection.UpRight,
            _ => MirrorDirection.DownLeft
        };
    }

    /// <summary>
    /// Draws the beam line between two world positions.
    /// </summary>
    /// <param name="start">World-space start position.</param>
    /// <param name="end">World-space end position.</param>
    private void DrawLine(Vector3 start, Vector3 end)
    {
        if (line == null) 
            line = GetComponent<LineRenderer>();
        
        if (line == null) 
            return;
        
        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    /// <summary>
    /// Clears and disables the beam line renderer.
    /// </summary>
    private void ClearLine()
    {
        if (line == null) 
            return;
        
        line.positionCount = 0;
        line.enabled = false;
    }

    /// <summary>
    /// Mirrors block movement to occupy their tile.
    /// </summary>
    public override bool BlocksMovement() => true;

    /// <summary>
    /// Mirrors block shape placement on their tile.
    /// </summary>
    public override bool BlocksShapePlacement() => true;
}

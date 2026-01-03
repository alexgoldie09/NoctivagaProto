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
    /// Cardinal diagonal directions for beam emission.
    /// </summary>
    public enum MirrorDirection
    {
        UpRight,   // ↗
        UpLeft,    // ↖
        DownRight, // ↘
        DownLeft   // ↙
    }

    [Header("Mirror Settings")]
    [SerializeField] private MirrorDirection direction = MirrorDirection.UpRight;

    [Tooltip("If true, this mirror starts the beam chain on rebuild.")]
    [SerializeField] private bool isEmitter = false;

    [Tooltip("If false, this mirror will not emit even if it is hit.")]
    [SerializeField] private bool beamActive = true;

    [Tooltip("Safety cap: max tiles the beam can travel before stopping.")]
    [SerializeField] private int maxSteps = 100;

    [Header("Beam Visuals")]
    [SerializeField] private float beamWidth = 0.1f;

    private LineRenderer line;
    private TilemapGridManager grid;

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
    }

    /// <summary>
    /// Waits one frame to ensure obstacles register before rebuilding beams.
    /// </summary>
    private IEnumerator Start()
    {
        // Ensures all mirrors have Awake() called and have registered their cells
        yield return null;
        RebuildAllBeams();
    }

    /// <summary>
    /// Clears this mirror's beam, unregisters it, and rebuilds beam chains.
    /// </summary>
    protected override void OnDisable()
    {
        // 1) Clear this mirror's beam cells first (or after, either is fine)
        var grid = TilemapGridManager.Instance;
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
            if (!m.isEmitter) continue;
            m.CastIfEnergized(visited);
        }

        // After beams update, resolve occupants if trapped on a beam cell.
        ResolveOccupantsIfOnBeam(g);
    }

    /// <summary>
    /// Emits a beam from this mirror if energized, chaining to mirrors hit.
    /// </summary>
    /// <param name="visited">Set of mirror instance IDs already processed.</param>
    private void CastIfEnergized(HashSet<int> visited)
    {
        if (visited.Contains(OwnerId)) 
            return;
        
        visited.Add(OwnerId);

        if (!beamActive)
            return;

        grid = TilemapGridManager.Instance;
        
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

                // Energize the hit mirror: it emits its own beam in its own direction.
                hitMirror.CastIfEnergized(visited);
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
    private static void ResolveOccupantsIfOnBeam(TilemapGridManager g)
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class MirrorObstacle : ObstacleBase
{
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

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 0;
        line.startWidth = beamWidth;
        line.endWidth = beamWidth;
        line.useWorldSpace = true;
    }

    private IEnumerator Start()
    {
        // Ensures all mirrors have Awake() called and have registered their cells
        yield return null;
        RebuildAllBeams();
    }

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

    public override void Interact()
    {
        RotateClockwise();
        RebuildAllBeams();
    }

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

    // -------------------------------------------
    // Beam Rebuild (emitters energize chains)
    // -------------------------------------------
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

        // After beams update, resolve player if trapped on a beam cell.
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            ResolvePlayerIfOnBeam(player, g);
    }

    private void CastIfEnergized(HashSet<int> visited)
    {
        if (visited.Contains(OwnerId)) return;
        visited.Add(OwnerId);

        if (!beamActive)
            return;

        grid = TilemapGridManager.Instance;
        if (grid == null) return;

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

    private static void ResolvePlayerIfOnBeam(PlayerController player, TilemapGridManager g)
    {
        Vector3Int pc = player.CellPosition;
        if (!g.IsBeamBlocked(pc)) return;

        // Try 4-direction nudge first
        Vector3Int[] dirs =
        {
            new (1,0,0),
            new (-1,0,0),
            new (0,1,0),
            new (0,-1,0),
        };

        foreach (var d in dirs)
        {
            var nc = pc + d;
            if (g.CanEnterCell(nc))
            {
                player.TeleportToCell(nc);
                return;
            }
        }

        // If stuck, fall-reset to start (your existing logic)
        Vector3 fallStartWorld = g.CellToWorldCenter(pc);
        player.StartVoidFallReset(g.GetStartCell(), fallStartWorld);
    }

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

    private void ClearLine()
    {
        if (line == null) return;
        line.positionCount = 0;
        line.enabled = false;
    }

    public override bool BlocksMovement() => true;
    public override bool BlocksShapePlacement() => true;
}

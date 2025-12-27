using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tilemap-backed grid query layer.
/// Reads GameTile metadata from tilemaps and exposes cell-based helpers for movement.
/// </summary>
public class TilemapGridManager : MonoBehaviour
{
    public static TilemapGridManager Instance { get; private set; }

    [Header("Tilemaps")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap; // Floor/Void/Start (painted with GameTile)
    [SerializeField] private Tilemap blocksTilemap; // Walls/Gates (painted with GameTile or any tiles)
    
    [Header("Default Ground Tiles (GameTile assets)")]
    [SerializeField] private GameTile floorTile;
    [SerializeField] private GameTile voidTile;

    [Header("Rules")]
    [Tooltip("If true, the player can step onto Void tiles. (You can still reset on enter.)")]
    [SerializeField] private bool allowWalkingIntoVoid = false;

    [Tooltip("If true and the entered tile has EnterEffect.ResetToStart, teleport to start.")]
    [SerializeField] private bool voidResetsToStart = true;
    
    // Obstacles occupying cells (1 obstacle per cell for now)
    private readonly Dictionary<Vector3Int, ObstacleBase> obstacleByCell = new();
    private readonly HashSet<Vector3Int> beamBlocked = new();
    private readonly Dictionary<int, List<Vector3Int>> beamByOwner = new();

    private BoundsInt groundBounds;
    private Vector3Int cachedStartCell;
    private bool hasCachedStart;

    #region Unity Events
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (grid == null)
            grid = GetComponentInParent<Grid>();
        
        RefreshBoundsAndCache();
    }
    #endregion

    #region Grid Methods
    /// <summary>Call if you change the level tilemaps at runtime and want bounds/start recalculated.</summary>
    public void RefreshBoundsAndCache()
    {
        if (groundTilemap == null)
        {
            Debug.LogError("[TilemapGridManager] Ground Tilemap not assigned.");
            return;
        }

        groundTilemap.CompressBounds();
        groundBounds = groundTilemap.cellBounds;

        cachedStartCell = FindStartCellFallbackToBoundsCenter();
        hasCachedStart = true;
    }

    // -----------------------------
    // World/Cell conversion
    // -----------------------------
    public Vector3Int WorldToCell(Vector3 worldPos) => grid.WorldToCell(worldPos);
    public Vector3 CellToWorldCenter(Vector3Int cell) => grid.GetCellCenterWorld(cell);
    
    public Vector3 GetCellEdgeWorld(Vector3Int hitCell, Vector3Int beamStep)
    {
        Vector3 center = CellToWorldCenter(hitCell);
        Vector2 half = GetCellHalfExtents();
        Vector2Int s = StepSign(beamStep);

        // Entry edge (where the beam enters the hit cell)
        return center - new Vector3(s.x * half.x, s.y * half.y, 0f);
    }
    
    private Vector2 GetCellHalfExtents()
    {
        // Prefer the Grid cell size if available; fallback to 0.5
        var g = groundTilemap != null ? groundTilemap.layoutGrid : null;
        if (g != null)
            return new Vector2(g.cellSize.x * 0.5f, g.cellSize.y * 0.5f);

        return new Vector2(0.5f, 0.5f);
    }

    private Vector2Int StepSign(Vector3Int step)
    {
        int sx = step.x == 0 ? 0 : (step.x > 0 ? 1 : -1);
        int sy = step.y == 0 ? 0 : (step.y > 0 ? 1 : -1);
        return new Vector2Int(sx, sy);
    }

    /// <summary>
    /// Point on the edge of a cell on the OUTER side in the beam direction.
    /// Use when the beam goes out-of-bounds, to end at the last valid cell boundary.
    /// </summary>
    public Vector3 GetCellOuterEdgeWorld(Vector3Int lastValidCell, Vector3Int beamStep)
    {
        Vector3 center = CellToWorldCenter(lastValidCell);
        Vector2 half = GetCellHalfExtents();
        Vector2Int s = StepSign(beamStep);

        return center + new Vector3(s.x * half.x, s.y * half.y, 0f);
    }

    // -----------------------------
    // Bounds
    // -----------------------------
    public bool IsInBounds(Vector3Int cell) => groundBounds.Contains(cell);

    public BoundsInt GroundBounds => groundBounds;

    // -----------------------------
    // Tile lookup (GameTile metadata)
    // -----------------------------
    public GameTile GetGroundGameTile(Vector3Int cell)
    {
        if (groundTilemap == null) 
            return null;
        
        return groundTilemap.GetTile(cell) as GameTile;
    }

    public GameTile GetBlockGameTile(Vector3Int cell)
    {
        if (blocksTilemap == null) 
            return null;
        
        return blocksTilemap.GetTile(cell) as GameTile;
    }

    public TileKind GetTileKind(Vector3Int cell)
    {
        // If something exists on the blocks layer, treat it as blocking first.
        if (blocksTilemap != null && blocksTilemap.HasTile(cell))
        {
            var b = GetBlockGameTile(cell);
            return b != null ? b.kind : TileKind.Wall; // non-GameTile tiles are assumed blocking
        }

        var g = GetGroundGameTile(cell);
        return g != null ? g.kind : TileKind.Void; // if unpainted, consider it void-ish
    }
    #endregion

    #region Walking Methods
    // -----------------------------
    // Walkability
    // -----------------------------
    public bool CanEnterCell(Vector3Int cell)
    {
        if (!IsInBounds(cell))
            return false;

        // Any tile on blocks tilemap blocks movement (walls/gates).
        if (blocksTilemap != null && blocksTilemap.HasTile(cell))
            return false;
        
        // blocks layer walls/gates
        if (IsBlockingTile(cell))
            return false;

        // beam cells
        if (IsBeamBlocked(cell))
            return false;

        // obstacle occupancy
        if (TryGetObstacle(cell, out var obs) && obs != null && obs.BlocksMovement())
            return false;

        var ground = GetGroundGameTile(cell);
        if (ground == null)
            return false; // keeps movement restricted to painted ground footprint

        // Use metadata + rule override for Void.
        if (ground.kind == TileKind.Void)
            return allowWalkingIntoVoid && ground.walkableByDefault;

        return ground.walkableByDefault;
    }

    /// <summary>
    /// Called after movement succeeds so tiles can apply enter effects (e.g. reset).
    /// </summary>
    public void HandleEnteredCell(Vector3Int cell, PlayerController player, Vector3 fallStartWorld)
    {
        if (!voidResetsToStart) return;

        var ground = GetGroundGameTile(cell);
        if (ground == null) return;

        if (ground.enterEffect == EnterEffect.ResetToStart)
        {
            player.StartVoidFallReset(GetStartCell(), fallStartWorld);
        }

    }
    #endregion
    
    #region Start Cell Methods
    // -----------------------------
    // Start cell
    // -----------------------------
    public Vector3Int GetStartCell()
    {
        // If bounds haven’t been computed yet for any reason, compute them now.
        if (!hasCachedStart || groundBounds.size.x == 0 || groundBounds.size.y == 0)
            RefreshBoundsAndCache();

        return cachedStartCell;
    }

    private Vector3Int FindStartCellFallbackToBoundsCenter()
    {
        // Scan within ground bounds for a GameTile marked Start.
        foreach (var pos in groundBounds.allPositionsWithin)
        {
            var t = GetGroundGameTile(pos);
            if (t != null && t.kind == TileKind.Start)
                return pos;
        }

        // Fallback: center of the painted bounds.
        return new Vector3Int(
            groundBounds.xMin + groundBounds.size.x / 2,
            groundBounds.yMin + groundBounds.size.y / 2,
            0
        );
    }
    #endregion
    
    #region Obstacle Methods
    public void RegisterObstacle(ObstacleBase obstacle, Vector3Int cell)
    {
        if (obstacle == null) return;
        obstacleByCell[cell] = obstacle;
    }

    public void UnregisterObstacle(ObstacleBase obstacle)
    {
        if (obstacle == null) return;

        // Remove all entries pointing to this obstacle (safe, small counts)
        var toRemove = new List<Vector3Int>();
        foreach (var kvp in obstacleByCell)
            if (kvp.Value == obstacle)
                toRemove.Add(kvp.Key);

        foreach (var c in toRemove)
            obstacleByCell.Remove(c);
    }

    public bool TryGetObstacle(Vector3Int cell, out ObstacleBase obstacle)
        => obstacleByCell.TryGetValue(cell, out obstacle);
    
    public void ToggleFloorVoidAt(Vector3Int cell)
    {
        if (groundTilemap == null) 
            return;
        
        // Don’t toggle tiles that are occupied by obstacles
        if (TryGetObstacle(cell, out var obs) && obs != null)
            return;

        var current = GetGroundGameTile(cell);
        if (current == null) 
            return;

        // Don't toggle Start (optional safety)
        if (current.kind == TileKind.Start) 
            return;

        if (current.kind == TileKind.Floor)
        {
            if (voidTile != null) 
                groundTilemap.SetTile(cell, voidTile);
        }
        else if (current.kind == TileKind.Void)
        {
            if (floorTile != null) 
                groundTilemap.SetTile(cell, floorTile);
        }
    }
    
    public bool IsBlockingTile(Vector3Int cell) => blocksTilemap != null && blocksTilemap.HasTile(cell);
    
    public bool IsBeamBlocked(Vector3Int cell) => beamBlocked.Contains(cell);

    public void SetBeamCellsForOwner(int ownerId, List<Vector3Int> newCells)
    {
        if (beamByOwner.TryGetValue(ownerId, out var old))
        {
            foreach (var c in old) beamBlocked.Remove(c);
        }

        beamByOwner[ownerId] = newCells;

        foreach (var c in newCells) beamBlocked.Add(c);
    }

    public void ClearBeamCellsForOwner(int ownerId)
    {
        if (!beamByOwner.TryGetValue(ownerId, out var old)) return;

        foreach (var c in old) beamBlocked.Remove(c);
        beamByOwner.Remove(ownerId);
    }
    #endregion
}

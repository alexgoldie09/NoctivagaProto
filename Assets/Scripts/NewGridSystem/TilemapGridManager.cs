using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tilemap-backed grid query layer.
/// Reads GameTile metadata from tilemaps and exposes cell-based helpers for movement,
/// preview rendering (TM_Preview), hazards (beams), and shape placement.
/// </summary>
public class TilemapGridManager : MonoBehaviour
{
    public static TilemapGridManager Instance { get; private set; }

    [Header("Tilemaps")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;   // Floor/Void/Start (painted with GameTile)
    [SerializeField] private Tilemap blocksTilemap;   // Walls/Gates (any tiles)
    [SerializeField] private Tilemap previewTilemap;  // TM_Preview
    [SerializeField] private Tilemap overlayTilemap; // TM_Overlay

    [Header("Default Ground Tiles (GameTile assets)")]
    [SerializeField] private GameTile floorTile;
    [SerializeField] private GameTile voidTile;

    [Header("Preview")]
    [SerializeField] private TileBase previewFillTile; // a simple 1x1 tile used for preview tinting

    [Header("Rules")]
    [Tooltip("If true, the player can step onto Void tiles. (You can still reset on enter.)")]
    [SerializeField] private bool allowWalkingIntoVoid = false;

    [Tooltip("If true, stepping onto a Reset tile (void) triggers fall + return to start.")]
    [SerializeField] private bool voidResetsToStart = true;

    // Obstacles + Hazards
    private readonly Dictionary<Vector3Int, ObstacleBase> obstacleByCell = new();
    private readonly HashSet<Vector3Int> beamBlocked = new();
    private readonly Dictionary<int, List<Vector3Int>> beamByOwner = new();

    // Preview ownership (telegraphs + shape preview)
    private readonly Dictionary<int, List<Vector3Int>> previewByOwner = new();
    private readonly Dictionary<int, int> previewTokenByOwner = new();
    
    // Overlay halftime
    private const int OVERLAY_OWNER_HALFTIME = 9001;
    private readonly Dictionary<int, List<Vector3Int>> overlayByOwner = new();
    private Coroutine halfTimeRoutine;

    private BoundsInt groundBounds;
    private Vector3Int cachedStartCell;
    private bool hasCachedStart;

    // ─────────────────────────────────────────────────────────────
    #region Unity Events
    /// <summary>
    /// Initializes the singleton instance and caches ground bounds/start cell.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (grid == null || groundTilemap == null)
        {
            Debug.LogError("[TilemapGridManager] Missing Grid or Ground Tilemap reference.");
            return;
        }

        groundTilemap.CompressBounds();
        groundBounds = groundTilemap.cellBounds;

        cachedStartCell = FindStartCellFallbackToBoundsCenter();
        hasCachedStart = true;
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region World/Cell conversion
    /// <summary>
    /// Converts a world position to a grid cell coordinate.
    /// </summary>
    /// <param name="worldPos">World position to convert.</param>
    public Vector3Int WorldToCell(Vector3 worldPos) => grid.WorldToCell(worldPos);

    /// <summary>
    /// Converts a cell coordinate to the world-space center of the ground tile.
    /// </summary>
    /// <param name="cell">Grid cell coordinate.</param>
    public Vector3 CellToWorldCenter(Vector3Int cell) => groundTilemap.GetCellCenterWorld(cell);
    
    /// <summary>
    /// Returns the entry edge world position for a beam entering a cell.
    /// </summary>
    /// <param name="hitCell">Cell hit by the beam.</param>
    /// <param name="beamStep">Beam step direction.</param>
    public Vector3 GetCellEdgeWorld(Vector3Int hitCell, Vector3Int beamStep)
    {
        Vector3 center = CellToWorldCenter(hitCell);
        Vector2 half = GetCellHalfExtents();
        Vector2Int s = StepSign(beamStep);

        // Entry edge (where the beam enters the hit cell)
        return center - new Vector3(s.x * half.x, s.y * half.y, 0f);
    }
    
    /// <summary>
    /// Returns half extents of a cell in world units.
    /// </summary>
    private Vector2 GetCellHalfExtents()
    {
        // Prefer the Grid cell size if available; fallback to 0.5
        var g = groundTilemap != null ? groundTilemap.layoutGrid : null;
        if (g != null)
            return new Vector2(g.cellSize.x * 0.5f, g.cellSize.y * 0.5f);

        return new Vector2(0.5f, 0.5f);
    }
    
    /// <summary>
    /// Converts a step vector into its sign components (-1, 0, or 1).
    /// </summary>
    /// <param name="step">Step vector to reduce.</param>
    /// <returns>Signed x/y components.</returns>
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
    /// <param name="lastValidCell">Last in-bounds cell before the beam exits.</param>
    /// <param name="beamStep">Beam step direction.</param>
    public Vector3 GetCellOuterEdgeWorld(Vector3Int lastValidCell, Vector3Int beamStep)
    {
        Vector3 center = CellToWorldCenter(lastValidCell);
        Vector2 half = GetCellHalfExtents();
        Vector2Int s = StepSign(beamStep);

        return center + new Vector3(s.x * half.x, s.y * half.y, 0f);
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Bounds / Start
    /// <summary>
    /// Checks whether a cell is within the ground tilemap bounds.
    /// </summary>
    /// <param name="cell">Cell to test.</param>
    /// <returns>True if the cell lies within bounds.</returns>
    public bool IsInBounds(Vector3Int cell) => groundBounds.Contains(cell);

    /// <summary>
    /// Returns the cached start cell or recomputes it if needed.
    /// </summary>
    public Vector3Int GetStartCell()
    {
        if (!hasCachedStart)
        {
            cachedStartCell = FindStartCellFallbackToBoundsCenter();
            hasCachedStart = true;
        }
        return cachedStartCell;
    }

    /// <summary>
    /// Searches for a Start tile or falls back to the bounds center.
    /// </summary>
    /// <returns>Cell coordinate for the start position.</returns>
    private Vector3Int FindStartCellFallbackToBoundsCenter()
    {
        // Try find a Start tile within ground bounds.
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
    // ─────────────────────────────────────────────────────────────
    #region Tile access helpers
    /// <summary>
    /// Retrieves the GameTile asset from the ground tilemap at a cell.
    /// </summary>
    private GameTile GetGroundGameTile(Vector3Int cell)
        => groundTilemap != null ? groundTilemap.GetTile<GameTile>(cell) : null;

    /// <summary>
    /// Retrieves the GameTile asset from the blocking tilemap at a cell.
    /// </summary>
    private GameTile GetBlockGameTile(Vector3Int cell)
        => blocksTilemap != null ? blocksTilemap.GetTile<GameTile>(cell) : null;

    /// <summary>
    /// Determines the logical tile kind at a cell, accounting for blocking layers.
    /// </summary>
    /// <param name="cell">Cell to query.</param>
    public TileKind GetTileKind(Vector3Int cell)
    {
        // If something exists on the blocks layer, treat it as blocking first.
        if (blocksTilemap != null && blocksTilemap.HasTile(cell))
        {
            var b = GetBlockGameTile(cell);
            return b != null ? b.kind : TileKind.Wall;
        }

        var g = GetGroundGameTile(cell);
        return g != null ? g.kind : TileKind.Void; // unpainted treated void-ish (but placement can restrict)
    }
    
    /// <summary>
    /// Scans the blocks tilemap for all unique gate key IDs.
    /// </summary>
    /// <returns>Set of gate key IDs present in the map.</returns>
    public HashSet<string> GetAllGateKeyIDsInMap()
    {
        var result = new HashSet<string>();

        if (blocksTilemap == null)
            return result;

        // Ensure bounds are accurate for scanning
        blocksTilemap.CompressBounds();
        var bounds = blocksTilemap.cellBounds;

        foreach (var cell in bounds.allPositionsWithin)
        {
            var t = blocksTilemap.GetTile<GameTile>(cell);
            if (t == null) continue;

            if (t.kind == TileKind.Gate && !string.IsNullOrEmpty(t.gateKeyID))
                result.Add(t.gateKeyID);
        }

        return result;
    }

    /// <summary>
    /// Checks whether a blocking tile exists at the cell.
    /// </summary>
    /// <param name="cell">Cell to test.</param>
    public bool IsBlockingTile(Vector3Int cell)
        => blocksTilemap != null && blocksTilemap.HasTile(cell);

    /// <summary>
    /// Checks whether the cell is blocked by a beam.
    /// </summary>
    /// <param name="cell">Cell to test.</param>
    public bool IsBeamBlocked(Vector3Int cell)
        => beamBlocked.Contains(cell);
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Walkability
    /// <summary>
    /// Central walkability rule that accounts for bounds, blocking layers, beams, obstacles, and void rules.
    /// </summary>
    /// <param name="cell">Cell to test for entry.</param>
    /// <param name="allowVoid">Whether void tiles are allowed.</param>
    /// <returns>True if the cell can be entered.</returns>
    private bool CanEnterCellInternal(Vector3Int cell, bool allowVoid)
    {
        if (!IsInBounds(cell))
            return false;

        if (IsBlockingTile(cell))
            return false;

        if (IsBeamBlocked(cell))
            return false;

        if (TryGetObstacle(cell, out var obs) && obs != null && obs.BlocksMovement())
            return false;

        var ground = GetGroundGameTile(cell);
        if (ground == null)
            return false; // restrict to painted ground footprint

        if (ground.kind == TileKind.Void)
            return allowVoid && ground.walkableByDefault;

        return ground.walkableByDefault;
    }
    
    /// <summary>
    /// Determines whether the player can enter the specified cell.
    /// </summary>
    /// <param name="cell">Cell to test for entry.</param>
    public bool CanEnterCell(Vector3Int cell)
        => CanEnterCellInternal(cell, allowWalkingIntoVoid);
    
    /// <summary>
    /// Determines whether an enemy can enter the specified cell.
    /// </summary>
    /// <param name="cell">Cell to test for entry.</param>
    public bool CanEnemyEnterCell(Vector3Int cell)
        => CanEnterCellInternal(cell, allowVoid: false);

    /// <summary>
    /// Called after movement succeeds so tiles can apply enter effects (e.g. reset).
    /// </summary>
    /// <param name="cell">Cell that was entered.</param>
    /// <param name="player">Player entering the cell.</param>
    /// <param name="fallStartWorld">World position where a fall should start from.</param>
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
    
    /// <summary>
    /// Checks whether a cell contains a gate tile and returns its metadata.
    /// </summary>
    /// <param name="cell">Cell to check.</param>
    /// <param name="gateTile">Gate tile metadata, if present.</param>
    /// <returns>True if the cell contains a gate tile.</returns>
    public bool IsGateCell(Vector3Int cell, out GameTile gateTile)
    {
        gateTile = null;

        if (blocksTilemap == null || !blocksTilemap.HasTile(cell))
            return false;

        gateTile = blocksTilemap.GetTile<GameTile>(cell);
        return gateTile != null && gateTile.kind == TileKind.Gate;
    }

    /// <summary>
    /// Attempts to unlock and remove a gate using the player's inventory.
    /// </summary>
    /// <param name="cell">Gate cell to unlock.</param>
    /// <param name="inventory">Player inventory used for key checks.</param>
    /// <returns>True if the gate was unlocked and removed.</returns>
    public bool TryUnlockGateAt(Vector3Int cell, PlayerInventory inventory)
    {
        if (inventory == null) return false;

        if (!IsGateCell(cell, out var gateTile))
            return false;

        // Gate with empty keyID acts like a wall unless you want "free gates".
        if (string.IsNullOrEmpty(gateTile.gateKeyID))
            return false;

        // Check key availability
        if (inventory.GetKeyCount(gateTile.gateKeyID) <= 0) // PlayerInventory already supports this
            return false;

        // Consume key (optional)
        if (gateTile.consumesKey)
            inventory.UseKey(gateTile.gateKeyID); // decrements + UI update

        // Remove the blocking tile and ensure ground is walkable
        blocksTilemap.SetTile(cell, null);
        if (groundTilemap != null && floorTile != null)
            groundTilemap.SetTile(cell, floorTile);

        return true;
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Obstacle registration
    /// <summary>
    /// Registers an obstacle as occupying a specific grid cell.
    /// </summary>
    /// <param name="cell">Cell occupied by the obstacle.</param>
    /// <param name="obstacle">Obstacle instance.</param>
    public void RegisterObstacle(Vector3Int cell, ObstacleBase obstacle)
    {
        if (obstacle == null) 
            return;
        
        obstacleByCell[cell] = obstacle;
    }

    /// <summary>
    /// Removes all cell registrations for the given obstacle.
    /// </summary>
    /// <param name="obstacle">Obstacle instance to unregister.</param>
    public void UnregisterObstacle(ObstacleBase obstacle)
    {
        if (obstacle == null) 
            return;

        var toRemove = new List<Vector3Int>();
        
        foreach (var kvp in obstacleByCell)
            if (kvp.Value == obstacle)
                toRemove.Add(kvp.Key);

        foreach (var c in toRemove)
            obstacleByCell.Remove(c);
    }

    /// <summary>
    /// Attempts to retrieve an obstacle registered at a cell.
    /// </summary>
    /// <param name="cell">Cell to query.</param>
    /// <param name="obstacle">Obstacle found at the cell.</param>
    /// <returns>True if an obstacle is registered at the cell.</returns>
    public bool TryGetObstacle(Vector3Int cell, out ObstacleBase obstacle)
        => obstacleByCell.TryGetValue(cell, out obstacle);
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Preview (TM_Preview)
    /// <summary>
    /// Sets preview cells for an owner ID and tints them uniformly.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the preview.</param>
    /// <param name="cells">Cells to preview.</param>
    /// <param name="color">Color to apply to each preview cell.</param>
    public void SetPreviewCellsForOwner(int ownerId, IReadOnlyList<Vector3Int> cells, Color color)
    {
        if (previewTilemap == null || previewFillTile == null) 
            return;

        ClearPreviewForOwner(ownerId);
        if (cells == null || cells.Count == 0) 
            return;

        var copy = new List<Vector3Int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
            copy.Add(cells[i]);

        previewByOwner[ownerId] = copy;

        foreach (var c in copy)
        {
            previewTilemap.SetTile(c, previewFillTile);
            previewTilemap.SetColor(c, color);
        }
    }

    /// <summary>
    /// Sets preview cells for an owner ID with per-cell colors.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the preview.</param>
    /// <param name="cells">Cells to preview.</param>
    /// <param name="perCellColors">Colors to apply per cell.</param>
    public void SetPreviewCellsForOwner(int ownerId, IReadOnlyList<Vector3Int> cells, IReadOnlyList<Color> perCellColors)
    {
        if (previewTilemap == null || previewFillTile == null) 
            return;

        ClearPreviewForOwner(ownerId);
        
        if (cells == null || cells.Count == 0) 
            return;

        var copy = new List<Vector3Int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
            copy.Add(cells[i]);

        previewByOwner[ownerId] = copy;

        for (int i = 0; i < copy.Count; i++)
        {
            var c = copy[i];
            previewTilemap.SetTile(c, previewFillTile);

            Color col = (perCellColors != null && i < perCellColors.Count) ? perCellColors[i] : Color.white;
            previewTilemap.SetColor(c, col);
        }
    }

    /// <summary>
    /// Temporarily displays a preview for a duration, with token-based invalidation.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the preview.</param>
    /// <param name="cells">Cells to preview.</param>
    /// <param name="color">Color to apply to each preview cell.</param>
    /// <param name="duration">Seconds to display the preview.</param>
    public void FlashPreviewCellsForOwner(int ownerId, IReadOnlyList<Vector3Int> cells, Color color, float duration)
    {
        if (previewTilemap == null || previewFillTile == null) 
            return;
        
        if (cells == null || cells.Count == 0) 
            return;

        // bump token so older coroutines don’t clear a newer telegraph
        int token = 1;
        if (previewTokenByOwner.TryGetValue(ownerId, out int existing))
            token = existing + 1;
        previewTokenByOwner[ownerId] = token;

        SetPreviewCellsForOwner(ownerId, cells, color);
        StartCoroutine(ClearPreviewAfterDelay(ownerId, token, duration));
    }

    /// <summary>
    /// Clears any preview tiles owned by the specified owner ID.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the preview.</param>
    public void ClearPreviewForOwner(int ownerId)
    {
        if (previewTilemap == null) 
            return;

        if (previewByOwner.TryGetValue(ownerId, out var cells))
        {
            foreach (var c in cells)
            {
                previewTilemap.SetTile(c, null);
                previewTilemap.SetColor(c, Color.white);
            }
        }

        previewByOwner.Remove(ownerId);
    }

    /// <summary>
    /// Clears a preview after a delay if its token still matches the current owner token.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the preview.</param>
    /// <param name="token">Token captured when the preview was shown.</param>
    /// <param name="duration">Seconds to wait before clearing.</param>
    private IEnumerator ClearPreviewAfterDelay(int ownerId, int token, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!previewTokenByOwner.TryGetValue(ownerId, out int current) || current != token)
            yield break;

        ClearPreviewForOwner(ownerId);
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Overlay Effect
    /// <summary>
    /// Enables or disables the halftime overlay effect across ground tiles.
    /// </summary>
    /// <param name="enabled">Whether the overlay should be active.</param>
    /// <param name="overlayColor">Base overlay color.</param>
    /// <param name="flashInterval">Seconds between opacity pulses.</param>
    public void SetHalfTimeOverlay(bool enabled, Color overlayColor, float flashInterval = 0.2f)
    {
        if (overlayTilemap == null || previewFillTile == null) 
            return;

        StopHalfTimeOverlayRoutine();

        if (!enabled)
        {
            ClearOverlayForOwner(OVERLAY_OWNER_HALFTIME);
            return;
        }

        // cover the painted ground footprint only
        var cells = new List<Vector3Int>();
        foreach (var pos in groundBounds.allPositionsWithin)
        {
            if (GetGroundGameTile(pos) != null)
                cells.Add(pos);
        }

        halfTimeRoutine = StartCoroutine(HalfTimeOverlayRoutine(cells, overlayColor, flashInterval));
    }

    /// <summary>
    /// Stops any active halftime overlay coroutine.
    /// </summary>
    private void StopHalfTimeOverlayRoutine()
    {
        if (halfTimeRoutine != null)
        {
            StopCoroutine(halfTimeRoutine);
            halfTimeRoutine = null;
        }
    }

    /// <summary>
    /// Pulses overlay tile colors at a fixed interval.
    /// </summary>
    /// <param name="cells">Cells to overlay.</param>
    /// <param name="baseColor">Base color to pulse.</param>
    /// <param name="interval">Seconds between pulses.</param>
    private IEnumerator HalfTimeOverlayRoutine(List<Vector3Int> cells, Color baseColor, float interval)
    {
        // Place tiles once
        SetOverlayCells(OVERLAY_OWNER_HALFTIME, cells, baseColor);

        bool on = true;
        while (true)
        {
            on = !on;

            Color c = baseColor;
            c.a = on ? baseColor.a : 0f;

            foreach (var cell in cells)
                overlayTilemap.SetColor(cell, c);

            yield return new WaitForSeconds(interval);
        }
    }
    
    /// <summary>
    /// Sets overlay tiles for an owner ID and tints them uniformly.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the overlay.</param>
    /// <param name="cells">Cells to overlay.</param>
    /// <param name="color">Color to apply to each overlay cell.</param>
    public void SetOverlayCells(int ownerId, IReadOnlyList<Vector3Int> cells, Color color)
    {
        if (overlayTilemap == null || previewFillTile == null) 
            return;

        ClearOverlayForOwner(ownerId);
        
        if (cells == null || cells.Count == 0) 
            return;

        var copy = new List<Vector3Int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
            copy.Add(cells[i]);

        overlayByOwner[ownerId] = copy;

        foreach (var c in copy)
        {
            overlayTilemap.SetTile(c, previewFillTile); // reuse same fill tile
            overlayTilemap.SetColor(c, color);
        }
    }

    /// <summary>
    /// Clears overlay tiles owned by the specified owner ID.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the overlay.</param>
    public void ClearOverlayForOwner(int ownerId)
    {
        if (overlayTilemap == null) 
            return;

        if (overlayByOwner.TryGetValue(ownerId, out var cells))
        {
            foreach (var c in cells)
            {
                overlayTilemap.SetTile(c, null);
                overlayTilemap.SetColor(c, Color.white);
            }
        }

        overlayByOwner.Remove(ownerId);
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Shape placement (Void -> Floor)
    /// <summary>
    /// Shape placement rule:
    /// - every cell must be within bounds
    /// - every cell must currently be a *painted* Void GameTile
    /// - must NOT be blocked by blocks tilemap, beam, or an obstacle that BlocksShapePlacement
    /// </summary>
    /// <param name="originCell">Origin cell where the shape is anchored.</param>
    /// <param name="offsets">Offsets that form the shape footprint.</param>
    /// <param name="playerCell">Current player cell for overlap checks.</param>
    /// <param name="overlapsPlayer">Outputs whether the shape overlaps the player cell.</param>
    /// <returns>True if placement rules allow the shape.</returns>
    public bool CanPlaceShapeOnVoid(Vector3Int originCell, Vector2Int[] offsets, Vector3Int playerCell, out bool overlapsPlayer)
    {
        overlapsPlayer = false;

        if (offsets == null || offsets.Length == 0)
            return false;

        for (int i = 0; i < offsets.Length; i++)
        {
            var off = offsets[i];
            var cell = originCell + new Vector3Int(off.x, off.y, 0);

            if (cell == playerCell)
                overlapsPlayer = true;

            if (!IsInBounds(cell))
                return false;

            if (IsBlockingTile(cell))
                return false;

            if (IsBeamBlocked(cell))
                return false;

            if (TryGetObstacle(cell, out var obs) && obs != null && obs.BlocksShapePlacement())
                return false;

            var g = GetGroundGameTile(cell);
            if (g == null)
                return false; // restrict placement to painted void tiles only

            if (g.kind != TileKind.Void)
                return false;
        }

        // Overlapping player is considered invalid placement.
        if (overlapsPlayer)
            return false;

        return true;
    }

    /// <summary>
    /// Converts void tiles under a shape footprint into floor tiles.
    /// </summary>
    /// <param name="originCell">Origin cell where the shape is anchored.</param>
    /// <param name="offsets">Offsets that form the shape footprint.</param>
    public void ApplyShapeToVoid(Vector3Int originCell, Vector2Int[] offsets)
    {
        if (groundTilemap == null || floorTile == null) 
            return;
        
        if (offsets == null) 
            return;

        for (int i = 0; i < offsets.Length; i++)
        {
            var off = offsets[i];
            var cell = originCell + new Vector3Int(off.x, off.y, 0);

            var g = GetGroundGameTile(cell);
            if (g != null && g.kind == TileKind.Void)
            {
                groundTilemap.SetTile(cell, floorTile);
            }
        }
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Lever Obstacle
    /// <summary>
    /// Toggles a floor tile to void (or vice versa) if safe to do so.
    /// </summary>
    /// <param name="cell">Cell to toggle.</param>
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
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Mirror Obstacle
    /// <summary>
    /// Updates beam-blocked cells for a specific owner.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the beam.</param>
    /// <param name="newCells">Cells to mark as beam-blocked.</param>
    public void SetBeamCellsForOwner(int ownerId, List<Vector3Int> newCells)
    {
        if (beamByOwner.TryGetValue(ownerId, out var old))
        {
            foreach (var c in old) beamBlocked.Remove(c);
        }

        beamByOwner[ownerId] = newCells;

        foreach (var c in newCells) beamBlocked.Add(c);
    }

    /// <summary>
    /// Clears beam-blocked cells for a specific owner.
    /// </summary>
    /// <param name="ownerId">Owner identifier for the beam.</param>
    public void ClearBeamCellsForOwner(int ownerId)
    {
        if (!beamByOwner.TryGetValue(ownerId, out var old)) return;

        foreach (var c in old) beamBlocked.Remove(c);
        beamByOwner.Remove(ownerId);
    }
    #endregion
}

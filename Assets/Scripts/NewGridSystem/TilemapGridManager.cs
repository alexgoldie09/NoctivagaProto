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
    public Vector3Int WorldToCell(Vector3 worldPos) => grid.WorldToCell(worldPos);

    // Use Tilemap.GetCellCenterWorld since GridLayout doesn't expose it consistently
    public Vector3 CellToWorldCenter(Vector3Int cell) => groundTilemap.GetCellCenterWorld(cell);
    
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
    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Bounds / Start
    public bool IsInBounds(Vector3Int cell) => groundBounds.Contains(cell);

    public Vector3Int GetStartCell()
    {
        if (!hasCachedStart)
        {
            cachedStartCell = FindStartCellFallbackToBoundsCenter();
            hasCachedStart = true;
        }
        return cachedStartCell;
    }

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
    private GameTile GetGroundGameTile(Vector3Int cell)
        => groundTilemap != null ? groundTilemap.GetTile<GameTile>(cell) : null;

    private GameTile GetBlockGameTile(Vector3Int cell)
        => blocksTilemap != null ? blocksTilemap.GetTile<GameTile>(cell) : null;

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

    public bool IsBlockingTile(Vector3Int cell)
        => blocksTilemap != null && blocksTilemap.HasTile(cell);

    public bool IsBeamBlocked(Vector3Int cell)
        => beamBlocked.Contains(cell);
    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Walkability
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

    // Player rule
    public bool CanEnterCell(Vector3Int cell)
        => CanEnterCellInternal(cell, allowWalkingIntoVoid);

    // Enemy rule (void never enterable)
    public bool CanEnemyEnterCell(Vector3Int cell)
        => CanEnterCellInternal(cell, allowVoid: false);

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
    
    public bool IsGateCell(Vector3Int cell, out GameTile gateTile)
    {
        gateTile = null;

        if (blocksTilemap == null || !blocksTilemap.HasTile(cell))
            return false;

        gateTile = blocksTilemap.GetTile<GameTile>(cell);
        return gateTile != null && gateTile.kind == TileKind.Gate;
    }

    public bool TryUnlockGateAt(Vector3Int cell, PlayerInventory inventory)
    {
        if (inventory == null) return false;

        if (!IsGateCell(cell, out var gateTile))
            return false;

        // Gate with empty keyID acts like a wall unless you want "free gates".
        if (string.IsNullOrEmpty(gateTile.gateKeyID))
            return false;

        // Check key availability
        if (inventory.GetKeyCount(gateTile.gateKeyID) <= 0) // PlayerInventory already supports this :contentReference[oaicite:3]{index=3}
            return false;

        // Consume key (optional)
        if (gateTile.consumesKey)
            inventory.UseKey(gateTile.gateKeyID); // decrements + UI update :contentReference[oaicite:4]{index=4}

        // Remove the blocking tile and ensure ground is walkable
        blocksTilemap.SetTile(cell, null);
        if (groundTilemap != null && floorTile != null)
            groundTilemap.SetTile(cell, floorTile);

        return true;
    }
    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Obstacle registration
    public void RegisterObstacle(Vector3Int cell, ObstacleBase obstacle)
    {
        if (obstacle == null) return;
        obstacleByCell[cell] = obstacle;
    }

    public void UnregisterObstacle(ObstacleBase obstacle)
    {
        if (obstacle == null) return;

        var toRemove = new List<Vector3Int>();
        foreach (var kvp in obstacleByCell)
            if (kvp.Value == obstacle)
                toRemove.Add(kvp.Key);

        foreach (var c in toRemove)
            obstacleByCell.Remove(c);
    }

    public bool TryGetObstacle(Vector3Int cell, out ObstacleBase obstacle)
        => obstacleByCell.TryGetValue(cell, out obstacle);
    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Preview (TM_Preview)
    public void SetPreviewCellsForOwner(int ownerId, IReadOnlyList<Vector3Int> cells, Color color)
    {
        if (previewTilemap == null || previewFillTile == null) return;

        ClearPreviewForOwner(ownerId);
        if (cells == null || cells.Count == 0) return;

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

    public void SetPreviewCellsForOwner(int ownerId, IReadOnlyList<Vector3Int> cells, IReadOnlyList<Color> perCellColors)
    {
        if (previewTilemap == null || previewFillTile == null) return;

        ClearPreviewForOwner(ownerId);
        if (cells == null || cells.Count == 0) return;

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

    public void FlashPreviewCellsForOwner(int ownerId, IReadOnlyList<Vector3Int> cells, Color color, float duration)
    {
        if (previewTilemap == null || previewFillTile == null) return;
        if (cells == null || cells.Count == 0) return;

        // bump token so older coroutines don’t clear a newer telegraph
        int token = 1;
        if (previewTokenByOwner.TryGetValue(ownerId, out int existing))
            token = existing + 1;
        previewTokenByOwner[ownerId] = token;

        SetPreviewCellsForOwner(ownerId, cells, color);
        StartCoroutine(ClearPreviewAfterDelay(ownerId, token, duration));
    }

    public void ClearPreviewForOwner(int ownerId)
    {
        if (previewTilemap == null) return;

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
    public void SetHalfTimeOverlay(bool enabled, Color overlayColor, float flashInterval = 0.2f)
    {
        if (overlayTilemap == null || previewFillTile == null) return;

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

    private void StopHalfTimeOverlayRoutine()
    {
        if (halfTimeRoutine != null)
        {
            StopCoroutine(halfTimeRoutine);
            halfTimeRoutine = null;
        }
    }

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
    
    public void SetOverlayCells(int ownerId, IReadOnlyList<Vector3Int> cells, Color color)
    {
        if (overlayTilemap == null || previewFillTile == null) return;

        ClearOverlayForOwner(ownerId);
        if (cells == null || cells.Count == 0) return;

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

    public void ClearOverlayForOwner(int ownerId)
    {
        if (overlayTilemap == null) return;

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

    public void ApplyShapeToVoid(Vector3Int originCell, Vector2Int[] offsets)
    {
        if (groundTilemap == null || floorTile == null) return;
        if (offsets == null) return;

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
    #region Lever helper (Floor <-> Void)
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
    #region Beams (MirrorObstacle integration)
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

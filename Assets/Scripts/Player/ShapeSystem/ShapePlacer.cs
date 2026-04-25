using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles placement of Tetris-like shapes on the tilemap grid based on the player's position and facing.
/// - Uses TilemapGridManager for validation + applying tiles.
/// - Uses TM_Preview + previewFillTile for in-game preview.
/// - No direct input reading: PlayerController calls the public methods.
/// </summary>
public class ShapePlacer : MonoBehaviour
{
    [Header("Placement FX")]
    [Tooltip("Dust prefab spawned on adjacent edge cells after a successful shape placement.")]
    [SerializeField] private GameObject edgeDustPrefab;
    
    [Header("Preview Colors")]
    public Color validColor = new (0f, 1f, 0f, 0.5f);
    public Color invalidColor = new (1f, 0f, 0f, 0.5f);
    public Color overPlayerColor = new (0f, 1f, 1f, 0.5f);

    private int currentShapeIndex = 0;
    private int currentRotation = 0;

    private PlayerController player;
    private PlayerInventory inventory;
    private TilemapGridManager grid;

    private readonly List<Vector3Int> previewCells = new();
    private readonly List<Color> previewColors = new();
    
    /// <summary>
    /// Fired after a successful placement that converted void -> floor.
    /// Provides the list of cells affected by that placement.
    /// </summary>
    public event Action<IReadOnlyList<Vector3Int>> OnShapePlaced;

    /// <summary>
    /// Gets the current shape index in the inventory list.
    /// </summary>
    public int CurrentIndex => currentShapeIndex;

    private ShapeInventoryEntry CurrentShapeEntry =>
        (inventory != null && inventory.shapeInventory != null && inventory.shapeInventory.Count > 0)
            ? inventory.shapeInventory[currentShapeIndex]
            : null;

    #region Unity Lifecycle
    /// <summary>
    /// Initializes references and sets an initial shape preview.
    /// </summary>
    private void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        inventory = FindFirstObjectByType<PlayerInventory>();
        grid = TilemapGridManager.Instance;

        if (player == null) 
            Debug.LogError("[ShapePlacer] PlayerController not found.");
        if (inventory == null) 
            Debug.LogError("[ShapePlacer] PlayerInventory not found.");
        if (grid == null) 
            Debug.LogError("[ShapePlacer] TilemapGridManager.Instance not found.");

        // Ensure we start with a valid index
        CycleShape(0);
        UpdatePreview();
    }

    /// <summary>
    /// Maintains placement preview visibility based on placement mode and freeze state.
    /// </summary>
    private void Update()
    {
        if (Utilities.IsGameFrozen)
        {
            grid?.ClearPreviewForOwner(GetInstanceID());
            return;
        }
        
        if (grid == null) 
            return;

        if (Utilities.IsPlacementModeActive)
            UpdatePreview();
        else
            grid.ClearPreviewForOwner(GetInstanceID());
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Public API
    /// <summary>
    /// Toggles placement mode and refreshes the placement preview.
    /// </summary>
    public void TogglePlacementMode()
    {
        // Prevent entering placement mode with an empty inventory
        if (!Utilities.IsPlacementModeActive && (inventory == null || !inventory.HasAnyShape()))
            return;
        
        Utilities.IsPlacementModeActive = !Utilities.IsPlacementModeActive;

        if (!Utilities.IsPlacementModeActive)
        {
            grid?.ClearPreviewForOwner(GetInstanceID());
            return;
        }

        // When entering placement mode, ensure we land on a shape that exists
        CycleShape(1);
        
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("shape_place_next", 0.2f);
        
        UpdatePreview();
    }

    /// <summary>
    /// Rotates the current shape clockwise in placement mode.
    /// </summary>
    public void RotateCW()
    {
        if (!Utilities.IsPlacementModeActive) 
            return;
        
        currentRotation = (currentRotation + 1) % 4;
        
        if (AudioManager.Instance != null && inventory.HasAnyShape())
            AudioManager.Instance.PlaySFX("shape_place_rotate", 0.2f);
        
        UpdatePreview();
    }

    /// <summary>
    /// Rotates the current shape counterclockwise in placement mode.
    /// </summary>
    public void RotateCCW()
    {
        if (!Utilities.IsPlacementModeActive) 
            return;
        
        currentRotation = (currentRotation + 3) % 4; // -1 mod 4
        
        if (AudioManager.Instance != null && inventory.HasAnyShape())
            AudioManager.Instance.PlaySFX("shape_place_rotate", 0.2f);
        
        UpdatePreview();
    }

    /// <summary>
    /// Selects the next available shape in the inventory.
    /// </summary>
    public void CycleNext()
    {
        if (!Utilities.IsPlacementModeActive) 
            return;
        
        CycleShape(1);
        
        if (AudioManager.Instance != null && inventory.AvailableShapeCount() > 1)
            AudioManager.Instance.PlaySFX("shape_place_next", 0.2f);

        UpdatePreview();
    }

    /// <summary>
    /// Selects the previous available shape in the inventory.
    /// </summary>
    public void CyclePrev()
    {
        if (!Utilities.IsPlacementModeActive) 
            return;
        
        CycleShape(-1);
        
        if (AudioManager.Instance != null && inventory.AvailableShapeCount() > 1)
            AudioManager.Instance.PlaySFX("shape_place_next", 0.2f);

        UpdatePreview();
    }

    /// <summary>
    /// Attempts to place the current shape on valid void tiles.
    /// </summary>
    public void TryPlace()
    {
        if (!Utilities.IsPlacementModeActive) 
            return;
        
        if (player == null || inventory == null || grid == null) 
            return;
        
        if (inventory.shapeInventory == null || inventory.shapeInventory.Count == 0) 
            return;

        var entry = CurrentShapeEntry;
        if (entry == null || entry.shapeData == null) 
            return;
        
        if (!inventory.HasShape(entry.shapeData)) 
            return;

        var rotatedOffsets = GetRotatedOffsets(entry.shapeData.tileOffsets, currentRotation);

        // Origin that never overlaps player (even after rotation)
        var originCell = GetSanitizedOriginCell(rotatedOffsets);

        bool overlapsPlayer;
        bool canPlace = grid.CanPlaceShapeOnVoid(originCell, rotatedOffsets, player.CellPosition, out overlapsPlayer);

        if (!canPlace)
            return;

        // Apply: Void -> Floor
        grid.ApplyShapeToVoid(originCell, rotatedOffsets);
        
        // Notify listeners (boss, achievements, etc.) which cells were placed
        // Build list once per placement; reuse a local list to avoid allocations if you want later.
        var placedCells = new List<Vector3Int>(rotatedOffsets.Length);
        foreach (var off in rotatedOffsets)
        {
            placedCells.Add(originCell + new Vector3Int(off.x, off.y, 0));
        }
        SpawnEdgeDust(placedCells);
        OnShapePlaced?.Invoke(placedCells);

        // Consume inventory
        inventory.ConsumeShape(entry.shapeData);
        
        // Play audio clip
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("shape_place_drop", 0.25f);

        // Score only on successful placement
        BeatHitQuality quality = RhythmManager.Instance.GetHitQuality();
        int points = Utilities.GetPointsForQuality(quality);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterMove();
            ScoreManager.Instance.AddRhythmScore(points, quality);
        }

        // If we ran out, move to next available shape
        if (!inventory.HasShape(entry.shapeData))
            CycleShape(1);

        // Auto-exit if inventory is now completely empty
        if (!inventory.HasAnyShape())
        {
            Utilities.IsPlacementModeActive = false;
            grid?.ClearPreviewForOwner(GetInstanceID());
    
            return; // Skip UpdatePreview — nothing left to show
        }

        UpdatePreview();
    }
    
    /// <summary>
    /// Clears any preview tiles owned by this ShapePlacer.
    /// </summary>
    public void ClearPreview()
    {
        grid?.ClearPreviewForOwner(GetInstanceID());
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Cycle/Rotator Functions
    private static readonly Vector2Int[] OrbitDirections = new[]
    {
        new Vector2Int( 0,  1),  // 0 = up
        new Vector2Int( 1,  0),  // 1 = right
        new Vector2Int( 0, -1),  // 2 = down
        new Vector2Int(-1,  0),  // 3 = left
    };
    /// <summary>
    /// Returns a placement origin one cell in front of the player,
    /// then nudges forward if the rotated shape would overlap the player cell.
    /// </summary>
    /// <param name="rotatedOffsets">Offsets for the current rotation.</param>
    /// <returns>Sanitized origin cell for placement.</returns>
    private Vector3Int GetSanitizedOriginCell(Vector2Int[] rotatedOffsets)
    {
        // Single tile: orbit around player using currentRotation as direction index
        if (rotatedOffsets.Length == 1)
        {
            var dir = OrbitDirections[currentRotation % 4];
            return player.CellPosition + new Vector3Int(dir.x, dir.y, 0);
        }

        // Multi-tile: existing facing-based logic
        Vector2Int f = player.FacingDirection;
        if (f == Vector2Int.zero)
            f = Vector2Int.right;

        Vector3Int step = new Vector3Int(f.x, f.y, 0);
        Vector3Int origin = player.CellPosition + step;

        if (origin == player.CellPosition)
            origin += step;

        const int maxNudges = 3;
        for (int i = 0; i < maxNudges; i++)
        {
            if (!WouldOverlapPlayer(origin, rotatedOffsets, player.CellPosition))
                break;
            origin += step;
        }

        return origin;
    }

    /// <summary>
    /// Checks whether a shape footprint would overlap the player cell.
    /// </summary>
    /// <param name="origin">Origin cell for the shape.</param>
    /// <param name="offsets">Offsets that form the shape footprint.</param>
    /// <param name="playerCell">Current player cell.</param>
    /// <returns>True if the shape overlaps the player cell.</returns>
    private static bool WouldOverlapPlayer(Vector3Int origin, Vector2Int[] offsets, Vector3Int playerCell)
    {
        if (offsets == null) return false;

        for (int i = 0; i < offsets.Length; i++)
        {
            var off = offsets[i];
            var c = origin + new Vector3Int(off.x, off.y, 0);
            if (c == playerCell)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Advances the current shape index to the next available shape.
    /// </summary>
    /// <param name="direction">Direction to cycle (+1 or -1).</param>
    private void CycleShape(int direction)
    {
        if (inventory == null || inventory.shapeInventory == null) return;

        int count = inventory.shapeInventory.Count;
        if (count == 0) 
            return;

        int tries = 0;
        
        do
        {
            currentShapeIndex = (currentShapeIndex + direction + count) % count;
            tries++;
        }
        while (CurrentShapeEntry != null && CurrentShapeEntry.count <= 0 && tries < count);
    }

    /// <summary>
    /// Builds and shows preview tiles for the current shape placement.
    /// </summary>
    private void UpdatePreview()
    {
        if (player == null || inventory == null || grid == null) 
            return;
        
        if (inventory.shapeInventory == null || inventory.shapeInventory.Count == 0) 
            return;

        var entry = CurrentShapeEntry;
        if (entry == null || entry.shapeData == null)
        {
            grid.ClearPreviewForOwner(GetInstanceID());
            return;
        }

        if (!inventory.HasShape(entry.shapeData))
        {
            grid.ClearPreviewForOwner(GetInstanceID());
            return;
        }

        var rotatedOffsets = GetRotatedOffsets(entry.shapeData.tileOffsets, currentRotation);

        // Origin that never overlaps player (even after rotation)
        var originCell = GetSanitizedOriginCell(rotatedOffsets);

        // Build preview cells + per-cell colors
        previewCells.Clear();
        previewColors.Clear();

        bool overlapsPlayer;
        bool canPlace = grid.CanPlaceShapeOnVoid(originCell, rotatedOffsets, player.CellPosition, out overlapsPlayer);

        Color baseColor = canPlace ? validColor : invalidColor;

        for (int i = 0; i < rotatedOffsets.Length; i++)
        {
            var off = rotatedOffsets[i];
            var c = originCell + new Vector3Int(off.x, off.y, 0);

            previewCells.Add(c);

            // Cyan warning if it overlaps player cell (shouldn't happen after sanitize,
            // but keep it as a safety visual).
            previewColors.Add(c == player.CellPosition ? overPlayerColor : baseColor);
        }

        grid.SetPreviewCellsForOwner(GetInstanceID(), previewCells, previewColors);
    }

    /// <summary>
    /// Returns a copy of offsets rotated clockwise by the given steps.
    /// </summary>
    /// <param name="original">Original shape offsets.</param>
    /// <param name="rotationStepsCw">Number of 90-degree clockwise rotations.</param>
    /// <returns>Rotated offsets.</returns>
    private static Vector2Int[] GetRotatedOffsets(Vector2Int[] original, int rotationStepsCw)
    {
        if (original == null) return Array.Empty<Vector2Int>();

        Vector2Int[] result = new Vector2Int[original.Length];

        for (int i = 0; i < original.Length; i++)
        {
            Vector2Int p = original[i];
            for (int r = 0; r < rotationStepsCw; r++)
                p = new Vector2Int(-p.y, p.x); // 90° clockwise
            result[i] = p;
        }

        return result;
    }
    
    /// <summary>
    /// Spawns one dust object on each exposed cardinal edge around the placed shape.
    /// Dust appears nudged inward at the midpoint between the placed cell and its adjacent edge cell.
    /// </summary>
    /// <param name="placedCells">Cells converted from void to floor by the placement.</param>
    private void SpawnEdgeDust(IReadOnlyList<Vector3Int> placedCells)
    {
        if (edgeDustPrefab == null || grid == null || placedCells == null || placedCells.Count == 0)
            return;

        var shapeCells = new HashSet<Vector3Int>(placedCells);

        Vector3Int[] directions =
        {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right
        };

        for (int i = 0; i < placedCells.Count; i++)
        {
            Vector3Int placedCell = placedCells[i];
            Vector3 placedWorld = grid.CellToWorldCenter(placedCell);

            for (int d = 0; d < directions.Length; d++)
            {
                Vector3Int adjacentCell = placedCell + directions[d];

                // Skip interior edges; only exposed perimeter edges should spawn dust.
                if (shapeCells.Contains(adjacentCell))
                    continue;

                if (!grid.IsInBounds(adjacentCell))
                    continue;

                Vector3 adjacentWorld = grid.CellToWorldCenter(adjacentCell);
                Vector3 spawnPos = Vector3.Lerp(placedWorld, adjacentWorld, 0.5f);
                Instantiate(edgeDustPrefab, spawnPos, Quaternion.identity);
            }
        }
    }
    #endregion
}

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
    [Header("Preview Colors")]
    public Color validColor = new Color(0f, 1f, 0f, 0.5f);
    public Color invalidColor = new Color(1f, 0f, 0f, 0.5f);
    public Color overPlayerColor = new Color(0f, 1f, 1f, 0.5f);

    private int currentShapeIndex = 0;
    private int currentRotation = 0;

    private PlayerController player;
    private PlayerInventory inventory;
    private TilemapGridManager grid;

    private readonly List<Vector3Int> previewCells = new();
    private readonly List<Color> previewColors = new();

    public int CurrentIndex => currentShapeIndex;

    private ShapeInventoryEntry CurrentShapeEntry =>
        (inventory != null && inventory.shapeInventory != null && inventory.shapeInventory.Count > 0)
            ? inventory.shapeInventory[currentShapeIndex]
            : null;

    private void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        inventory = FindFirstObjectByType<PlayerInventory>();
        grid = TilemapGridManager.Instance;

        if (player == null) Debug.LogError("[ShapePlacer] PlayerController not found.");
        if (inventory == null) Debug.LogError("[ShapePlacer] PlayerInventory not found.");
        if (grid == null) Debug.LogError("[ShapePlacer] TilemapGridManager.Instance not found.");

        // Ensure we start with a valid index
        CycleShape(0);
        UpdatePreview();
    }

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

    // ─────────────────────────────────────────────────────────────
    #region Public API (called by PlayerController)

    public void TogglePlacementMode()
    {
        Utilities.IsPlacementModeActive = !Utilities.IsPlacementModeActive;

        if (!Utilities.IsPlacementModeActive)
        {
            grid?.ClearPreviewForOwner(GetInstanceID());
            return;
        }

        // When entering placement mode, ensure we land on a shape that exists
        CycleShape(1);
        UpdatePreview();
    }

    public void RotateCW()
    {
        if (!Utilities.IsPlacementModeActive) return;
        currentRotation = (currentRotation + 1) % 4;
        UpdatePreview();
    }

    public void RotateCCW()
    {
        if (!Utilities.IsPlacementModeActive) return;
        currentRotation = (currentRotation + 3) % 4; // -1 mod 4
        UpdatePreview();
    }

    public void CycleNext()
    {
        if (!Utilities.IsPlacementModeActive) return;
        CycleShape(1);
        UpdatePreview();
    }

    public void CyclePrev()
    {
        if (!Utilities.IsPlacementModeActive) return;
        CycleShape(-1);
        UpdatePreview();
    }

    public void TryPlace()
    {
        if (!Utilities.IsPlacementModeActive) return;
        if (player == null || inventory == null || grid == null) return;
        if (inventory.shapeInventory == null || inventory.shapeInventory.Count == 0) return;

        var entry = CurrentShapeEntry;
        if (entry == null || entry.shapeData == null) return;
        if (!inventory.HasShape(entry.shapeData)) return;

        var rotatedOffsets = GetRotatedOffsets(entry.shapeData.tileOffsets, currentRotation);

        // Origin that never overlaps player (even after rotation)
        var originCell = GetSanitizedOriginCell(rotatedOffsets);

        bool overlapsPlayer;
        bool canPlace = grid.CanPlaceShapeOnVoid(originCell, rotatedOffsets, player.CellPosition, out overlapsPlayer);

        if (!canPlace)
            return;

        // Apply: Void -> Floor
        grid.ApplyShapeToVoid(originCell, rotatedOffsets);

        // Consume inventory
        inventory.ConsumeShape(entry.shapeData);

        // Score only on successful placement
        BeatHitQuality quality = RhythmManager.Instance.GetHitQuality();
        int points = Utilities.GetPointsForQuality(quality);

        ScoreManager.Instance.RegisterMove();
        ScoreManager.Instance.AddRhythmScore(points, quality);

        // If we ran out, move to next available shape
        if (!inventory.HasShape(entry.shapeData))
            CycleShape(1);

        UpdatePreview();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a placement origin one cell in front of the player,
    /// then nudges forward if the rotated shape would overlap the player cell.
    /// </summary>
    private Vector3Int GetSanitizedOriginCell(Vector2Int[] rotatedOffsets)
    {
        Vector2Int f = player.FacingDirection;

        // Fallback if facing is ever zero (prevents origin == player)
        if (f == Vector2Int.zero)
            f = Vector2Int.right;

        Vector3Int step = new Vector3Int(f.x, f.y, 0);

        // Start one cell ahead (desired behavior)
        Vector3Int origin = player.CellPosition + step;

        // Extra safety: if something still results in origin on player, push again
        if (origin == player.CellPosition)
            origin += step;

        // If rotation causes the shape to overlap the player cell, nudge forward.
        // Cap to avoid infinite loops in weird cases.
        const int maxNudges = 3;
        for (int i = 0; i < maxNudges; i++)
        {
            if (!WouldOverlapPlayer(origin, rotatedOffsets, player.CellPosition))
                break;

            origin += step;
        }

        return origin;
    }

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

    private void CycleShape(int direction)
    {
        if (inventory == null || inventory.shapeInventory == null) return;

        int count = inventory.shapeInventory.Count;
        if (count == 0) return;

        int tries = 0;

        do
        {
            currentShapeIndex = (currentShapeIndex + direction + count) % count;
            tries++;
        }
        while (CurrentShapeEntry != null && CurrentShapeEntry.count <= 0 && tries < count);
    }

    private void UpdatePreview()
    {
        if (player == null || inventory == null || grid == null) return;
        if (inventory.shapeInventory == null || inventory.shapeInventory.Count == 0) return;

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
}

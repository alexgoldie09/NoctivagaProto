using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles placement of Tetris-like shapes on the grid based on the player's position and facing.
/// Supports previewing, rotation, cycling shapes, and placement validation.
/// </summary>
public class ShapePlacer : MonoBehaviour
{
    [Header("Settings")]
    public GameObject previewTilePrefab;
    public Color validColor = new Color(0f, 1f, 0f, 0.5f);
    public Color invalidColor = new Color(1f, 0f, 0f, 0.5f);
    public Color overPlayerColor = new Color(0f, 1f, 1f, 0.5f); // Cyan = warning if shape overlaps player

    private int currentShapeIndex = 0;
    private int currentRotation = 0;
    private PlayerController player;
    private PlayerInventory inventory;
    private GridManager gridManager;
    private List<GameObject> previewTiles = new List<GameObject>();

    /// <summary> The currently selected shape from the player's inventory. </summary>
    private ShapeInventoryEntry CurrentShapeEntry => inventory.shapeInventory[currentShapeIndex];

    /// <summary> Public accessor for the current shape index. </summary>
    public int CurrentIndex => currentShapeIndex;

    void Start()
    {
        player = GetComponentInParent<PlayerController>();
        inventory = GetComponentInParent<PlayerInventory>();
        gridManager = GridManager.Instance;

        if (player == null)
            Debug.LogError("ShapePlacer must be a child of an object with PlayerController.");

        if (inventory == null)
            Debug.LogError("ShapePlacer must be a child of an object with PlayerInventory.");

        if (gridManager == null)
            Debug.LogError("Grid Manager instance does not exist.");

        UpdatePreview();
    }

    void Update()
    {
        HandleToggleInput();

        if (Utilities.IsPlacementModeActive)
        {
            HandleInput();
            UpdatePreview();
        }
        else
        {
            ClearPreview();
        }
    }

    /// <summary>
    /// Toggles placement mode using the F key.
    /// </summary>
    private void HandleToggleInput()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            Utilities.IsPlacementModeActive = !Utilities.IsPlacementModeActive;
        }
    }

    /// <summary>
    /// Handles rotation, shape switching, and shape placement input.
    /// </summary>
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            CycleShape(-1);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            CycleShape(1);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotation = (currentRotation + 1) % 4; // 0-3 (0°, 90°, 180°, 270°)
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryPlaceCurrentShape();
        }
    }

    /// <summary>
    /// Changes the current selected shape in inventory.
    /// Wraps around if necessary and skips empty entries.
    /// </summary>
    private void CycleShape(int direction)
    {
        int tries = 0;
        int count = inventory.shapeInventory.Count;

        do
        {
            currentShapeIndex = (currentShapeIndex + direction + count) % count;
            tries++;
        } while (CurrentShapeEntry.count <= 0 && tries < count); // Avoid shapes with 0 count
    }

    /// <summary>
    /// Attempts to place the currently selected shape on the grid in front of the player.
    /// </summary>
    private void TryPlaceCurrentShape()
    {
        var shape = CurrentShapeEntry.shapeData;
        var origin = player.GridPosition + player.FacingDirection;
        Vector2Int[] rotatedOffsets = GetRotatedOffsets(shape.tileOffsets, currentRotation);

        // Placement validation
        if (!gridManager.CanPlaceShape(origin, rotatedOffsets))
            return;

        // Apply shape tiles to the grid
        foreach (var offset in rotatedOffsets)
        {
            Vector2Int pos = origin + offset;
            GridTile tile = gridManager.GetTileAt(pos.x, pos.y);
            if (tile != null)
            {
                tile.tileType = TileType.Floor;
                tile.UpdateVisual();
            }
        }

        // Remove shape from inventory
        inventory.ConsumeShape(shape);

        UpdatePreview();
    }

    /// <summary>
    /// Updates the visual preview of the shape at the intended location.
    /// Color-coded based on placement validity.
    /// </summary>
    void UpdatePreview()
    {
        if (player == null || inventory.shapeInventory.Count == 0) return;

        ClearPreview();

        if (!inventory.HasShape(CurrentShapeEntry.shapeData))
            return;

        var shape = CurrentShapeEntry.shapeData;
        var origin = player.GridPosition + player.FacingDirection;
        Vector2Int[] rotatedOffsets = GetRotatedOffsets(shape.tileOffsets, currentRotation);

        bool valid = gridManager.CanPlaceShape(origin, rotatedOffsets);

        foreach (var offset in rotatedOffsets)
        {
            Vector2Int pos = origin + offset;
            GameObject tile = Instantiate(previewTilePrefab, new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            var sr = tile.GetComponent<SpriteRenderer>();

            // Highlight overlaps differently
            sr.color = (pos == player.GridPosition)
                ? overPlayerColor
                : (valid ? validColor : invalidColor);

            previewTiles.Add(tile);
        }
    }

    /// <summary>
    /// Destroys all preview tiles in the scene.
    /// </summary>
    void ClearPreview()
    {
        foreach (var tile in previewTiles)
        {
            if (tile != null) Destroy(tile);
        }
        previewTiles.Clear();
    }

    /// <summary>
    /// Returns rotated shape offsets based on the number of 90° clockwise rotations.
    /// </summary>
    Vector2Int[] GetRotatedOffsets(Vector2Int[] original, int rotationStepsCW)
    {
        Vector2Int[] result = new Vector2Int[original.Length];
        for (int i = 0; i < original.Length; i++)
        {
            Vector2Int p = original[i];
            for (int r = 0; r < rotationStepsCW; r++)
                p = new Vector2Int(-p.y, p.x); // 90° clockwise rotation

            result[i] = p;
        }
        return result;
    }
}

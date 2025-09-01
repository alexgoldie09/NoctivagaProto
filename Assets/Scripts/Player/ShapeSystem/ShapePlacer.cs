using UnityEngine;
using System.Collections.Generic;

public class ShapePlacer : MonoBehaviour
{
    [Header("Settings")]
    public GameObject previewTilePrefab;
    public Color validColor = new Color(0f, 1f, 0f, 0.5f);
    public Color invalidColor = new Color(1f, 0f, 0f, 0.5f);
    public Color overPlayerColor = new Color(0f, 1f, 1f, 0.5f); // Debug: cyan

    [Header("Shape Inventory")]
    public List<ShapeInventoryEntry> inventory;

    private int currentShapeIndex = 0;
    private int currentRotation = 0;
    private PlayerController player;
    private List<GameObject> previewTiles = new List<GameObject>();

    private ShapeInventoryEntry CurrentShapeEntry => inventory[currentShapeIndex];
    public int CurrentIndex => currentShapeIndex;

    void Start()
    {
        player = GetComponentInParent<PlayerController>();
        if (player == null)
        {
            Debug.LogError("ShapePlacer must be a child of an object with PlayerController.");
            return;
        }

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
            ClearPreview(); // Ensure no ghosts remain
        }
    }
    
    private void HandleToggleInput()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            Utilities.IsPlacementModeActive = !Utilities.IsPlacementModeActive;
        }
    }

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
            currentRotation = (currentRotation + 1) % 4;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryPlaceCurrentShape();
        }
    }

    private void CycleShape(int direction)
    {
        int tries = 0;
        do
        {
            currentShapeIndex = (currentShapeIndex + direction + inventory.Count) % inventory.Count;
            tries++;
        } while (CurrentShapeEntry.count <= 0 && tries < inventory.Count);

        //Debug.Log($"Selected shape: {CurrentShapeEntry.shapeData.name} (x{CurrentShapeEntry.count})");
    }

    private void TryPlaceCurrentShape()
    {
        if (CurrentShapeEntry.count <= 0)
        {
            //Debug.LogWarning($"No more uses left for {CurrentShapeEntry.shapeData.name}");
            return;
        }

        var shape = CurrentShapeEntry.shapeData;
        var origin = player.GridPosition + player.FacingDirection;
        Vector2Int[] rotatedOffsets = GetRotatedOffsets(shape.tileOffsets, currentRotation);

        if (!GridManager.Instance.CanPlaceShape(origin, rotatedOffsets))
        {
            //Debug.Log("Invalid placement location.");
            return;
        }

        foreach (var offset in rotatedOffsets)
        {
            Vector2Int pos = origin + offset;
            GridTile tile = GridManager.Instance.GetTileAt(pos.x, pos.y);
            if (tile != null)
            {
                tile.tileType = TileType.Floor;
                tile.UpdateVisual();
            }
        }

        CurrentShapeEntry.count--;
        //Debug.Log($"Placed {shape.name}. Remaining: {CurrentShapeEntry.count}");

        UpdatePreview();
    }

    void UpdatePreview()
    {
        if (player == null) return;

        ClearPreview();
        
        // Don't show preview if shape has no uses left
        if (CurrentShapeEntry.count <= 0)
        {
            return;
        }

        var shape = CurrentShapeEntry.shapeData;
        var origin = player.GridPosition + player.FacingDirection;
        Vector2Int[] rotatedOffsets = GetRotatedOffsets(shape.tileOffsets, currentRotation);

        bool valid = GridManager.Instance.CanPlaceShape(origin, rotatedOffsets);

        foreach (var offset in rotatedOffsets)
        {
            Vector2Int pos = origin + offset;
            GameObject tile = Instantiate(previewTilePrefab, new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            var sr = tile.GetComponent<SpriteRenderer>();

            if (pos == player.GridPosition)
            {
                sr.color = overPlayerColor; // Debugging if preview overlaps player
            }
            else
            {
                sr.color = valid ? validColor : invalidColor;
            }

            previewTiles.Add(tile);
        }
    }

    void ClearPreview()
    {
        foreach (var tile in previewTiles)
        {
            if (tile != null) Destroy(tile);
        }
        previewTiles.Clear();
    }

    Vector2Int[] GetRotatedOffsets(Vector2Int[] original, int rotationStepsCW)
    {
        Vector2Int[] result = new Vector2Int[original.Length];
        for (int i = 0; i < original.Length; i++)
        {
            Vector2Int p = original[i];
            for (int r = 0; r < rotationStepsCW; r++)
            {
                p = new Vector2Int(-p.y, p.x); // 90° CW rotation
            }
            result[i] = p;
        }
        return result;
    }
}

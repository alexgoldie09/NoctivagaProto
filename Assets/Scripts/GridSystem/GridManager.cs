using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton manager for creating, storing, and accessing GridTiles.
/// Supports both runtime-generated grids (from MapData) and editor-placed grids.
/// </summary>
public class GridManager : MonoBehaviour 
{
    public static GridManager Instance { get; private set; }

    [Header("Tile Reference")]
    public GameObject tilePrefab;             // Tile prefab used in runtime generation
    public Sprite floorSprite;
    public Sprite voidSprite;
    public Sprite wallSprite;
    public Sprite gateSprite;
    public Sprite startSprite;               

    [Header("Runtime Map Loading")]
    public bool generateFromMapData = false; // Toggle between mapData or editor-placed
    public MapData mapData;                  // ScriptableObject-based map representation

    [Header("Obstacle Prefab Registry")]
    public ObstacleRegistry obstacleRegistry; // Cached obstacles

    private GridTile[,] gridTiles;

    // Width/height fallback to editor grid size if not using mapData
    public int width => generateFromMapData && mapData != null ? mapData.width : gridTiles?.GetLength(0) ?? 0;
    public int height => generateFromMapData && mapData != null ? mapData.height : gridTiles?.GetLength(1) ?? 0;

    // ─────────────────────────────────────────────────────────────────────────────

    private void Awake() 
    {
        // Enforce Singleton pattern
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Choose between runtime or editor-based grid setup
        if (generateFromMapData && mapData != null) 
        {
            GenerateGridFromMapData();
        } 
        else 
        {
            CacheEditorPlacedTiles(); // Grab tiles manually placed in scene
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates grid based on MapData asset (used for runtime loading).
    /// </summary>
    private void GenerateGridFromMapData() 
    {
        gridTiles = new GridTile[mapData.width, mapData.height];

        // Temporary store for obstacles + metadata
        List<(ObstacleBase obs, Dictionary<string, string> data)> metadataQueue = new();

        for (int y = 0; y < mapData.height; y++) 
        {
            for (int x = 0; x < mapData.width; x++) 
            {
                MapData.TileInfo info = mapData.GetTileInfo(x, y);

                // Instantiate tile at grid position
                GameObject tileObj = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, transform);
                GridTile tile = tileObj.GetComponent<GridTile>();
                tile.gridPos = new Vector2Int(x, y);

                // Set base tile data
                tile.tileType = info.type;
                tile.floorSprite = floorSprite;
                tile.voidSprite = voidSprite;
                tile.wallSprite = wallSprite;
                tile.gateSprite = gateSprite;
                tile.startSprite = startSprite;

                tile.UpdateVisual();
                gridTiles[x, y] = tile;

                // Restore gate keys
                if (tile.tileType == TileType.Gate && info.gateRequiredKeys != null)
                {
                    tile.requiredKeyIds = info.gateRequiredKeys;
                }

                // Restore obstacle (if any)
                if (!string.IsNullOrEmpty(info.obstacleType))
                {
                    GameObject prefab = obstacleRegistry?.GetPrefab(info.obstacleType);
                    if (prefab != null)
                    {
                        GameObject obsObj = Instantiate(prefab, tileObj.transform);
                        obsObj.transform.localPosition = Vector3.zero;

                        ObstacleBase obs = obsObj.GetComponent<ObstacleBase>();
                        if (obs != null)
                        {
                            tile.SetObstacle(obs);

                            // Defer SetMetadata until AFTER grid is fully built
                            if (info.obstacleMetadata != null && info.obstacleMetadata.Count > 0)
                            {
                                metadataQueue.Add((obs, info.obstacleMetadata));
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Prefab '{info.obstacleType}' has no ObstacleBase component.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"No registered prefab found for ID '{info.obstacleType}'");
                    }
                }
            }
        }

        // Post-pass: Apply metadata now that all tiles are registered
        foreach (var (obs, data) in metadataQueue)
        {
            obs.SetMetadata(data);
        }

        Debug.Log($"Map imported successfully at runtime from '{mapData.name}'");
    }

    /// <summary>
    /// Caches manually-placed tiles in editor (for drag-and-drop level design).
    /// </summary>
    private void CacheEditorPlacedTiles() 
    {
        GridTile[] foundTiles = GetComponentsInChildren<GridTile>(true); // true = include inactive

        int maxX = 0;
        int maxY = 0;

        // Calculate bounds based on tile positions
        foreach (var tile in foundTiles) 
        {
            maxX = Mathf.Max(maxX, tile.gridPos.x);
            maxY = Mathf.Max(maxY, tile.gridPos.y);
        }

        gridTiles = new GridTile[maxX + 1, maxY + 1];

        foreach (var tile in foundTiles) 
        {
            Vector2Int pos = tile.gridPos;
            gridTiles[pos.x, pos.y] = tile;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary> Check if given coordinate is within grid bounds. </summary>
    public bool IsInBounds(int x, int y) 
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    /// <summary> Returns true if tile at (x, y) exists and is walkable. </summary>
    public bool IsWalkable(int x, int y) 
    {
        if (!IsInBounds(x, y)) 
            return false;
        return gridTiles[x, y] != null && gridTiles[x, y].IsWalkable();
    }

    /// <summary> Get the tile at grid position (x, y). </summary>
    public GridTile GetTileAt(int x, int y) 
    {
        return IsInBounds(x, y) ? gridTiles[x, y] : null;
    }
    
    /// <summary>
    /// Finds and returns the first tile marked as Start.
    /// Returns null if no Start tile is found.
    /// </summary>
    public GridTile GetStartTile()
    {
        foreach (var tile in AllTiles)
        {
            if (tile.tileType == TileType.Start)
                return tile;
        }

        // Debug.LogWarning("No Start tile found in grid.");
        return null;
    }

    /// <summary>
    /// Check if a shape can be placed at the given origin with the given offsets.
    /// </summary>
    public bool CanPlaceShape(Vector2Int origin, Vector2Int[] offsets) 
    {
        foreach (var offset in offsets) 
        {
            Vector2Int checkPos = origin + offset;

            if (!IsInBounds(checkPos.x, checkPos.y))
                return false;

            GridTile tile = gridTiles[checkPos.x, checkPos.y];
            if (tile == null || tile.BlocksShapePlacement())
                return false;
        }
        return true;
    }

    /// <summary> Enumerates all non-null tiles in the grid. </summary>
    public IEnumerable<GridTile> AllTiles
    {
        get
        {
            foreach (var tile in gridTiles)
            {
                if (tile != null)
                    yield return tile;
            }
        }
    }
}

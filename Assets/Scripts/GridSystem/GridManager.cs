using UnityEngine;

public class GridManager : MonoBehaviour 
{
    public static GridManager Instance { get; private set; }

    [Header("Tile Reference")]
    public GameObject tilePrefab; // Used only for runtime grid generation
    public Sprite floorSprite;
    public Sprite voidSprite;
    public Sprite wallSprite;

    [Header("Runtime Map Loading")]
    public bool generateFromMapData = false; // Toggle for runtime vs editor-drawn
    public MapData mapData; // ScriptableObject support

    private GridTile[,] gridTiles;

    public int width => generateFromMapData && mapData != null ? mapData.width : gridTiles?.GetLength(0) ?? 0;
    public int height => generateFromMapData && mapData != null ? mapData.height : gridTiles?.GetLength(1) ?? 0;

    // ─────────────────────────────────────────────────────────────────────────────

    private void Awake() 
    {
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (generateFromMapData && mapData != null) 
        {
            GenerateGridFromMapData();
        } else 
        {
            // Assuming gridTiles already placed in editor using spawner or manually
            CacheEditorPlacedTiles();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    private void GenerateGridFromMapData() 
    {
        gridTiles = new GridTile[mapData.width, mapData.height];

        for (int y = 0; y < mapData.height; y++) 
        {
            for (int x = 0; x < mapData.width; x++) 
            {
                TileType type = mapData.GetTile(x, y);

                GameObject tileObj = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, transform);
                GridTile tile = tileObj.GetComponent<GridTile>();
                tile.gridPos = new Vector2Int(x, y);
                tile.tileType = type;

                tile.floorSprite = floorSprite;
                tile.voidSprite = voidSprite;
                tile.wallSprite = wallSprite;
                tile.UpdateVisual();

                gridTiles[x, y] = tile;
            }
        }
    }

    private void CacheEditorPlacedTiles() 
    {
        GridTile[] foundTiles = FindObjectsByType<GridTile>(FindObjectsSortMode.None);

        int maxX = 0;
        int maxY = 0;

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

    public bool IsInBounds(int x, int y) 
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public bool IsWalkable(int x, int y) 
    {
        if (!IsInBounds(x, y)) 
            return false;
        return gridTiles[x, y] != null && gridTiles[x, y].IsWalkable();
    }

    public GridTile GetTileAt(int x, int y) 
    {
        return IsInBounds(x, y) ? gridTiles[x, y] : null;
    }
    
    public bool CanPlaceShape(Vector2Int origin, Vector2Int[] offsets) 
    {
        foreach (var offset in offsets) 
        {
            Vector2Int checkPos = origin + offset;

            if (!IsInBounds(checkPos.x, checkPos.y))
                return false;

            GridTile tile = gridTiles[checkPos.x, checkPos.y];
            if (tile == null || tile.tileType != TileType.Void)
                return false;
        }
        return true;
    }
    
    public Vector2Int? FindSpawnPoint() 
    {
        if (generateFromMapData && mapData != null) return mapData.FindSpawnPoint();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) 
            {
                GridTile tile = GetTileAt(x, y);
                if (tile != null && tile.tileType == TileType.Floor) 
                    return new Vector2Int(x, y); // fallback
            }
        }

        Debug.LogWarning("No valid spawn point found.");
        return null;
    }

    public GridTile[,] GetGridArray() => gridTiles;
}

using UnityEngine;

public class GridManager : MonoBehaviour 
{
    public int width = 10;
    public int height = 10;
    public GameObject tilePrefab; // Prefab with GridTile + SpriteRenderer

    private GridTile[,] gridTiles;

    void Awake() 
    {
        GenerateGrid();
    }

    void GenerateGrid() 
    {
        gridTiles = new GridTile[width, height];

        for (int x = 0; x < width; x++) 
        {
            for (int y = 0; y < height; y++) 
            {
                GameObject tileObj = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, transform);
                GridTile tile = tileObj.GetComponent<GridTile>();

                TileType type = TileType.Floor; // TODO: add map data later
                if ((x == 3 && y == 3) || Random.value < 0.1f) 
                    type = TileType.Void;
                if ((x == 0 || y == 0 || x == width - 1 || y == height - 1)) 
                    type = TileType.Wall;

                tile.Init(new Vector2Int(x, y), type);
                gridTiles[x, y] = tile;
            }
        }
    }

    public bool IsWalkable(int x, int y) 
    {
        if (!IsInBounds(x, y)) 
            return false;
        
        return gridTiles[x, y].IsWalkable();
    }

    public bool IsInBounds(int x, int y) 
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public GridTile GetTileAt(int x, int y) 
    {
        return IsInBounds(x, y) ? gridTiles[x, y] : null;
    }
}
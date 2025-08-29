using UnityEngine;

[CreateAssetMenu(fileName = "NewMapData", menuName = "Noctivaga/Map Data")]
public class MapData : ScriptableObject 
{
    public int width;
    public int height;
    public TileType[] tiles; // Flattened grid data

    public TileType GetTile(int x, int y) 
    {
        return tiles[y * width + x];
    }

    public void SetTile(int x, int y, TileType type) 
    {
        tiles[y * width + x] = type;
    }

    public Vector2Int? FindSpawnPoint() 
    {
        for (int y = 0; y < height; y++) 
        {
            for (int x = 0; x < width; x++) 
            {
                if (GetTile(x, y) == TileType.Floor) 
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        return null;
    }
}
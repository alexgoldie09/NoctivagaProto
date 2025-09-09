using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// ScriptableObject that stores all serialized tile and obstacle information
/// used to regenerate the map layout during runtime or from editor.
/// </summary>
[CreateAssetMenu(menuName = "Grid/Map Data")]
public class MapData : ScriptableObject
{
    public int width;
    public int height;


    [System.Serializable]
    public class TileInfo
    {
        public TileType type; // Stores tile type like Floor, Void, Gate, Start
        public string obstacleType; // Stores string ID of obstacle prefab
        public string[] gateRequiredKeys; // Gate requirement info


        // Generic obstacle metadata for storing things like mirror direction, lever targets, etc.
        public SerializableDictionary<string, string> obstacleMetadata;
    }
    
    [SerializeField]
    public TileInfo[] tileInfos; // Flattened grid representation

    /// <summary>
    /// Returns TileInfo at (x, y)
    /// </summary>
    public TileInfo GetTileInfo(int x, int y)
    {
        return tileInfos[y * width + x];
    }
    
    /// <summary>
    /// Updates TileInfo at (x, y)
    /// </summary>
    public void SetTileInfo(int x, int y, TileInfo info)
    {
        tileInfos[y * width + x] = info;
    }
    
    /// <summary>
    /// Initializes the grid with default Floor-type tiles.
    /// </summary>
    public void InitializeTiles()
    {
        tileInfos = new TileInfo[width * height];
        for (int i = 0; i < tileInfos.Length; i++)
        {
            tileInfos[i] = new TileInfo { type = TileType.Floor, obstacleMetadata = new SerializableDictionary<string, string>() };
        }
    }
    
    /// <summary>
    /// Finds all tile positions of the specified TileType.
    /// </summary>
    public List<Vector2Int> FindTilesOfType(TileType type)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (GetTileInfo(x, y).type == type)
                {
                    positions.Add(new Vector2Int(x, y));
                }
            }
        }
        return positions;
    }


    /// <summary>
    /// Returns how many tiles of the given type exist in the map.
    /// </summary>
    public int CountTilesOfType(TileType type)
    {
        int count = 0;
        for (int i = 0; i < tileInfos.Length; i++)
        {
            if (tileInfos[i].type == type)
            {
                count++;
            }
        }
        return count;
    }
}
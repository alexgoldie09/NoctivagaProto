using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Types of tiles on the grid.
/// </summary>
public enum TileType 
{
    Floor,
    Void,
    Wall,
    Gate,
    Start
}

/// <summary>
/// Represents a single tile on the grid. Responsible for its visual,
/// behavior, and interactions with obstacles and players.
/// </summary>
public class GridTile : MonoBehaviour 
{
    public Vector2Int gridPos;                        // Grid location
    public TileType tileType;                         // Type of tile
    public string[] requiredKeyIds;                   // Keys required if this is a Gate

    [Header("Tile Sprites")]
    public Sprite floorSprite;
    public Sprite voidSprite;
    public Sprite wallSprite;
    public Sprite gateSprite;
    public Sprite startSprite;
    
    [Header("Obstacle (Optional)")]
    public ObstacleBase obstacle;                     // Optional obstacle reference
    public bool isBeamBlocking = false;               // True if dynamic beam makes this tile unwalkable
    
    private SpriteRenderer sr;

    void Awake() 
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }
    

    /// <summary>
    /// Updates the tile's sprite based on its type.
    /// </summary>
    public void UpdateVisual() 
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        switch (tileType) 
        {
            case TileType.Floor: sr.sprite = floorSprite; break;
            case TileType.Void:  sr.sprite = voidSprite;  break;
            case TileType.Wall:  sr.sprite = wallSprite;  break;
            case TileType.Gate:  sr.sprite = gateSprite;  break;
            case TileType.Start: sr.sprite = startSprite; break;
        }
    }

    /// <summary>
    /// Determines if the tile can be walked on by the player.
    /// </summary>
    public bool IsWalkable()
    {
        if (tileType == TileType.Void || tileType == TileType.Wall || isBeamBlocking)
            return false;

        if (obstacle != null && obstacle.BlocksMovement())
            return false;

        return true;
    }
    
    /// <summary>
    /// Determines if the tile blocks shape placement.
    /// Only Void tiles without beam or obstacles allow placement.
    /// </summary>
    public bool BlocksShapePlacement()
    {
        if (tileType != TileType.Void)
            return true;

        if (isBeamBlocking)
            return true;

        if (obstacle != null && obstacle.BlocksShapePlacement())
            return true;

        return false;
    }
    
    /// <summary>
    /// Updates this tile's obstacle.
    /// </summary>
    public void SetObstacle(ObstacleBase obs)
    {
        obstacle = obs;
    }

    /// <summary>
    /// Returns true if this tile has an obstacle.
    /// </summary>
    public bool HasObstacle(out ObstacleBase obs)
    {
        obs = obstacle;
        return obstacle != null;
    }

    /// <summary>
    /// Updates this tile's type and refreshes its visual.
    /// </summary>
    public void SetTileType(TileType newType) 
    {
        tileType = newType;
        UpdateVisual();
    }

    /// <summary>
    /// Checks if the player has enough keys to unlock this tile if it's a Gate.
    /// </summary>
    public bool CanUnlock(PlayerInventory inventory)
    {
        if (tileType != TileType.Gate) return false;

        foreach (string key in requiredKeyIds)
        {
            if (inventory.GetKeyCount(key) <= 0)
                return false;
        }

        return true;
    }
    
    /// <summary>
    /// Unlocks a Gate using keys from the inventory and turns it into a Floor.
    /// </summary>
    public void UnlockWithKeys(PlayerInventory inventory)
    {
        if (tileType != TileType.Gate) return;

        foreach (string key in requiredKeyIds)
        {
            inventory.UseKey(key);
        }

        SetTileType(TileType.Floor);
    }

#if UNITY_EDITOR
    void OnValidate() 
    {
        UpdateVisual();
    }

    void OnDrawGizmos() 
    {
        Handles.Label(transform.position + Vector3.up * 0.3f, tileType.ToString());
    }
#endif
}

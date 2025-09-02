using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum TileType 
{
    Floor,
    Void,
    Wall,
    Gate
}

public class GridTile : MonoBehaviour 
{
    public Vector2Int gridPos;
    public TileType tileType;
    public string[] requiredKeyIds;

    [Header("Tile Sprites")]
    public Sprite floorSprite;
    public Sprite voidSprite;
    public Sprite wallSprite;
    public Sprite gateSprite;

    private SpriteRenderer sr;

    void Awake() 
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    public void Init(Vector2Int pos, TileType type) 
    {
        gridPos = pos;
        tileType = type;
        sr = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    public void UpdateVisual() 
    {
        if (sr == null)
        {
            sr = GetComponent<SpriteRenderer>();
        }
        switch (tileType) 
        {
            case TileType.Floor: sr.sprite = floorSprite; break;
            case TileType.Void: sr.sprite = voidSprite; break;
            case TileType.Wall: sr.sprite = wallSprite; break;
            case TileType.Gate: sr.sprite = gateSprite; break;
        }
    }

    public bool IsWalkable() 
    {
        return tileType == TileType.Floor;
    }

    public void SetTileType(TileType newType) 
    {
        tileType = newType;
        UpdateVisual();
    }

    public bool CanUnlock(PlayerInventory inventory)
    {
        if (tileType == TileType.Gate)
        {
            foreach (string key in requiredKeyIds)
            {
                if (inventory.GetKeyCount(key) <= 0)
                    return false;
            }

            return true;
        }
        return false;
    }
    
    public void UnlockWithKeys(PlayerInventory inventory)
    {
        if (tileType != TileType.Gate) return;

        foreach (string key in requiredKeyIds)
        {
            inventory.UseKey(key);
        }

        SetTileType(TileType.Floor);
        Debug.Log($"Gate at {gridPos} unlocked using keys.");
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

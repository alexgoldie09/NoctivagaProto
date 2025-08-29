using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum TileType 
{
    Floor,
    Void,
    Wall
}

public class GridTile : MonoBehaviour 
{
    public Vector2Int gridPos;
    public TileType tileType;

    [Header("Tile Sprites")]
    public Sprite floorSprite;
    public Sprite voidSprite;
    public Sprite wallSprite;

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

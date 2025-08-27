using UnityEngine;

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
    }

    public void Init(Vector2Int pos, TileType type) 
    {
        gridPos = pos;
        tileType = type;
        UpdateVisual();
    }

    public void UpdateVisual() 
    {
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
}

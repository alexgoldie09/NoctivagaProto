using UnityEngine;
using UnityEngine.Tilemaps;

public enum TileKind
{
    Floor,
    Void,
    Start,
    Wall,
    Gate
}

public enum EnterEffect
{
    None,
    ResetToStart
}

[CreateAssetMenu(menuName = "Tiles/Game Tile")]
public class GameTile : Tile
{
    [Header("Gameplay")]
    public TileKind kind = TileKind.Floor;

    [Tooltip("If true, the player can enter this tile unless blocked by obstacles/beams/etc.")]
    public bool walkableByDefault = true;

    [Tooltip("What happens when the player enters this tile.")]
    public EnterEffect enterEffect = EnterEffect.None;

    [Header("Placement / Hazards (optional)")]
    public bool blocksShapePlacement = false;
    public bool blocksBeam = false; // If you want tiles that inherently block beams
}

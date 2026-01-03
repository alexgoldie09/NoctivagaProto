using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// High-level classification for grid tiles.
/// </summary>
public enum TileKind
{
    Floor,
    Void,
    Start,
    Wall,
    Gate
}

/// <summary>
/// Effect applied when the player enters a tile.
/// </summary>
public enum EnterEffect
{
    None,
    ResetToStart
}

/// <summary>
/// Tile asset that stores gameplay metadata for grid interactions.
/// </summary>
[CreateAssetMenu(menuName = "Tiles/Game Tile")]
public class GameTile : Tile
{
    [Header("Gameplay")]
    public TileKind kind = TileKind.Floor;

    [Tooltip("If true, the player can enter this tile unless blocked by obstacles/beams/etc.")]
    public bool walkableByDefault = true;

    [Tooltip("What happens when the player enters this tile.")]
    public EnterEffect enterEffect = EnterEffect.None;
    
    [Header("Gate (only used if kind == Gate)")]
    [Tooltip("Key ID required to unlock this gate, e.g. 'red', 'blue', 'gold'. Empty means 'no key required'.")]
    public string gateKeyID = "red";

    [Tooltip("If true, using the gate consumes one key from the inventory.")]
    public bool consumesKey = true;
}

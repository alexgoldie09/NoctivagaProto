using UnityEngine;

/// <summary>
/// Represents a predefined Tetris shape configuration used in placement mechanics.
/// Holds tile offsets relative to an origin and a preview sprite for UI display.
/// This data is used by the TetrisManager and TetrisPlacer for shape instantiation.
/// </summary>
[CreateAssetMenu(menuName = "Noctivaga/Tetris Shape", fileName = "NewTetrisShape")]
public class TetrisShapeData : ScriptableObject 
{
    [Header("Shape Identification")]
    public string shapeName; // A name identifier for the shape.

    [Header("Tile Configuration")]
    [Tooltip("Offsets from shape origin tile (0,0)")]
    public Vector2Int[] tileOffsets = new Vector2Int[4]; // These are applied relative to a central pivot (origin) during placement.

    [Header("UI Preview")]
    public Sprite previewSprite; // A sprite used to display this shape in the shape selection UI.
}
/// <summary>
/// Serializable data class used to store a reference to a Tetris shape
/// and how many times it can still be placed. This class is primarily 
/// used by shape managers, placement systems, or inventories to manage 
/// shape availability.
/// </summary>
[System.Serializable]
public class ShapeInventoryEntry
{
    public TetrisShapeData shapeData; // The Tetris shape definition.
    public int count; // Number of available placements left for this shape type.
}
using UnityEngine;

[CreateAssetMenu(menuName = "Noctivaga/Tetris Shape", fileName = "NewTetrisShape")]
public class TetrisShapeData : ScriptableObject 
{
    public string shapeName;

    [Tooltip("Offsets from shape origin tile (0,0)")]
    public Vector2Int[] tileOffsets = new Vector2Int[4];

    public Sprite previewSprite;
}
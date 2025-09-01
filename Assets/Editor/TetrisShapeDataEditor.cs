using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TetrisShapeData))]
public class TetrisShapeDataEditor : Editor 
{
    private const int cellSize = 20;
    private const int gridPadding = 5;

    public override void OnInspectorGUI() 
    {
        base.OnInspectorGUI(); // Draw default fields

        TetrisShapeData shape = (TetrisShapeData)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Shape Preview", EditorStyles.boldLabel);

        Rect previewRect = GUILayoutUtility.GetRect(100, 100);
        DrawShapePreview(previewRect, shape);
    }

    private void DrawShapePreview(Rect rect, TetrisShapeData shape) 
    {
        Vector2 center = rect.center;

        Handles.BeginGUI();

        foreach (Vector2Int offset in shape.tileOffsets) 
        {
            Vector2 pos = center + new Vector2(offset.x, -offset.y) * cellSize;
            Rect cellRect = new Rect(pos.x - cellSize / 2, pos.y - cellSize / 2, cellSize, cellSize);
            EditorGUI.DrawRect(cellRect, new Color(0.2f, 0.6f, 1f, 1f)); // blue cell
            Handles.DrawSolidRectangleWithOutline(cellRect, new Color(0, 0, 0, 0), Color.black);
        }

        // Draw origin tile at (0,0)
        Rect originRect = new Rect(center.x - cellSize / 2, center.y - cellSize / 2, cellSize, cellSize);
        Handles.DrawSolidRectangleWithOutline(originRect, new Color(1f, 1f, 0f, 0.5f), Color.yellow); // origin highlight

        Handles.EndGUI();
    }
}
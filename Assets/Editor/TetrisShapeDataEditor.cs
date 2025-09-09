using UnityEngine;
using UnityEditor;
/// <summary>
/// Custom Unity Editor for TetrisShapeData.
/// Draws a visual preview of the shape's tile offsets in a grid view,
/// helping designers visualize Tetris shapes in the inspector.
/// </summary>
[CustomEditor(typeof(TetrisShapeData))]
public class TetrisShapeDataEditor : Editor
{
    private const int cellSize = 20;
    private const int gridPadding = 5;

    /// <summary>
    /// Overrides the default Inspector GUI to include a shape preview.
    /// </summary>
    public override void OnInspectorGUI() 
    {
        base.OnInspectorGUI(); // Draw default serialized fields

        TetrisShapeData shape = (TetrisShapeData)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Shape Preview", EditorStyles.boldLabel);

        Rect previewRect = GUILayoutUtility.GetRect(100, 100); // Allocates space for drawing
        DrawShapePreview(previewRect, shape); // Call preview renderer
    }

    /// <summary>
    /// Draws a mini grid preview of the Tetris shape using Handles and EditorGUI.
    /// The shape is centered in the preview area.
    /// </summary>
    /// <param name="rect">The preview rectangle bounds in GUI space.</param>
    /// <param name="shape">The shape data to render.</param>
    private void DrawShapePreview(Rect rect, TetrisShapeData shape) 
    {
        Vector2 center = rect.center;

        Handles.BeginGUI();

        foreach (Vector2Int offset in shape.tileOffsets) 
        {
            // Convert tile offset into screen space
            Vector2 pos = center + new Vector2(offset.x, -offset.y) * cellSize;
            Rect cellRect = new Rect(pos.x - cellSize / 2, pos.y - cellSize / 2, cellSize, cellSize);

            // Draw the tile (light blue box)
            EditorGUI.DrawRect(cellRect, new Color(0.2f, 0.6f, 1f, 1f));
            Handles.DrawSolidRectangleWithOutline(cellRect, new Color(0, 0, 0, 0), Color.black);
        }

        // Draw origin cell in yellow (for clarity)
        Rect originRect = new Rect(center.x - cellSize / 2, center.y - cellSize / 2, cellSize, cellSize);
        Handles.DrawSolidRectangleWithOutline(originRect, new Color(1f, 1f, 0f, 0.5f), Color.yellow);

        Handles.EndGUI();
    }
}

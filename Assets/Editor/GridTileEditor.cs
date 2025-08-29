using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridTile))]
public class GridTileEditor : Editor 
{
    public override void OnInspectorGUI() 
    {
        DrawDefaultInspector();

        GridTile tile = (GridTile)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tile Painter", EditorStyles.boldLabel);

        if (GUILayout.Button("Set to Floor")) 
        {
            tile.tileType = TileType.Floor;
            tile.UpdateVisual();
        }

        if (GUILayout.Button("Set to Void")) 
        {
            tile.tileType = TileType.Void;
            tile.UpdateVisual();
        }

        if (GUILayout.Button("Set to Wall")) 
        {
            tile.tileType = TileType.Wall;
            tile.UpdateVisual();
        }

        if (GUI.changed) 
        {
            EditorUtility.SetDirty(tile);
        }
    }
}
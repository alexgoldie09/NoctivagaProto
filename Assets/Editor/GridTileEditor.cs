using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridTile))]
public class GridTileEditor : Editor 
{
    SerializedProperty tileTypeProp;
    SerializedProperty requiredKeyIDsProp;

    void OnEnable()
    {
        tileTypeProp = serializedObject.FindProperty("tileType");
        requiredKeyIDsProp = serializedObject.FindProperty("requiredKeyIds");
    }

    public override void OnInspectorGUI() 
    {
        serializedObject.Update();

        DrawDefaultInspectorExcept("tileType", "requiredKeyIds");

        EditorGUILayout.PropertyField(tileTypeProp);

        // Show requiredKeyIDs only if TileType is Gate
        if ((TileType)tileTypeProp.enumValueIndex == TileType.Gate)
        {
            EditorGUILayout.PropertyField(requiredKeyIDsProp, true);
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tile Painter", EditorStyles.boldLabel);

        GridTile tile = (GridTile)target;

        if (GUILayout.Button("Set to Floor")) 
        {
            tile.SetTileType(TileType.Floor);
        }

        if (GUILayout.Button("Set to Void")) 
        {
            tile.SetTileType(TileType.Void);
        }

        if (GUILayout.Button("Set to Wall")) 
        {
            tile.SetTileType(TileType.Wall);
        }

        if (GUILayout.Button("Set to Gate")) 
        {
            tile.SetTileType(TileType.Gate);
        }

        if (GUI.changed) 
        {
            EditorUtility.SetDirty(tile);
        }
    }

    // Helper to skip specific default fields
    void DrawDefaultInspectorExcept(params string[] excludedProps)
    {
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            bool excluded = false;
            foreach (var name in excludedProps)
            {
                if (prop.name == name)
                {
                    excluded = true;
                    break;
                }
            }

            if (!excluded)
            {
                EditorGUILayout.PropertyField(prop, true);
            }
        }
    }
}

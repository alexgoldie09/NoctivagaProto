// using UnityEditor;
// using UnityEngine;
//
// /// <summary>
// /// Custom editor for GridTile to provide an enhanced Inspector UI
// /// for editing tile types and gate requirements directly in the Unity Editor.
// /// </summary>
// [CustomEditor(typeof(GridTile))]
// public class GridTileEditor : Editor 
// {
//     SerializedProperty tileTypeProp;
//     SerializedProperty requiredKeyIDsProp;
//
//     // Called when the editor is enabled — binds serialized properties.
//     void OnEnable()
//     {
//         tileTypeProp = serializedObject.FindProperty("tileType");
//         requiredKeyIDsProp = serializedObject.FindProperty("requiredKeyIds");
//     }
//
//     public override void OnInspectorGUI() 
//     {
//         serializedObject.Update();
//
//         // Draw all other serialized properties except the custom-handled ones
//         DrawDefaultInspectorExcept("tileType", "requiredKeyIds");
//
//         // Manual control of tileType
//         EditorGUILayout.PropertyField(tileTypeProp);
//
//         // Show requiredKeyIDs only if TileType is Gate
//         if ((TileType)tileTypeProp.enumValueIndex == TileType.Gate)
//         {
//             EditorGUILayout.PropertyField(requiredKeyIDsProp, true);
//         }
//
//         serializedObject.ApplyModifiedProperties();
//
//         // Painter Section UI
//         EditorGUILayout.Space();
//         EditorGUILayout.LabelField("Tile Painter", EditorStyles.boldLabel);
//
//         GridTile tile = (GridTile)target;
//
//         // Painter buttons for quickly setting tile types
//         if (GUILayout.Button("Set to Floor")) 
//             tile.SetTileType(TileType.Floor);
//
//         if (GUILayout.Button("Set to Void")) 
//             tile.SetTileType(TileType.Void);
//
//         if (GUILayout.Button("Set to Wall")) 
//             tile.SetTileType(TileType.Wall);
//
//         if (GUILayout.Button("Set to Gate")) 
//             tile.SetTileType(TileType.Gate);
//
//         if (GUILayout.Button("Set to Start")) 
//             tile.SetTileType(TileType.Start); // NEW start tile button
//
//         // Ensure Unity registers the tile as modified
//         if (GUI.changed) 
//             EditorUtility.SetDirty(tile);
//     }
//
//     /// <summary>
//     /// Draws all serialized fields except specified exclusions.
//     /// Used to override specific fields (like tileType).
//     /// </summary>
//     void DrawDefaultInspectorExcept(params string[] excludedProps)
//     {
//         SerializedProperty prop = serializedObject.GetIterator();
//         bool enterChildren = true;
//
//         while (prop.NextVisible(enterChildren))
//         {
//             enterChildren = false;
//
//             bool excluded = false;
//             foreach (var name in excludedProps)
//             {
//                 if (prop.name == name)
//                 {
//                     excluded = true;
//                     break;
//                 }
//             }
//
//             if (!excluded)
//             {
//                 EditorGUILayout.PropertyField(prop, true);
//             }
//         }
//     }
// }

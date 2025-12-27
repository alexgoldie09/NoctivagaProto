// using UnityEngine;
// using UnityEditor;
// using System.Collections.Generic;
//
// /// <summary>
// /// Editor utility that exports GridTile layout from the scene into a MapData ScriptableObject.
// /// Stores tile type, gate key requirements, obstacle prefab ID, and obstacle metadata.
// /// </summary>
// public class MapDataExporter : MonoBehaviour
// {
//     [MenuItem("Noctivaga/Export Map From Scene")]
//     public static void ExportMap()
//     {
//         // Collect all GridTile objects from the scene.
//         GridTile[] allTiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
//         if (allTiles.Length == 0)
//         {
//             Debug.LogWarning("No GridTile objects found in scene.");
//             return;
//         }
//
//         // Determine map bounds.
//         int maxX = 0;
//         int maxY = 0;
//         foreach (var tile in allTiles)
//         {
//             maxX = Mathf.Max(maxX, tile.gridPos.x);
//             maxY = Mathf.Max(maxY, tile.gridPos.y);
//         }
//
//         int width = maxX + 1;
//         int height = maxY + 1;
//
//         // Create new MapData asset.
//         MapData newMap = ScriptableObject.CreateInstance<MapData>();
//         newMap.width = width;
//         newMap.height = height;
//         newMap.InitializeTiles();
//
//         foreach (var tile in allTiles)
//         {
//             int index = tile.gridPos.y * width + tile.gridPos.x;
//
//             MapData.TileInfo info = new MapData.TileInfo();
//             info.type = tile.tileType;
//
//             // Handle gate tile's required key IDs.
//             if (tile.tileType == TileType.Gate && tile.requiredKeyIds != null)
//             {
//                 info.gateRequiredKeys = tile.requiredKeyIds;
//             }
//
//             // Handle obstacle (save its script type).
//             if (tile.HasObstacle(out ObstacleBase obstacle))
//             {
//                 info.obstacleType = obstacle.GetType().Name;
//
//                 // Export obstacle metadata
//                 if (obstacle.GetMetadata() is Dictionary<string, string> metadata)
//                 {
//                     info.obstacleMetadata = new SerializableDictionary<string, string>();
//                     foreach (var kvp in metadata)
//                     {
//                         info.obstacleMetadata.Add(kvp.Key, kvp.Value);
//                     }
//                 }
//             }
//
//             newMap.SetTileInfo(tile.gridPos.x, tile.gridPos.y, info);
//         }
//
//         // Prompt user to save asset.
//         string folderPath = EditorUtility.SaveFilePanelInProject(
//             "Save Map Data",
//             "NewMapData",
//             "asset",
//             "Choose location to save the map asset");
//
//         if (!string.IsNullOrEmpty(folderPath))
//         {
//             AssetDatabase.CreateAsset(newMap, folderPath);
//             AssetDatabase.SaveAssets();
//             AssetDatabase.Refresh();
//
//             EditorUtility.FocusProjectWindow();
//             Selection.activeObject = newMap;
//
//             Debug.Log($"Map exported successfully to: {folderPath}");
//         }
//         else
//         {
//             Debug.LogWarning("Export cancelled.");
//         }
//     }
// }

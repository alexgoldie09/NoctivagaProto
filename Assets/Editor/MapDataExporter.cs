using UnityEngine;
using UnityEditor;
using System.IO;

public class MapDataExporter 
{
    [MenuItem("Noctivaga/Export Map From Scene")]
    public static void ExportMap() 
    {
        GridTile[] allTiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        if (allTiles.Length == 0) 
        {
            Debug.LogWarning("No GridTile objects found in scene.");
            return;
        }

        // Determine bounds
        int maxX = 0;
        int maxY = 0;

        foreach (var tile in allTiles) 
        {
            maxX = Mathf.Max(maxX, tile.gridPos.x);
            maxY = Mathf.Max(maxY, tile.gridPos.y);
        }

        int width = maxX + 1;
        int height = maxY + 1;

        TileType[] tileArray = new TileType[width * height];

        foreach (var tile in allTiles) 
        {
            int index = tile.gridPos.y * width + tile.gridPos.x;
            if (index < tileArray.Length) 
            {
                tileArray[index] = tile.tileType;
            }
        }

        // Create and fill ScriptableObject
        MapData newMap = ScriptableObject.CreateInstance<MapData>();
        newMap.width = width;
        newMap.height = height;
        newMap.tiles = tileArray;

        // Ask where to save the file
        string folderPath = EditorUtility.SaveFilePanelInProject(
            "Save Map Data",
            "NewMapData",
            "asset",
            "Choose location to save the map asset"
        );

        if (!string.IsNullOrEmpty(folderPath)) 
        {
            AssetDatabase.CreateAsset(newMap, folderPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newMap;

            Debug.Log($"Map exported successfully to: {folderPath}");
        } 
        else 
        {
            Debug.LogWarning("Export cancelled.");
        }
    }
}
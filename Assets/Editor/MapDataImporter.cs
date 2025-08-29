using UnityEngine;
using UnityEditor;
using System.IO;

public class MapDataImporter : EditorWindow 
{
    private MapData mapToImport;
    private GameObject tilePrefab;
    private Transform parent;
    private Sprite floorSprite, voidSprite, wallSprite;

    [MenuItem("Noctivaga/Import Map To Scene")]
    public static void ShowWindow() 
    {
        GetWindow<MapDataImporter>("Map Importer");
    }

    private void OnGUI() 
    {
        EditorGUILayout.LabelField("Map Import Settings", EditorStyles.boldLabel);
        mapToImport = (MapData)EditorGUILayout.ObjectField("MapData Asset", mapToImport, typeof(MapData), false);
        tilePrefab = (GameObject)EditorGUILayout.ObjectField("Tile Prefab", tilePrefab, typeof(GameObject), false);
        parent = (Transform)EditorGUILayout.ObjectField("Parent Object", parent, typeof(Transform), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sprites", EditorStyles.boldLabel);
        floorSprite = (Sprite)EditorGUILayout.ObjectField("Floor Sprite", floorSprite, typeof(Sprite), false);
        voidSprite = (Sprite)EditorGUILayout.ObjectField("Void Sprite", voidSprite, typeof(Sprite), false);
        wallSprite = (Sprite)EditorGUILayout.ObjectField("Wall Sprite", wallSprite, typeof(Sprite), false);

        EditorGUILayout.Space();
        if (GUILayout.Button("Import Map Into Scene")) 
        {
            if (mapToImport != null && tilePrefab != null) 
            {
                ImportMap();
            } 
            else 
            {
                Debug.LogError("Please assign a MapData asset and a Tile Prefab.");
            }
        }
    }

    private void ImportMap() 
    {
        if (parent == null) 
        {
            GameObject parentObj = new GameObject("ImportedGrid");
            parent = parentObj.transform;
        }

        // Clean up existing children
        for (int i = parent.childCount - 1; i >= 0; i--) 
        {
            DestroyImmediate(parent.GetChild(i).gameObject);
        }

        int width = mapToImport.width;
        int height = mapToImport.height;

        for (int y = 0; y < height; y++) 
        {
            for (int x = 0; x < width; x++) 
            {
                GameObject tileObj = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, parent);
                tileObj.transform.position = new Vector3(x, y, 0);

                GridTile tile = tileObj.GetComponent<GridTile>();
                tile.gridPos = new Vector2Int(x, y);
                tile.tileType = mapToImport.GetTile(x, y);

                tile.floorSprite = floorSprite;
                tile.voidSprite = voidSprite;
                tile.wallSprite = wallSprite;
                tile.UpdateVisual();
            }
        }

        Debug.Log($"Map imported successfully into scene as '{parent.name}'");
    }
}

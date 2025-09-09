using UnityEngine;
using UnityEditor;
using Unity.VisualScripting;
using System.Collections.Generic;

/// <summary>
/// Editor utility for importing a MapData asset into the Unity scene as a grid of tile GameObjects.
/// Supports assigning tile prefab, parent transform, and individual sprites for each tile type.
/// Also instantiates obstacle prefabs based on their registered string identifiers.
/// </summary>
public class MapDataImporter : EditorWindow 
{
    private MapData mapToImport;
    private GameObject tilePrefab;
    private Transform parent;
    private ObstacleRegistry obstacleRegistry;
    private Sprite floorSprite, voidSprite, wallSprite, gateSprite, startSprite;

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
        obstacleRegistry = (ObstacleRegistry)EditorGUILayout.ObjectField("Obstacle Registry", obstacleRegistry, typeof(ObstacleRegistry), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sprites", EditorStyles.boldLabel);
        floorSprite = (Sprite)EditorGUILayout.ObjectField("Floor Sprite", floorSprite, typeof(Sprite), false);
        voidSprite = (Sprite)EditorGUILayout.ObjectField("Void Sprite", voidSprite, typeof(Sprite), false);
        wallSprite = (Sprite)EditorGUILayout.ObjectField("Wall Sprite", wallSprite, typeof(Sprite), false);
        gateSprite = (Sprite)EditorGUILayout.ObjectField("Gate Sprite", gateSprite, typeof(Sprite), false);
        startSprite = (Sprite)EditorGUILayout.ObjectField("Start Sprite", startSprite, typeof(Sprite), false);

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

    /// <summary>
    /// Instantiates and configures the grid tiles based on the MapData.
    /// </summary>
    private void ImportMap() 
    {
        if (parent == null) 
        {
            GameObject parentObj = new GameObject("ImportedGrid");
            parent = parentObj.transform;
        }

        // Clear existing children
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
                MapData.TileInfo info = mapToImport.GetTileInfo(x, y);

                // Instantiate tile
                GameObject tileObj = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, parent);
                tileObj.transform.position = new Vector3(x, y, 0);

                GridTile tile = tileObj.GetComponent<GridTile>();
                tile.gridPos = new Vector2Int(x, y);
                tile.tileType = info.type;
                tile.floorSprite = floorSprite;
                tile.voidSprite = voidSprite;
                tile.wallSprite = wallSprite;
                tile.gateSprite = gateSprite;
                tile.startSprite = startSprite;

                tile.UpdateVisual();

                // Restore gate required keys
                if (tile.tileType == TileType.Gate && info.gateRequiredKeys != null)
                {
                    tile.requiredKeyIds = info.gateRequiredKeys;
                }

                // Restore obstacle if ID exists
                if (!string.IsNullOrEmpty(info.obstacleType))
                {
                    GameObject prefab = obstacleRegistry?.GetPrefab(info.obstacleType);
                    if (prefab != null)
                    {
                        GameObject obsObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tileObj.transform);
                        obsObj.transform.localPosition = Vector3.zero;

                        ObstacleBase obs = obsObj.GetComponent<ObstacleBase>();
                        if (obs != null)
                        {
                            tile.SetObstacle(obs);

                            // Restore obstacle metadata if present
#if UNITY_EDITOR
                            // Delay SetMetadata to ensure all tiles exist in the scene
                            if (info.obstacleMetadata != null && info.obstacleMetadata.Count > 0)
                            {
                                var metadataCopy = new Dictionary<string, string>(info.obstacleMetadata);

                                // Use delay only for obstacles that rely on tiles being present
                                bool requiresDelay = obs is LeverObstacle;

                                if (requiresDelay)
                                {
                                    EditorApplication.delayCall += () =>
                                    {
                                        if (obs != null)
                                        {
                                            obs.SetMetadata(metadataCopy);
                                        }
                                    };
                                }
                                else
                                {
                                    obs.SetMetadata(metadataCopy);
                                }
                            }
#endif
                        }
                        else
                        {
                            Debug.LogWarning($"Prefab '{info.obstacleType}' has no ObstacleBase component.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"No registered prefab found for ID '{info.obstacleType}'");
                    }
                }
            }
        }

        Debug.Log($"Map imported successfully into scene as '{parent.name}'");
    }
}

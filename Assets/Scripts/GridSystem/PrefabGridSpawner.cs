using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PrefabGridSpawner : MonoBehaviour 
{
    public GameObject tilePrefab;
    public int width = 10;
    public int height = 10;

    [ContextMenu("Spawn Grid")]
    public void SpawnGrid() 
    {
        // Clear existing children
        for (int i = transform.childCount - 1; i >= 0; i--) 
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

#if UNITY_EDITOR
        for (int x = 0; x < width; x++) 
        {
            for (int y = 0; y < height; y++) 
            {
                Vector3 pos = new Vector3(x, y, 0);
                GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, transform);
                tile.transform.position = pos;

                GridTile gridTile = tile.GetComponent<GridTile>();
                if (gridTile != null) 
                {
                    gridTile.gridPos = new Vector2Int(x, y);
                }
            }
        }
#endif
    }
}
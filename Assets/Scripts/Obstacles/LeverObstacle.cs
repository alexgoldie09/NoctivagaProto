using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lever obstacle that activates or deactivates target tiles when interacted with.
/// Can toggle between floor and void tiles during gameplay.
/// </summary>
public class LeverObstacle : ObstacleBase
{
    [Header("Tiles to Toggle")]
    public List<GameObject> targetTiles = new List<GameObject>(); // List of tile GameObjects affected by the lever

    /// <summary>
    /// Called when the lever is interacted with.
    /// Toggles each target tile between Floor and Void types.
    /// Ensures player is not left on a void tile after toggling.
    /// </summary>
    public override void Interact()
    {
        foreach (GameObject obj in targetTiles)
        {
            if (obj == null) continue;

            GridTile tile = obj.GetComponent<GridTile>();
            if (tile == null) continue;

            switch (tile.tileType)
            {
                case TileType.Floor:
                    tile.SetTileType(TileType.Void);
                    break;

                case TileType.Void:
                    tile.SetTileType(TileType.Floor);
                    break;

                default:
                    // Do nothing for other types like Wall or Gate
                    break;
            }
        }

        // Safety: Ensure player is not standing on a void tile after toggling
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector2Int playerPos = player.GridPosition;
            GridTile currentTile = GridManager.Instance.GetTileAt(playerPos.x, playerPos.y);

            if (currentTile != null && currentTile.tileType == TileType.Void)
            {
                Vector2Int safeSpot = player.FindNearestWalkable(playerPos);
                player.TeleportTo(safeSpot); // Must be implemented in PlayerController
            }
        }

        // Optional: Add visual/sound feedback here (e.g. SFX, animation)
    }

    /// <summary>
    /// Returns true, indicating this obstacle blocks player movement.
    /// </summary>
    public override bool BlocksMovement() => true;

    /// <summary>
    /// Returns true, indicating this obstacle blocks shape placement.
    /// </summary>
    public override bool BlocksShapePlacement() => true;

    /// <summary>
    /// Serializes target tile positions into a string format for saving.
    /// </summary>
    public override Dictionary<string, string> GetMetadata()
    {
        Dictionary<string, string> data = new Dictionary<string, string>();

        List<string> posStrings = new List<string>(); // Holds "x,y" strings for each tile
        foreach (var tileObj in targetTiles)
        {
            if (tileObj == null) continue;

            GridTile tile = tileObj.GetComponent<GridTile>();
            if (tile != null)
            {
                Vector2Int pos = tile.gridPos;
                posStrings.Add($"{pos.x},{pos.y}");
            }
        }

        data["targets"] = string.Join(";", posStrings); // Combine all tile positions into one string
        return data;
    }

    /// <summary>
    /// Deserializes tile positions and resolves references using GridManager or scene search.
    /// </summary>
    public override void SetMetadata(Dictionary<string, string> data)
    {
        targetTiles.Clear(); // Reset list before loading

        if (data.TryGetValue("targets", out string value))
        {
            string[] parts = value.Split(';');
            foreach (string part in parts)
            {
                string[] xy = part.Split(',');
                if (xy.Length == 2 &&
                    int.TryParse(xy[0], out int x) &&
                    int.TryParse(xy[1], out int y))
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    GameObject target = null;

                    // Try to resolve target via GridManager (runtime)
                    if (GridManager.Instance != null)
                    {
                        var tile = GridManager.Instance.GetTileAt(x, y);
                        if (tile != null)
                            target = tile.gameObject;
                    }

#if UNITY_EDITOR
                    // Fallback: Editor-time FindObjectsByType() for scene references
                    if (target == null && !Application.isPlaying)
                    {
                        foreach (var tile in FindObjectsByType<GridTile>(FindObjectsSortMode.None))
                        {
                            if (tile.gridPos == pos)
                            {
                                target = tile.gameObject;
                                break;
                            }
                        }
                    }
#endif

                    if (target != null)
                        targetTiles.Add(target);
                }
            }
        }

#if UNITY_EDITOR
        // Mark this object and scene dirty so Unity saves changes
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}

using System.Collections.Generic;
using UnityEngine;

public class LeverObstacle : ObstacleBase
{
    [Header("Tiles to Toggle")]
    public List<GameObject> targetTiles = new List<GameObject>();

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
                    // Do nothing for walls, gates, etc.
                    break;
            }
        }
        
        // ---- Player safety check after map change ----
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector2Int playerPos = player.GridPosition;
            GridTile currentTile = GridManager.Instance.GetTileAt(playerPos.x, playerPos.y);

            if (currentTile != null && currentTile.tileType == TileType.Void)
            {
                Vector2Int safeSpot = player.FindNearestWalkable(playerPos);
                player.TeleportTo(safeSpot); // You’ll need to add this method below
            }
        }

        // Optional: Add sound or animation feedback here
    }

    public override bool BlocksMovement() => true;
    public override bool BlocksShapePlacement() => true;
}
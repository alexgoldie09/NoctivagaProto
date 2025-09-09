using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls player movement and interaction on a tile-based grid.
/// </summary>
public class PlayerController : MonoBehaviour 
{
    private Vector2Int gridPos; // Player's current grid coordinates
    private Vector2Int lastDirection = Vector2Int.right; // Facing direction for interaction
    private SpriteRenderer sr; // Cached sprite renderer for flipping
    private GridManager grid; // Cached instance of grid

    void Start() 
    {
        sr = GetComponent<SpriteRenderer>();

        grid = GridManager.Instance;
        if (grid == null) 
        {
            Debug.LogError("GridManager instance not found.");
            return;
        }

        GridTile startTile = grid.GetStartTile();
        if (startTile != null)
        {
            gridPos = startTile.gridPos;
        }
        else
        {
            Vector2Int center = new Vector2Int(grid.width / 2, grid.height / 2);
            gridPos = FindNearestWalkable(center);
        }

        transform.position = GridToWorld(gridPos);
    }

    void Update()
    {
        HandleInput();
    }

    /// <summary>
    /// Handles WASD/arrow input and interaction key.
    /// </summary>
    private void HandleInput()
    {
        Vector2Int input = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            input = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            input = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            input = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            input = Vector2Int.right;

        if (input != Vector2Int.zero) 
        {
            lastDirection = input;
            sr.flipX = lastDirection.x < 0;
            TryMove(input);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            TryInteract();
        }
    }

    /// <summary>
    /// Attempts to move the player in the specified direction.
    /// </summary>
    void TryMove(Vector2Int direction) 
    {
        Vector2Int nextPos = gridPos + direction;

        if (!grid.IsInBounds(nextPos.x, nextPos.y))
            return;

        GridTile targetTile = grid.GetTileAt(nextPos.x, nextPos.y);
        if (targetTile == null)
            return;

        // Check for gates and unlock if player has required keys
        if (targetTile.tileType == TileType.Gate)
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory != null && targetTile.CanUnlock(inventory))
            {
                targetTile.UnlockWithKeys(inventory);
            }
            else
            {
                string keyIds = string.Join(", ", targetTile.requiredKeyIds);
                Debug.Log($"Can't unlock gate, you need: {keyIds}");
                return;
            }
        }

        if (targetTile.IsWalkable())
        {
            gridPos = nextPos;
            transform.position = GridToWorld(gridPos);
        }
    }
    
    /// <summary>
    /// Attempts to interact with the tile the player is facing.
    /// </summary>
    void TryInteract()
    {
        Vector2Int targetPos = GridPosition + FacingDirection;
        GridTile tile = grid.GetTileAt(targetPos.x, targetPos.y);

        if (tile != null && tile.HasObstacle(out ObstacleBase obstacle))
        {
            obstacle.Interact();
        }
    }

    /// <summary>
    /// Finds the nearest walkable tile from a starting position.
    /// </summary>
    public Vector2Int FindNearestWalkable(Vector2Int center) 
    {
        if (grid.IsWalkable(center.x, center.y)) 
            return center;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        queue.Enqueue(center);
        visited.Add(center);

        Vector2Int[] directions = 
        {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        while (queue.Count > 0) 
        {
            Vector2Int current = queue.Dequeue();

            foreach (var dir in directions) 
            {
                Vector2Int neighbor = current + dir;

                if (visited.Contains(neighbor) || !grid.IsInBounds(neighbor.x, neighbor.y))
                    continue;

                if (grid.IsWalkable(neighbor.x, neighbor.y))
                    return neighbor;

                queue.Enqueue(neighbor);
                visited.Add(neighbor);
            }
        }

        // Debug.LogWarning("No walkable tile found near center. Player may be stuck.");
        return center;
    }

    /// <summary>
    /// Converts a grid coordinate to world space.
    /// </summary>
    Vector3 GridToWorld(Vector2Int pos) 
    {
        return new Vector3(pos.x, pos.y, 0f);
    }

    /// <summary>
    /// Moves the player instantly to a new grid position.
    /// </summary>
    public void TeleportTo(Vector2Int newPos)
    {
        gridPos = newPos;
        transform.position = GridToWorld(newPos);
    }

    public Vector2Int GridPosition => gridPos;
    public Vector2Int FacingDirection => lastDirection;
}

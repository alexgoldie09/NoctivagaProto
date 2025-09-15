using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls player movement and interaction on a tile-based grid.
/// Delegates all scoring and feedback to ScoreManager.
/// </summary>
public class PlayerController : MonoBehaviour 
{
    private Vector2Int gridPos;                  // Player's current grid coordinates
    private Vector2Int lastDirection = Vector2Int.right; // Facing direction for interaction
    private SpriteRenderer sr;                   // Cached sprite renderer for flipping
    private GridManager grid;                    // Cached instance of grid
    public bool isShadowMode = false;            // Used by Shadow Mode powerup

    // ─────────────────────────────────────────────
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
            gridPos = startTile.gridPos;
        else
            gridPos = FindNearestWalkable(new Vector2Int(grid.width / 2, grid.height / 2));

        transform.position = GridToWorld(gridPos);
    }

    void Update()
    {
        // Stop player input if game is frozen
        if (Utilities.IsGameFrozen) return;
        
        HandleInput();
    }

    // ─────────────────────────────────────────────
    #region Input

    /// <summary>
    /// Handles WASD/arrow input and interaction key.
    /// </summary>
    private void HandleInput()
    {
        Vector2Int input = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W))
            input = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S))
            input = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A))
            input = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D))
            input = Vector2Int.right;

        if (input != Vector2Int.zero) 
        {
            lastDirection = input;
            sr.flipX = lastDirection.x < 0;
            TryMove(input);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Actions

    /// <summary>
    /// Attempts to move the player in the specified direction.
    /// </summary>
    private void TryMove(Vector2Int direction) 
    {
        Vector2Int nextPos = gridPos + direction;

        if (!grid.IsInBounds(nextPos.x, nextPos.y)) return;

        GridTile targetTile = grid.GetTileAt(nextPos.x, nextPos.y);
        if (targetTile == null) return;

        // Handle gates
        if (targetTile.tileType == TileType.Gate)
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory != null && targetTile.CanUnlock(inventory))
                targetTile.UnlockWithKeys(inventory);
            else
                return;
        }

        if (targetTile.IsWalkable())
        {
            gridPos = nextPos;
            transform.position = GridToWorld(gridPos);

            RegisterActionScore("Move");
        }
    }
    
    /// <summary>
    /// Attempts to interact with the tile the player is facing.
    /// </summary>
    private void TryInteract()
    {
        Vector2Int targetPos = GridPosition + FacingDirection;
        GridTile tile = grid.GetTileAt(targetPos.x, targetPos.y);

        if (tile != null && tile.HasObstacle(out ObstacleBase obstacle))
        {
            obstacle.Interact();
            RegisterActionScore("Interact");
        }
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Scoring

    /// <summary>
    /// Determines rhythm quality, calculates points, and notifies ScoreManager.
    /// </summary>
    private void RegisterActionScore(string actionType)
    {
        BeatHitQuality quality = RhythmManager.Instance.GetHitQuality();
        int points = Utilities.GetPointsForQuality(quality);

        ScoreManager.Instance.RegisterMove();
        ScoreManager.Instance.AddRhythmScore(points, quality);
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Helpers

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

        return center; // fallback
    }

    /// <summary>
    /// Converts a grid coordinate to world space.
    /// </summary>
    private Vector3 GridToWorld(Vector2Int pos) 
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
    #endregion
}

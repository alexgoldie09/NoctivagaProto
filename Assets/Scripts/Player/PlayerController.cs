using UnityEngine;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour 
{
    public GridManager gridManager;

    private Vector2Int gridPos;

    void Start() 
    {
        Vector2Int center = new Vector2Int(gridManager.width / 2, gridManager.height / 2);
        gridPos = FindNearestWalkable(center);
        transform.position = GridToWorld(gridPos);
    }

    void Update() 
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
            TryMove(input);
        }
    }

    void TryMove(Vector2Int direction) 
    {
        Vector2Int nextPos = gridPos + direction;

        if (gridManager.IsInBounds(nextPos.x, nextPos.y) &&
            gridManager.IsWalkable(nextPos.x, nextPos.y)) {

            gridPos = nextPos;
            transform.position = GridToWorld(gridPos);
        }
    }

    Vector2Int FindNearestWalkable(Vector2Int center) 
    {
        if (gridManager.IsWalkable(center.x, center.y)) return center;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        queue.Enqueue(center);
        visited.Add(center);

        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        while (queue.Count > 0) {
            Vector2Int current = queue.Dequeue();

            foreach (var dir in directions) {
                Vector2Int neighbor = current + dir;

                if (visited.Contains(neighbor) || !gridManager.IsInBounds(neighbor.x, neighbor.y))
                    continue;

                if (gridManager.IsWalkable(neighbor.x, neighbor.y))
                    return neighbor;

                queue.Enqueue(neighbor);
                visited.Add(neighbor);
            }
        }

        Debug.LogWarning("No walkable tile found near center. Player may be stuck.");
        return center; // fallback (could be void!)
    }

    Vector3 GridToWorld(Vector2Int pos) 
    {
        return new Vector3(pos.x, pos.y, 0f);
    }
}

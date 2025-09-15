using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A static enemy that performs a swipe attack every few beats.
/// The swipe covers a 2x2 quad of tiles in a chosen cardinal direction.
/// Only valid walkable tiles are considered for swipe effects and contact.
/// </summary>
public class EnemyStaticSwiping : EnemyStatic
{
    [Header("Swipe Settings")]
    [Tooltip("Prefab for swipe visual (optional).")]
    [SerializeField] private GameObject swipeVFXPrefab;

    [Tooltip("Offset from this enemy where the swipe quad starts (1 = one tile away).")]
    [SerializeField] private int attackRange = 1;
    
    [Tooltip("Alpha-flash telegraph color for warning tiles.")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0f, 0f, 0.5f);

    public enum SwipeDirection { Up, Down, Left, Right }
    [Header("Attack Direction")]
    [SerializeField] private SwipeDirection attackDirection = SwipeDirection.Up;
    private List<Vector2Int> pendingAttackTiles = new List<Vector2Int>();

    protected override void OnBeatAction()
    {
        base.OnBeatAction();

        if (pendingAttackTiles.Count > 0)
        {
            // Execute the real swipe
            ExecuteSwipe(pendingAttackTiles);
            pendingAttackTiles.Clear();
        }
        else
        {
            // Telegraph tiles this beat
            Vector2Int dir = GetDirectionVector(attackDirection);
            pendingAttackTiles = GetSwipeTiles(dir);

            foreach (var pos in pendingAttackTiles)
            {
                GridTile tile = grid.GetTileAt(pos.x, pos.y);
                if (tile != null)
                {
                    tile.FlashWarning(telegraphColor, 0.3f);
                    // flash before attack
                }
            }
        }
    }

    private void ExecuteSwipe(List<Vector2Int> tiles)
    {
        // Spawn swipe VFX
        if (swipeVFXPrefab != null)
        {
            foreach (var tile in tiles)
            {
                Vector3 worldPos = GridToWorld(tile);
                Instantiate(swipeVFXPrefab, worldPos, Quaternion.identity);
            }
        }

        // Player contact check
        if (player != null && tiles.Contains(player.GridPosition))
        {
            OnPlayerContact();
        }
    }

    /// <summary>
    /// Returns valid grid positions for the swipe quad in the given direction.
    /// Filters out tiles that are out of bounds or not walkable.
    /// </summary>
    private List<Vector2Int> GetSwipeTiles(Vector2Int dir)
    {
        List<Vector2Int> tiles = new List<Vector2Int>();

        Vector2Int basePos = gridPos + dir * attackRange;
        List<Vector2Int> candidates = new List<Vector2Int>();

        if (dir == Vector2Int.up || dir == Vector2Int.down)
        {
            candidates.Add(basePos);
            candidates.Add(basePos + Vector2Int.right);
            candidates.Add(basePos + Vector2Int.left);
            candidates.Add(basePos + dir);
        }
        else
        {
            candidates.Add(basePos);
            candidates.Add(basePos + Vector2Int.up);
            candidates.Add(basePos + Vector2Int.down);
            candidates.Add(basePos + dir);
        }

        foreach (var c in candidates)
        {
            if (grid != null && grid.IsInBounds(c.x, c.y) && grid.IsWalkable(c.x, c.y))
                tiles.Add(c);
        }

        return tiles;
    }

    private Vector2Int GetDirectionVector(SwipeDirection dir)
    {
        switch (dir)
        {
            case SwipeDirection.Up: return Vector2Int.up;
            case SwipeDirection.Down: return Vector2Int.down;
            case SwipeDirection.Left: return Vector2Int.left;
            case SwipeDirection.Right: return Vector2Int.right;
            default: return Vector2Int.up;
        }
    }

    // ─────────────────────────────────────────────
    // Gizmos show only chosen direction
    private void OnDrawGizmosSelected()
    {
        if (grid == null) return;

        // Pick color for chosen direction
        Color gizmoColor = Color.white;
        switch (attackDirection)
        {
            case SwipeDirection.Up: gizmoColor = new Color(0f, 0f, 1f, 0.4f); break;    // blue
            case SwipeDirection.Down: gizmoColor = new Color(1f, 0f, 0f, 0.4f); break; // red
            case SwipeDirection.Left: gizmoColor = new Color(0f, 1f, 0f, 0.4f); break; // green
            case SwipeDirection.Right: gizmoColor = new Color(1f, 1f, 0f, 0.4f); break;// yellow
        }

        Gizmos.color = gizmoColor;

        Vector2Int dir = GetDirectionVector(attackDirection);
        List<Vector2Int> tiles = GetSwipeTiles(dir);

        foreach (var tile in tiles)
        {
            Vector3 pos = GridToWorld(tile);
            Gizmos.DrawCube(pos + Vector3.one * 0.5f, Vector3.one * 0.9f);
        }
    }
}

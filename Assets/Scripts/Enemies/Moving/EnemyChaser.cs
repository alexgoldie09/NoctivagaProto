using UnityEngine;
using System.Linq; // for OrderBy random shuffle

/// <summary>
/// A chasing enemy that moves toward the player’s grid position
/// using Manhattan distance. Moves one tile per action beat.
/// If its path is blocked, it stays put that beat.
/// In Shadow Mode, the enemy moves randomly instead of chasing.
/// </summary>
public class EnemyChaser : EnemyBase
{
    private SpriteRenderer spriteRenderer;

    protected override void Start()
    {
        base.Start(); // keep base setup
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    
    protected override void OnBeatAction()
    {
        if (player == null || grid == null) return;

        if (player.IsShadowMode)
        {
            // ─── Shadow Mode: Wander randomly ───────────────────────────────────
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            dirs = dirs.OrderBy(x => Random.value).ToArray();

            foreach (var d in dirs)
            {
                if (TryStep(d))
                {
                    // Flip sprite if moved horizontally
                    if (spriteRenderer != null && d.x != 0)
                        spriteRenderer.flipX = d.x < 0;
                    break;
                }
            }
        }
        else
        {
            // ─── Normal chase mode ─────────────────────────────────────────────
            Vector2Int playerPos = player.GridPosition;
            Vector2Int dir = GetChaseDirection(playerPos);

            // Flip sprite if moving horizontally
            if (spriteRenderer != null && dir.x != 0)
            {
                spriteRenderer.flipX = dir.x < 0;
            }

            // Try moving in primary direction
            if (!TryStep(dir))
            {
                // If blocked, try the secondary direction
                Vector2Int altDir = GetAlternateDirection(playerPos, dir);

                if (spriteRenderer != null && altDir.x != 0)
                {
                    spriteRenderer.flipX = altDir.x < 0;
                }

                TryStep(altDir);
            }
        }

        // Trigger animation
        if (animator != null)
        {
            animator.SetTrigger("OnBeat");
        }
    }

    /// <summary>
    /// Chooses the primary direction to step toward the player
    /// (the axis with the greater absolute distance).
    /// </summary>
    private Vector2Int GetChaseDirection(Vector2Int targetPos)
    {
        int dx = targetPos.x - gridPos.x;
        int dy = targetPos.y - gridPos.y;

        if (Mathf.Abs(dx) > Mathf.Abs(dy))
        {
            return dx > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            return dy > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }

    /// <summary>
    /// Chooses the alternate direction (orthogonal axis) if the first choice is blocked.
    /// </summary>
    private Vector2Int GetAlternateDirection(Vector2Int targetPos, Vector2Int triedDir)
    {
        int dx = targetPos.x - gridPos.x;
        int dy = targetPos.y - gridPos.y;

        // If we tried horizontal first, fallback to vertical
        if (triedDir.x != 0)
        {
            return dy > 0 ? Vector2Int.up : Vector2Int.down;
        }
        else
        {
            return dx > 0 ? Vector2Int.right : Vector2Int.left;
        }
    }
}

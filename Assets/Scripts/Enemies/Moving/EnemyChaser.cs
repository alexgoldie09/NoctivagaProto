using UnityEngine;

/// <summary>
/// A chasing enemy that moves toward the player’s grid position
/// using Manhattan distance. Moves one tile per action beat.
/// If its path is blocked, it stays put that beat.
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

        Vector2Int playerPos = player.GridPosition;
        Vector2Int dir = GetChaseDirection(playerPos);

        // 👉 Flip sprite if moving horizontally
        if (spriteRenderer != null && dir.x != 0)
        {
            spriteRenderer.flipX = dir.x < 0;
        }

        // Try moving in primary direction
        if (!TryStep(dir))
        {
            // If blocked, try the secondary direction
            Vector2Int altDir = GetAlternateDirection(playerPos, dir);

            // 👉 Also flip when fallback is horizontal
            if (spriteRenderer != null && altDir.x != 0)
            {
                spriteRenderer.flipX = altDir.x < 0;
            }

            TryStep(altDir);
        }

        // Optionally trigger animation
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

using UnityEngine;
using System.Linq; // for OrderBy random shuffle

/// <summary>
/// A chasing enemy that moves toward the player’s cell position
/// using Manhattan distance. Moves one tile per action beat.
/// If its path is blocked, it stays put that beat.
/// In Shadow Mode, the enemy moves randomly instead of chasing.
/// </summary>
public class EnemyChaser : EnemyBase
{
    private SpriteRenderer spriteRenderer;

    protected override void Start()
    {
        base.Start();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    protected override void OnBeatAction()
    {
        if (player == null || grid == null) return;

        // Shadow mode: wander
        if (player.IsShadowMode)
        {
            Vector3Int[] dirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
            dirs = dirs.OrderBy(_ => Random.value).ToArray();

            foreach (var d in dirs)
            {
                if (TryMove(d))
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
            // Normal chase: primary axis first, then fallback axis
            Vector3Int playerCell = player.CellPosition;
            Vector3Int dir = GetChaseDirection(playerCell);

            if (spriteRenderer != null && dir.x != 0)
                spriteRenderer.flipX = dir.x < 0;

            if (!TryMove(dir))
            {
                Vector3Int altDir = GetAlternateDirection(playerCell, dir);

                if (spriteRenderer != null && altDir.x != 0)
                    spriteRenderer.flipX = altDir.x < 0;

                TryMove(altDir);
            }
        }

        // Trigger animation
        if (animator != null)
            animator.SetTrigger("OnBeat");
    }

    /// <summary>
    /// Chooses the primary direction to step toward the player
    /// (the axis with the greater absolute distance).
    /// </summary>
    private Vector3Int GetChaseDirection(Vector3Int targetCell)
    {
        int dx = targetCell.x - cellPos.x;
        int dy = targetCell.y - cellPos.y;

        if (Mathf.Abs(dx) > Mathf.Abs(dy))
            return dx > 0 ? Vector3Int.right : Vector3Int.left;
        
        return dy > 0 ? Vector3Int.up : Vector3Int.down;
    }

    /// <summary>
    /// Chooses the alternate direction (orthogonal axis) if the first choice is blocked.
    /// </summary>
    private Vector3Int GetAlternateDirection(Vector3Int targetCell, Vector3Int triedDir)
    {
        int dx = targetCell.x - cellPos.x;
        int dy = targetCell.y - cellPos.y;

        // If we tried horizontal first, fallback to vertical
        if (triedDir.x != 0)
            return dy > 0 ? Vector3Int.up : Vector3Int.down;
        
        return dx > 0 ? Vector3Int.right : Vector3Int.left;
    }
}

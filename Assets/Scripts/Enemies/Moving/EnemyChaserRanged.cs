using UnityEngine;
using System.Linq;

/// <summary>
/// A chasing enemy that moves toward the player and periodically fires a straight-line projectile.
/// Uses the same grid movement rules as EnemyChaser but is not rhythm-bound when useRhythm is disabled.
/// </summary>
public class EnemyChaserRanged : EnemyBase
{
    [Header("Ranged Attack")]
    [Tooltip("Projectile prefab to fire toward the player.")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Number of move actions before firing a projectile.")]
    [SerializeField] private int movesBeforeShot = 2;
    [Tooltip("Spawn transform for projectile.")]
    [SerializeField] private Transform projectileSpawn;

    private SpriteRenderer spriteRenderer;
    private int moveCounter;

    /// <summary>
    /// Initializes the ranged chaser sprite renderer reference.
    /// </summary>
    protected override void Start()
    {
        base.Start();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Moves toward the player or fires after enough moves.
    /// </summary>
    protected override void OnBeatAction()
    {
        if (player == null || grid == null)
            return;

        if (movesBeforeShot < 1)
            movesBeforeShot = 1;

        if (moveCounter >= movesBeforeShot)
        {
            TryShootAtPlayer();
            moveCounter = 0;
            return;
        }

        TryChaseMove();
        moveCounter++;

        if (animator != null)
            animator.SetTrigger("OnBeat");
    }

    private bool TryChaseMove()
    {
        // Shadow mode: wander
        if (player.IsShadowMode)
        {
            Vector3Int[] dirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
            dirs = dirs.OrderBy(_ => Random.value).ToArray();

            foreach (var d in dirs)
            {
                if (TryMove(d))
                {
                    UpdateSpriteFacing(d);
                    return true;
                }
            }

            return false;
        }

        Vector3Int playerCell = player.CellPosition;
        Vector3Int dir = GetChaseDirection(playerCell);
        UpdateSpriteFacing(dir);

        if (TryMove(dir))
            return true;

        Vector3Int altDir = GetAlternateDirection(playerCell, dir);
        UpdateSpriteFacing(altDir);
        return TryMove(altDir);
    }

    private void TryShootAtPlayer()
    {
        if (projectilePrefab == null)
            return;

        Vector3Int playerCell = player.CellPosition;
        Vector3Int dir = GetChaseDirection(playerCell);
        UpdateSpriteFacing(dir);

        Vector2 direction = new Vector2(dir.x, dir.y);

        var projectile = Instantiate(projectilePrefab, projectileSpawn.position, Quaternion.identity);
        projectile.GetComponent<EnemyStraightProjectile>().Initialize(direction);

        if (animator != null)
            animator.SetTrigger("OnBeat");
    }

    private void UpdateSpriteFacing(Vector3Int dir)
    {
        if (spriteRenderer != null && dir.x != 0)
            spriteRenderer.flipX = dir.x < 0;
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

        if (triedDir.x != 0)
            return dy > 0 ? Vector3Int.up : Vector3Int.down;

        return dx > 0 ? Vector3Int.right : Vector3Int.left;
    }
}

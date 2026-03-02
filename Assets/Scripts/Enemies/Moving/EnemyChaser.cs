using UnityEngine;
using System.Linq;

/// <summary>
/// A chasing enemy that moves toward the player’s cell position
/// using Manhattan distance. Moves one tile per action beat.
/// If its path is blocked, it stays put that beat.
/// In Shadow Mode, the enemy moves randomly instead of chasing.
/// </summary>
public class EnemyChaser : EnemyBase
{
    [Header("Chase Settings")]
    [SerializeField, Min(0f)] private float lookRadius = 6f;

    protected SpriteRenderer spriteRenderer;
    
    // Updated in OnBeatAction (authoritative “mode” decision)
    protected bool isWanderingMode;   // true = wander visuals, false = chase visuals
    protected bool isAggroed;         // optional, if you already use it elsewhere
    
    // The last direction we *successfully* moved while wandering.
    // Used so the enemy keeps a consistent facing between beats.
    private Vector3Int lastWanderMoveDir = Vector3Int.right;

    // When true, realtime facing updates are temporarily suspended (eg. during a shoot pose).
    private bool autoFacingLocked;

    /// <summary>
    /// Initializes the chaser sprite renderer reference.
    /// </summary>
    protected override void Start()
    {
        base.Start();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    
    /// <summary>
    /// Keeps the enemy's sprite facing updated in realtime.
    /// This decouples visual facing from rhythm beats (OnBeatAction).
    /// </summary>
    protected virtual void Update()
    {
        if (player == null || spriteRenderer == null)
            return;

        if (autoFacingLocked)
            return;

        UpdateFacingRealtime();
    }
    

    /// <summary>
    /// Moves toward the player on active beats, or wanders in shadow mode.
    /// </summary>
    protected override void OnBeatAction()
    {
        if (player == null || grid == null) return;

        // Cache once per beat (authoritative)
        bool inRange = IsPlayerWithinLookRadius();
        isWanderingMode = player.IsShadowMode || !inRange;
        isAggroed = !isWanderingMode;

        if (isWanderingMode)
            WanderRandomly();
        else
            ChasePlayer();

        if (animator != null)
            animator.SetTrigger("OnBeat");
    }
    
    /// <summary>
    /// Wanders one tile in a random cardinal direction.
    /// </summary>
    private void WanderRandomly()
    {
        Vector3Int[] dirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        dirs = dirs.OrderBy(_ => Random.value).ToArray();

        foreach (var d in dirs)
        {
            if (TryMove(d))
            {
                lastWanderMoveDir = d;
                UpdateSpriteFacing(d);
                break;
            }
        }
    }
    
    /// <summary>
    /// Moves one step toward the player, using axis-priority plus orthogonal fallback.
    /// </summary>
    private void ChasePlayer()
    {
        Vector3Int playerCell = player.CellPosition;
        Vector3Int dir = GetChaseDirection(playerCell);

        if (!TryMove(dir))
        {
            Vector3Int altDir = GetAlternateDirection(playerCell, dir);
            TryMove(altDir);
        }
    }
    
    protected bool CanAttackPlayer() => !player.IsShadowMode && IsPlayerWithinLookRadius();
    
    /// <summary>
    /// Returns true when the player is inside this enemy's look radius.
    /// </summary>
    private bool IsPlayerWithinLookRadius() => Vector3Int.Distance(cellPos, player.CellPosition) <= lookRadius;

    protected void UpdateSpriteFacing(Vector3Int dir)
    {
        if (spriteRenderer != null && dir.x != 0)
            spriteRenderer.flipX = dir.x < 0;
    }
    
    /// Centralized per-frame facing logic.
    /// - If wandering (shadow mode / out of aggro), face the last wander movement direction.
    /// - If chasing, face the player by X delta (even if chase step would be vertical).
    /// </summary>
    private void UpdateFacingRealtime()
    {
        if (isWanderingMode)
        {
            UpdateSpriteFacing(lastWanderMoveDir);
            return;
        }

        // chasing: face by player X delta so it works even when chase direction is vertical
        int dx = player.CellPosition.x - cellPos.x;
        if (dx != 0)
            UpdateSpriteFacing(new Vector3Int(dx, 0, 0));
    }

    /// <summary>
    /// Allows child classes (eg. ranged enemies) to temporarily stop realtime facing updates.
    /// </summary>
    protected void SetAutoFacingLocked(bool locked) => autoFacingLocked = locked;

    /// <summary>
    /// Forces an immediate facing refresh (useful right after unlocking).
    /// </summary>
    protected void RefreshFacingNow()
    {
        if (player == null || spriteRenderer == null)
            return;

        if (!autoFacingLocked)
            UpdateFacingRealtime();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lookRadius);
    }

    /// <summary>
    /// Chooses the primary direction to step toward the player
    /// (the axis with the greater absolute distance).
    /// </summary>
    /// <param name="targetCell">Player grid position to chase.</param>
    /// <returns>Primary chase direction on the grid.</returns>
    protected Vector3Int GetChaseDirection(Vector3Int targetCell)
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
    /// <param name="targetCell">Player grid position to chase.</param>
    /// <param name="triedDir">Primary direction that was attempted.</param>
    /// <returns>Fallback direction on the orthogonal axis.</returns>
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

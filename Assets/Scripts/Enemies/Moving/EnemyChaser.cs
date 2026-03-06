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
    [Header("Line of Sight")]
    [SerializeField, Min(0f)] private float lookRadius = 6f;
    [Tooltip("Layer mask for walls that block the enemy's line of sight.")]
    [SerializeField] private LayerMask wallLayer;
    [Tooltip("Number of radial rays drawn in the Scene view gizmo.")]
    [SerializeField, Range(8, 72)] private int gizmoRayCount = 32;

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
        bool inRange = HasLineOfSightToPlayer();
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
    
    protected bool CanAttackPlayer() => !player.IsShadowMode && HasLineOfSightToPlayer();
    
    /// <summary>
    /// Returns true when the player is within lookRadius AND no wall blocks the line of sight.
    /// The radius acts as the outer bound; a raycast then confirms clear visibility.
    /// </summary>
    private bool HasLineOfSightToPlayer()
    {
        Vector2 origin = transform.position;
        Vector2 target = player.transform.position;
        Vector2 toPlayer = target - origin;
        float distance = toPlayer.magnitude;

        // Quick radius cull — avoids the raycast entirely when out of range.
        if (distance > lookRadius)
            return false;

        // Cast toward the player and stop if a wall is in the way.
        RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distance, wallLayer);
        return hit.collider == null;
    }

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
        Vector3 origin = transform.position;

        // --- Yellow outer radius boundary ---
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, lookRadius);

        // --- Red radial rays, each terminating at the first wall hit ---
        Gizmos.color = Color.red;
        float angleStep = 360f / gizmoRayCount;

        for (int i = 0; i < gizmoRayCount; i++)
        {
            float angleRad = i * angleStep * Mathf.Deg2Rad;
            Vector2 dir2D = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            Vector3 dir3D = new Vector3(dir2D.x, dir2D.y, 0f);

            // In Play mode use the actual Physics2D result; in Edit mode draw to full radius.
            float rayLength = lookRadius;
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                RaycastHit2D hit = Physics2D.Raycast(origin, dir2D, lookRadius, wallLayer);
                if (hit.collider != null)
                    rayLength = hit.distance;
            }
#endif
            Gizmos.DrawLine(origin, origin + dir3D * rayLength);
        }

        // --- Green line to player when visible (Play mode only) ---
#if UNITY_EDITOR
        if (Application.isPlaying && player != null && HasLineOfSightToPlayer())
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, player.transform.position);
        }
#endif
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

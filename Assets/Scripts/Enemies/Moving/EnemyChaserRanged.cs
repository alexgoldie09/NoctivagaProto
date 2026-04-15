using UnityEngine;
using System.Collections;

/// <summary>
/// A chasing enemy that moves toward the player and periodically fires a straight-line projectile.
/// Uses the same grid movement rules as EnemyChaser but is not rhythm-bound when useRhythm is disabled.
/// </summary>
public class EnemyChaserRanged : EnemyChaser
{
    [Header("Ranged Attack")]
    [Tooltip("Projectile prefab to fire toward the player.")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Number of move actions before firing a projectile.")]
    [SerializeField] private int movesBeforeShot = 2;
    [Tooltip("Spawn transform for projectile.")]
    [SerializeField] private Transform projectileSpawn;
    
    [Header("Sprite Visuals")]
    [SerializeField] private Sprite defaultShootSprite;
    [SerializeField] private Sprite downShootSprite;
    [SerializeField] private Sprite upShootSprite;
    [SerializeField, Min(0f)] private float shootPoseDuration = 0.2f;
    
    private int moveCounter;
    private Coroutine shootPoseRoutine;
    
    /// <summary>
    /// Moves toward the player or fires after enough moves.
    /// </summary>
    protected override void OnBeatAction()
    {
        if (movesBeforeShot < 1)
            movesBeforeShot = 1;

        if (moveCounter >= movesBeforeShot)
        {
            if (CanAttackPlayer())
            {
                TryShootAtPlayer();
                moveCounter = 0;
                return;
            }

            // If we are not allowed to shoot (shadow mode / out of range),
            // keep moving with base behavior until the player is valid again.
            base.OnBeatAction();
            return;
        }

        base.OnBeatAction();
        moveCounter++;
    }

    private void TryShootAtPlayer()
    {
        if (projectilePrefab == null || projectileSpawn == null)
            return;
        
        if (player == null || grid == null)
            return;
        
        Vector3Int dir = GetChaseDirection(player.CellPosition);
        
        // Disable animator FIRST so it can't overwrite SpriteRenderer.sprite
        HoldAnimatorOnShootPose();
        
        UpdateShootVisualDirection(dir);

        Vector2 direction = new Vector2(dir.x, dir.y);

        var projectile = Instantiate(projectilePrefab, projectileSpawn.position, Quaternion.identity);
        projectile.GetComponent<EnemyStraightProjectile>().Initialize(direction);
        
        // Only play the sound if the enemy is visible — projectile always fires regardless
        if (IsOnScreen())
            AudioManager.Instance?.PlaySFX("enemy_chaser_snake", 0.75f);
    }

    private void UpdateShootVisualDirection(Vector3Int dir)
    {
        if (spriteRenderer == null)
            return;
        
        if (dir.y > 0 && upShootSprite != null)
        {
            spriteRenderer.sprite = upShootSprite;
            spriteRenderer.flipX = false;
            return;
        }
        
        if (dir.y < 0 && downShootSprite != null)
        {
            spriteRenderer.sprite = downShootSprite;
            spriteRenderer.flipX = false;
            return;
        }
        
        if (defaultShootSprite != null)
            spriteRenderer.sprite = defaultShootSprite;
        
        UpdateSpriteFacing(dir);
    }
    
    private void HoldAnimatorOnShootPose()
    {
        if (animator == null)
            return;

        if (shootPoseRoutine != null)
            StopCoroutine(shootPoseRoutine);
        
        animator.enabled = false; 
        
        // While the shoot pose is active, don't let realtime facing fight our pose.
        SetAutoFacingLocked(true);
        
        shootPoseRoutine = StartCoroutine(RestoreAnimatorSpeedAfterDelay());
    }
    
    private IEnumerator RestoreAnimatorSpeedAfterDelay()
    {
        yield return new WaitForSeconds(shootPoseDuration);

        if (animator != null)
            animator.enabled = true;
        
        // Restore realtime facing after the pose ends
        SetAutoFacingLocked(false);
        RefreshFacingNow();
        
        shootPoseRoutine = null;
    }
}

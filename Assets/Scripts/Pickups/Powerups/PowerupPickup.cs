using UnityEngine;

/// <summary>
/// Handles the pickup logic for powerups on the grid.
/// Powerups activate an immediate effect (e.g., half-time rhythm, shadow mode)
/// instead of being stored in an inventory. They are aligned to grid positions.
/// When the player touches the powerup, it activates and the object is destroyed.
/// </summary>
public class PowerupPickup : MonoBehaviour
{
    [Header("Powerup Properties")]
    [Tooltip("Type of powerup this pickup will activate.")]
    public PowerupType type = PowerupType.HalfTime;

    [Tooltip("Duration of the powerup effect in seconds.")]
    public float duration = 5f;

    [Tooltip("Grid position where this powerup spawns (converted to world position on Start).")]
    public Vector2Int gridPosition;

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Different types of powerups the player can collect.
    /// </summary>
    public enum PowerupType { HalfTime, ShadowMode }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Aligns the powerup to the grid position when the scene starts.
    /// </summary>
    private void Start()
    {
        transform.position = new Vector3(gridPosition.x, gridPosition.y, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when another collider enters the trigger zone.
    /// If the player enters, the powerup effect is activated and this pickup is destroyed.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Ask GameManager if we can activate this powerup
            bool activated = GameManager.Instance.TryActivatePowerup(type, duration);

            if (activated)
            {
                Destroy(gameObject); // only destroy if effect actually applied
            }

        }
    }
}
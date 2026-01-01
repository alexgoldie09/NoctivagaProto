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

    public enum PowerupType { HalfTime, ShadowMode }

    private void Start()
    {
        var grid = TilemapGridManager.Instance;
        if (grid == null)
        {
            Debug.LogError("[PowerupPickup] TilemapGridManager.Instance not found.");
            return;
        }

        Vector3Int cell = grid.WorldToCell(transform.position);
        transform.position = grid.CellToWorldCenter(cell);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        bool activated = GameManager.Instance.TryActivatePowerup(type, duration);

        if (activated)
            Destroy(gameObject);
    }
}
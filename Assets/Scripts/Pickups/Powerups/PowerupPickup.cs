using UnityEngine;

/// <summary>
/// Handles the pickup logic for powerups on the grid.
/// Powerups activate an immediate effect (e.g., half-time rhythm, shadow mode)
/// instead of being stored in an inventory. They are aligned to grid positions.
/// When the player touches the powerup, it activates and the object is destroyed.
/// </summary>
public class PowerupPickup : PickupBase
{
    [Header("Powerup Properties")]
    [Tooltip("Type of powerup this pickup will activate.")]
    public PowerupType type = PowerupType.HalfTime;

    [Tooltip("Duration of the powerup effect in seconds.")]
    public float duration = 5f;

    /// <summary>
    /// Supported powerup effect types.
    /// </summary>
    public enum PowerupType { HalfTime, ShadowMode, Melee }

    /// <summary>
    /// Activates the powerup when the player enters the trigger.
    /// </summary>
    /// <param name="other">Collider that entered the trigger.</param>
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        var activated = GameManager.Instance.TryActivatePowerup(type, duration);

        if (activated)
            Destroy(gameObject);
    }
}

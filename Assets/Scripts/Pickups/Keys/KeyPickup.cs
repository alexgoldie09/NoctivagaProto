using UnityEngine;

/// <summary>
/// Handles the pickup logic for collectible keys on the grid.
/// Keys are identified by a unique string ID and aligned to grid positions.
/// When the player touches the key, it is added to the player's inventory and the object is destroyed.
/// </summary>
public class KeyPickup : PickupBase
{
    [Header("Key Properties")]
    [Tooltip("Unique identifier for the key (used for gates, etc).")]
    public string keyID = "default";

    /// <summary>
    /// Called when another collider enters the trigger zone.
    /// If the player enters, the key is added to their inventory and this pickup is destroyed.
    /// </summary>
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerInventory inventory = other.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.AddKey(keyID);
                Destroy(gameObject); // Remove the key from the scene after collection
            }
        }
    }
}
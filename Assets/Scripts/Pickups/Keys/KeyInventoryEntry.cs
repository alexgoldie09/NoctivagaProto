/// <summary>
/// Represents a single key entry in the player's inventory,
/// including a unique key ID and the number of that key held.
/// </summary>
[System.Serializable]
public class KeyInventoryEntry
{
    public string keyID; // Unique identifier for the key type (e.g., "red", "blue", "gold").
    public int count; // Number of keys of this type currently in the inventory.
}
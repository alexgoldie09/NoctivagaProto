using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the player's inventory including both Tetris shapes and collectible keys.
/// This component is responsible for tracking available resources, consuming them,
/// and updating UI representations such as the key palette.
/// </summary>


public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory")]
    public List<ShapeInventoryEntry> shapeInventory = new List<ShapeInventoryEntry>();
    public List<KeyInventoryEntry> keys = new List<KeyInventoryEntry>();

    [Header("UI References")]
    public KeyPaletteUI keyPaletteUI;
    
    // ─────────────────────────────────────────────────────────────────────────────
    #region SHAPE INVENTORY METHODS
    /// <summary>
    /// Checks if the player has at least one instance of a given shape.
    /// </summary>
    /// <param name="shape">Shape asset to look for.</param>
    /// <returns>True if the shape exists in inventory with a positive count.</returns>
    public bool HasShape(TetrisShapeData shape) =>
        shapeInventory.Exists(entry => entry.shapeData == shape && entry.count > 0);

    /// <summary>
    /// Adds a shape to the inventory, incrementing count if it already exists.
    /// </summary>
    /// <param name="shape">Shape asset to add.</param>
    /// <param name="amount">Number of instances to add.</param>
    public void AddShape(TetrisShapeData shape, int amount = 1)
    {
        var entry = shapeInventory.Find(e => e.shapeData == shape);
        if (entry != null)
        {
            entry.count += amount;
        }
        else
        {
            shapeInventory.Add(new ShapeInventoryEntry
            {
                shapeData = shape,
                count = amount
            });
        }
    }

    /// <summary>
    /// Attempts to consume one instance of a specific shape. Returns true if successful.
    /// </summary>
    /// <param name="shape">Shape asset to consume.</param>
    /// <returns>True if a shape instance was consumed.</returns>
    public bool ConsumeShape(TetrisShapeData shape)
    {
        var entry = shapeInventory.Find(e => e.shapeData == shape && e.count > 0);
        if (entry != null)
        {
            entry.count--;
            return true;
        }
        return false;
    }
    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region KEY INVENTORY METHODS
    /// <summary>
    /// Adds a key to the inventory. Updates the key UI if present.
    /// </summary>
    /// <param name="keyID">Identifier of the key to add.</param>
    public void AddKey(string keyID)
    {
        var entry = keys.Find(k => k.keyID == keyID);
        if (entry != null)
        {
            entry.count++;
        }
        else
        {
            keys.Add(new KeyInventoryEntry
            {
                keyID = keyID,
                count = 1
            });
        }

        keyPaletteUI?.UpdateKey(keyID);
        Debug.Log($"Picked up key: {keyID}. Total: {GetKeyCount(keyID)}");
    }

    /// <summary>
    /// Returns the number of keys in the inventory matching a specific ID.
    /// </summary>
    /// <param name="keyID">Identifier of the key to count.</param>
    /// <returns>Number of keys with the provided ID.</returns>
    public int GetKeyCount(string keyID)
    {
        var entry = keys.Find(k => k.keyID == keyID);
        return entry != null ? entry.count : 0;
    }

    /// <summary>
    /// Attempts to use a key. If successful, decrements the count and updates UI.
    /// </summary>
    /// <param name="keyID">Identifier of the key to use.</param>
    /// <returns>True if a key was consumed.</returns>
    public bool UseKey(string keyID)
    {
        var entry = keys.Find(k => k.keyID == keyID);
        if (entry != null && entry.count > 0)
        {
            entry.count--;
            keyPaletteUI?.UpdateKey(keyID);
            Debug.Log($"Used key: {keyID}. Remaining: {entry.count}");
            return true;
        }

        Debug.LogWarning($"Tried to use key: {keyID}, but none available.");
        return false;
    }
    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
}

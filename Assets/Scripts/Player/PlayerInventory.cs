using UnityEngine;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{
    public List<ShapeInventoryEntry> shapeInventory = new List<ShapeInventoryEntry>();
    public List<KeyInventoryEntry> keys;
    public KeyPaletteUI keyPaletteUI;
    
    public bool HasShape(TetrisShapeData shape) =>
        shapeInventory.Exists(entry => entry.shapeData == shape && entry.count > 0);

    public void AddShape(TetrisShapeData shape, int amount = 1)
    {
        var entry = shapeInventory.Find(e => e.shapeData == shape);
        if (entry != null)
        {
            entry.count += amount;
        }
        else
        {
            shapeInventory.Add(new ShapeInventoryEntry { shapeData = shape, count = amount });
        }
    }

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

    public void AddKey(string keyID)
    {
        var entry = keys.Find(k => k.keyID == keyID);
        if (entry != null)
        {
            entry.count++;
        }
        else
        {
            keys.Add(new KeyInventoryEntry { keyID = keyID, count = 1 });
        }

        if (keyPaletteUI != null)
        {
            keyPaletteUI.UpdateKey(keyID);
        }

        Debug.Log($"Picked up key: {keyID}. Total: {GetKeyCount(keyID)}");
    }

    public int GetKeyCount(string keyID)
    {
        var entry = keys.Find(k => k.keyID == keyID);
        return entry != null ? entry.count : 0;
    }

    public bool UseKey(string keyID)
    {
        var entry = keys.Find(k => k.keyID == keyID);
        if (entry != null && entry.count > 0)
        {
            entry.count--;
            
            if (keyPaletteUI != null)
            {
                keyPaletteUI.UpdateKey(keyID);
            }
            
            Debug.Log($"Used key: {keyID}. Remaining: {entry.count}");
            return true;
        }

        Debug.LogWarning($"Tried to use key: {keyID}, but none available.");
        return false;
    }
}

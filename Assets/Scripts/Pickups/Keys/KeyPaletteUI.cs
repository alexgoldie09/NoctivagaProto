using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UI handler for displaying all potential key icons based on the map.
/// Initializes key slots based on all required keys found in gate tiles,
/// and updates their counts in real time from the PlayerInventory.
/// </summary>
public class KeyPaletteUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject keyIconPrefab; // Prefab for key icon UI.
    public Transform container;      // Parent transform for key icons.
    public Sprite defaultKeyIcon;    // Fallback icon if none is defined.

    private Dictionary<string, KeyIconUI> keyIcons = new(); // Maps keyID to UI element.
    private PlayerInventory inventory; // Cached player inventory.

    /// <summary>
    /// Find PlayerInventory and initialize key icons on scene start.
    /// </summary>
    void Start()
    {
        inventory = FindAnyObjectByType<PlayerInventory>();
        InitFromMap();
        UpdateAll();
    }

    /// <summary>
    /// Scans the map for all Gate tiles and extracts required key IDs.
    /// Instantiates key icons for each unique key ID.
    /// </summary>
    void InitFromMap()
    {
        HashSet<string> allKeyIDs = new();

        foreach (var tile in GridManager.Instance.AllTiles)
        {
            if (tile.tileType == TileType.Gate && tile.requiredKeyIds != null)
            {
                foreach (var key in tile.requiredKeyIds)
                    allKeyIDs.Add(key);
            }
        }

        foreach (string keyID in allKeyIDs)
        {
            GameObject entryObj = Instantiate(keyIconPrefab, container);
            KeyIconUI icon = entryObj.GetComponent<KeyIconUI>();
            icon.SetDisplay(keyID, defaultKeyIcon, 0);
            keyIcons[keyID] = icon;
        }
    }

    /// <summary>
    /// Updates a specific key icon's count from inventory.
    /// </summary>
    public void UpdateKey(string keyID)
    {
        if (keyIcons.TryGetValue(keyID, out var icon))
        {
            icon.UpdateCount(inventory.GetKeyCount(keyID));
        }
    }

    /// <summary>
    /// Updates all key icon counts based on current inventory.
    /// </summary>
    public void UpdateAll()
    {
        foreach (var pair in keyIcons)
        {
            string keyID = pair.Key;
            pair.Value.UpdateCount(inventory.GetKeyCount(keyID));
        }
    }
}
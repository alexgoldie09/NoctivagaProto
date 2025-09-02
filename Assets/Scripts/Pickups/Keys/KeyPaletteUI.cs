using UnityEngine;
using System.Collections.Generic;

public class KeyPaletteUI : MonoBehaviour
{
    public GameObject keyIconPrefab;
    public Transform container;
    public Sprite defaultKeyIcon;

    private Dictionary<string, KeyIconUI> keyIcons = new();
    private PlayerInventory inventory;

    void Start()
    {
        inventory = FindAnyObjectByType<PlayerInventory>();
        InitFromMap();
        UpdateAll();
    }

    void InitFromMap()
    {
        HashSet<string> allKeyIDs = new();

        foreach (var tile in GridManager.Instance.AllTiles) // updated
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

    public void UpdateKey(string keyID)
    {
        if (keyIcons.TryGetValue(keyID, out var icon))
        {
            icon.UpdateCount(inventory.GetKeyCount(keyID));
        }
    }

    public void UpdateAll()
    {
        foreach (var pair in keyIcons)
        {
            string keyID = pair.Key;
            pair.Value.UpdateCount(inventory.GetKeyCount(keyID));
        }
    }
}
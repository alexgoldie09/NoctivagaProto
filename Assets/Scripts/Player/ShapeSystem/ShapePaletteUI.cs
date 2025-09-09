using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI component that displays the available shape inventory to the player.
/// Dynamically creates and updates shape icons based on the player's inventory.
/// </summary>
public class ShapePaletteUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Prefab used to represent a shape icon.")]
    public GameObject shapeIconPrefab;

    [Tooltip("Parent transform under which the icons will be instantiated.")]
    public Transform iconParent;

    private List<ShapeIconUI> iconInstances = new List<ShapeIconUI>(); // Current shape icons.
    private ShapePlacer shapePlacer; // Cached shape placer system.
    private PlayerInventory inventory; // Cached player inventory.

    // ─────────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        shapePlacer = FindAnyObjectByType<ShapePlacer>();
        inventory = FindAnyObjectByType<PlayerInventory>();

        if (shapePlacer == null || inventory == null)
        {
            Debug.LogError("ShapePlacer or PlayerInventory not found for ShapePaletteUI.");
            enabled = false;
            return;
        }

        CreateIcons();
    }

    private void Update()
    {
        UpdateIcons();
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates icons for each shape in the player's inventory.
    /// </summary>
    private void CreateIcons()
    {
        foreach (var shapeEntry in inventory.shapeInventory)
        {
            GameObject go = Instantiate(shapeIconPrefab, iconParent);
            ShapeIconUI icon = go.GetComponent<ShapeIconUI>();
            if (icon != null)
                iconInstances.Add(icon);
        }
    }

    /// <summary>
    /// Updates icon visuals based on current inventory count and selected shape.
    /// </summary>
    private void UpdateIcons()
    {
        for (int i = 0; i < iconInstances.Count; i++)
        {
            if (i >= inventory.shapeInventory.Count)
                continue;

            var entry = inventory.shapeInventory[i];
            bool isSelected = (i == shapePlacer.CurrentIndex) && Utilities.IsPlacementModeActive;

            iconInstances[i].SetData(
                entry.shapeData.previewSprite,
                entry.count,
                isSelected,
                entry.count > 0
            );
        }
    }
}

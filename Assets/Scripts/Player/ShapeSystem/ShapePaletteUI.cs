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
    
    private void OnEnable()
    {
        // If Start hasn't run yet, we might not have refs. We'll subscribe in Start too.
        if (inventory != null)
            inventory.OnShapeInventoryStructureChanged += RebuildIcons;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnShapeInventoryStructureChanged -= RebuildIcons;
    }

    /// <summary>
    /// Finds required references and builds the initial icon set.
    /// </summary>
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

        // Ensure we are subscribed
        inventory.OnShapeInventoryStructureChanged -= RebuildIcons;
        inventory.OnShapeInventoryStructureChanged += RebuildIcons;

        RebuildIcons();
    }

    /// <summary>
    /// Refreshes icon visuals each frame to reflect selection and counts.
    /// </summary>
    private void Update()
    {
        // Safety: if list size changes without firing the event, fix it once.
        if (inventory != null && iconInstances.Count != inventory.shapeInventory.Count)
        {
            RebuildIcons();
        }
        
        UpdateIcons();
    }
    
    /// <summary>
    /// Clears and recreates icons to match current shapeInventory structure.
    /// </summary>
    public void RebuildIcons()
    {
        // Destroy old icons
        for (int i = 0; i < iconInstances.Count; i++)
        {
            if (iconInstances[i] != null)
                Destroy(iconInstances[i].gameObject);
        }
        iconInstances.Clear();

        CreateIcons();
        UpdateIcons(); // immediate refresh so you see it right away
    }

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

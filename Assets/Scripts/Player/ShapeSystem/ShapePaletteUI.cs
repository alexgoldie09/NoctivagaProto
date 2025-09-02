using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ShapePaletteUI : MonoBehaviour
{
    public GameObject shapeIconPrefab;
    public Transform iconParent;

    private List<ShapeIconUI> iconInstances = new List<ShapeIconUI>();
    private ShapePlacer shapePlacer;
    private PlayerInventory inventory;

    void Start()
    {
        shapePlacer = FindAnyObjectByType<ShapePlacer>();
        inventory = FindAnyObjectByType<PlayerInventory>();

        if (shapePlacer == null || inventory == null)
        {
            Debug.LogError("ShapePlacer or PlayerInventory not found for ShapePaletteUI.");
            return;
        }

        CreateIcons();
    }

    void Update()
    {
        UpdateIcons();
    }

    void CreateIcons()
    {
        foreach (var shapeEntry in inventory.shapeInventory)
        {
            GameObject go = Instantiate(shapeIconPrefab, iconParent);
            ShapeIconUI icon = go.GetComponent<ShapeIconUI>();
            iconInstances.Add(icon);
        }
    }

    void UpdateIcons()
    {
        for (int i = 0; i < iconInstances.Count; i++)
        {
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
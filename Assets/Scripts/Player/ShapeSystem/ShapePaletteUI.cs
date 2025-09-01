using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ShapePaletteUI : MonoBehaviour
{
    public GameObject shapeIconPrefab;
    public Transform iconParent;

    private List<ShapeIconUI> iconInstances = new List<ShapeIconUI>();
    private ShapePlacer shapePlacer;

    void Start()
    {
        shapePlacer = FindAnyObjectByType<ShapePlacer>();
        if (shapePlacer == null)
        {
            Debug.LogError("ShapePlacer not found for palette UI.");
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
        foreach (var shapeEntry in shapePlacer.inventory)
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
            var entry = shapePlacer.inventory[i];
            bool isSelected = (i == shapePlacer.CurrentIndex) && Utilities.IsPlacementModeActive;

            iconInstances[i].SetData(entry.shapeData.previewSprite, entry.count, isSelected, entry.count > 0);
        }
    }
}
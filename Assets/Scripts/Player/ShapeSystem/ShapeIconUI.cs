using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Handles the UI icon representation for a shape in the shape selection panel.
/// This includes displaying the shape's sprite, remaining count, selection highlight,
/// and availability (e.g., if the player has none left).
/// </summary>
public class ShapeIconUI : MonoBehaviour
{
    public Image iconImage; // Image component that displays the shape's sprite.

    public TextMeshProUGUI countText; // Text element showing how many of this shape the player has left.

    public GameObject highlightFrame; // UI frame shown when this icon is currently selected.

    /// <summary>
    /// Updates the icon UI with the shape's visual and logical state.
    /// </summary>
    public void SetData(Sprite sprite, int count, bool isSelected, bool isAvailable)
    {
        iconImage.sprite = sprite;
        countText.text = count.ToString();

        // Set dimmed appearance if unavailable
        iconImage.color = isAvailable ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f); // Semi-transparent gray
        countText.color = isAvailable ? Color.white : Color.gray;

        // Toggle highlight only if this icon is selected and available
        highlightFrame.SetActive(isSelected && isAvailable);
    }
}

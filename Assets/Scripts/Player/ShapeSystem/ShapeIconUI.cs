using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ShapeIconUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI countText;
    public GameObject highlightFrame;

    public void SetData(Sprite sprite, int count, bool isSelected, bool isAvailable)
    {
        iconImage.sprite = sprite;
        countText.text = count.ToString();

        // Apply color based on availability
        iconImage.color = isAvailable ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f); // gray & semi-transparent
        countText.color = isAvailable ? Color.white : Color.gray;

        // Highlight border if selected and available
        highlightFrame.SetActive(isSelected && isAvailable);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KeyIconUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text keyIDText;
    public TMP_Text countText;

    public void SetDisplay(string keyID, Sprite icon, int count)
    {
        keyIDText.text = keyID;
        iconImage.sprite = icon;
        UpdateCount(count);
    }

    public void UpdateCount(int newCount)
    {
        countText.text = $"x {newCount}";
    }
}
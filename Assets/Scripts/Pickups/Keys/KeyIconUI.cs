using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the visual representation of a single key in the player's UI.
/// Displays the key ID, icon, and the current count owned by the player.
/// </summary>
public class KeyIconUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Image component that displays the icon of the key.")]
    public Image iconImage;

    [Tooltip("Text component that displays the key's identifier (e.g., 'RedKey').")]
    public TMP_Text keyIDText;

    [Tooltip("Text component that shows the number of this key the player currently holds.")]
    public TMP_Text countText;

    /// <summary>
    /// Sets the visual display of the key icon, ID label, and count.
    /// Called when the UI element is initialized or refreshed.
    /// </summary>
    /// <param name="keyID">Key identifier label to display.</param>
    /// <param name="icon">Sprite used for the key icon.</param>
    /// <param name="count">Number of keys owned.</param>
    public void SetDisplay(string keyID, Sprite icon, int count)
    {
        keyIDText.text = keyID;
        iconImage.sprite = icon;
        UpdateCount(count);
    }

    /// <summary>
    /// Updates the displayed count of how many keys the player currently holds.
    /// </summary>
    /// <param name="newCount">New key count to display.</param>
    public void UpdateCount(int newCount)
    {
        countText.text = $"x {newCount}";
    }
}
using TMPro;
using UnityEngine;

/// <summary>
/// Simple world-space interaction prompt bubble.
/// Displays a short text string (eg. "Press E to interact") and can be shown/hidden via a CanvasGroup,
/// making it suitable to place above interactable prefabs.
/// </summary>
public class WorldPromptBubble : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("CanvasGroup used to fade/show the prompt without disabling the GameObject.")]
    [SerializeField] private CanvasGroup group;
    [Tooltip("Label used to display the prompt text.")]
    [SerializeField] private TMP_Text label;
    
    // ─────────────────────────────────────────────
    #region Unity Lifecycle

    /// <summary>
    /// Ensures the prompt is hidden when the object initializes.
    /// </summary>
    private void Awake()
    {
        Hide();
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// Shows the prompt and updates the displayed text.
    /// </summary>
    /// <param name="text">Prompt text to display.</param>
    public void Show(string text)
    {
        if (label != null)
            label.text = text;

        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Hides the prompt.
    /// </summary>
    public void Hide()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    #endregion
}

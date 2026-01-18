using TMPro;
using UnityEngine;

public class WorldPromptBubble : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;

    private void Awake() => Hide();

    public void Show(string text)
    {
        if (label) label.text = text;

        if (group)
        {
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        else gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        else gameObject.SetActive(false);
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Self-contained component for a single level button in the Load scene.
/// Reads from SaveManager on Initialise() and populates its own UI.
/// Attach to each level button GameObject in the Load scene.
///
/// Inspector setup per button:
///   - Set levelIndex (1–10) and isBossLevel to match the level.
///   - Wire up the TMP labels and the button reference.
///   - LoadSceneUI will call Initialise() on Start — no manual wiring needed.
/// </summary>
public class LevelButtonUI : MonoBehaviour
{
    [Header("Level Config")]
    [Tooltip("1-based level index matching scene build order.")]
    [SerializeField] private int  levelIndex  = 1;
    [Tooltip("Mark true for boss levels (5 and 10). Score display is hidden for these.")]
    [SerializeField] private bool isBossLevel = false;

    [Header("UI References")]
    [SerializeField] private Button            levelButton;
    [SerializeField] private TextMeshProUGUI   levelNameText;
    [SerializeField] private TextMeshProUGUI   scoreText;
    [SerializeField] private TextMeshProUGUI   ratingText;
    [SerializeField] private TextMeshProUGUI   statusText;
    [Tooltip("Optional overlay or icon to show when the level is locked.")]
    [SerializeField] private GameObject        lockedOverlay;

    [Header("Display Settings")]
    [Tooltip("Prefix used in the level name label, e.g. 'Level' produces 'Level 1'.")]
    [SerializeField] private string levelNamePrefix = "Level";
    [Tooltip("Scene name to load when this button is pressed, e.g. 'Level_1'.")]
    [SerializeField] private string sceneName = "Level_1";

    // Cached by LoadSceneUI so it can route the transition correctly
    public string SceneName => sceneName;
    public int LevelIndex => levelIndex;

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by LoadSceneUI on Start. Reads save data and populates the button UI.
    /// The onClick listener is assigned here so LoadSceneUI can inject the transition callback.
    /// </summary>
    /// <param name="onPressed">Callback from LoadSceneUI that handles the scene transition.</param>
    public void Initialise(System.Action<string> onPressed)
    {
        SaveData       data  = SaveManager.LoadData();
        LevelSaveData  level = data.levels[levelIndex - 1];

        bool unlocked = IsUnlocked(data);

        // Level name label
        if (levelNameText != null)
            levelNameText.text = $"{levelNamePrefix} {levelIndex}";

        // Locked overlay
        if (lockedOverlay != null)
            lockedOverlay.SetActive(!unlocked);

        // Button interactability
        if (levelButton != null)
        {
            levelButton.interactable = unlocked;

            if (unlocked)
                levelButton.onClick.AddListener(() => onPressed(sceneName));
        }

        // Status / score / rating display
        if (!unlocked)
        {
            SetStatus("Locked");
            HideScoreDisplay();
            return;
        }

        if (!level.completed)
        {
            // Unlocked but not yet played
            SetStatus("Not Completed");
            HideScoreDisplay();
            return;
        }

        // Completed — show results
        SetStatus(isBossLevel ? "Complete" : "");

        if (!isBossLevel)
        {
            if (scoreText != null)
                scoreText.text = $"Best: {level.highScore}";

            if (ratingText != null)
                ratingText.text = $"Rating: {RatingLabel(level.highRating)}";
        }
        else
        {
            HideScoreDisplay();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    #region Helpers

    /// <summary>
    /// Level 1 is always unlocked. All others require the previous level to be completed.
    /// </summary>
    private bool IsUnlocked(SaveData data)
    {
        if (levelIndex == 1) return true;
        return data.levels[levelIndex - 2].completed;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void HideScoreDisplay()
    {
        if (scoreText  != null) scoreText.gameObject.SetActive(false);
        if (ratingText != null) ratingText.gameObject.SetActive(false);
    }

    private string RatingLabel(ScoreRating rating) => rating switch
    {
        ScoreRating.High   => "High",
        ScoreRating.Medium => "Medium",
        ScoreRating.Low    => "Low",
        _                  => "-"
    };

    #endregion
}
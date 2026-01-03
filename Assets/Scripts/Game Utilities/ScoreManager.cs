using UnityEngine;
using TMPro;

/// <summary>
/// Central manager for handling scoring, efficiency tracking, and feedback display.
/// - Tracks base score from rhythm timing.
/// - Tracks move count (movement, interactions, shape placements).
/// - Applies efficiency penalty at end of level (moves * penaltyPerMove).
/// - Displays live score, move count, and timing feedback in UI.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Scoring Settings")]
    [Tooltip("Points lost per move at final calculation.")]
    [SerializeField] private int penaltyPerMove = 5;

    [Header("UI References")]
    [Tooltip("UI text for displaying live score.")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Tooltip("UI text for displaying live move count.")]
    [SerializeField] private TextMeshProUGUI moveText;

    [Tooltip("UI text for showing rhythm feedback (Perfect/Good/Okay/Bad).")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    
    [Header("Feedback Settings")]
    [Tooltip("How long feedback text stays visible (seconds).")]
    [SerializeField] private float feedbackDuration = 0.5f;

    private float feedbackTimer = 0f;

    private int baseScore = 0;   // Raw score before penalty
    private int moveCount = 0;   // Number of moves performed
    private int finalScore = 0;  // Final score after penalty applied

    // ─────────────────────────────────────────────────────────────────────────────
    #region Unity lifecycle
    /// <summary>
    /// Establishes the singleton instance for score management.
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    /// <summary>
    /// Initializes UI values and clears any lingering feedback text.
    /// </summary>
    private void Start()
    {
        UpdateUI();
        if (feedbackText != null)
            feedbackText.text = ""; // clear on start
    }
    
    /// <summary>
    /// Counts down and hides rhythm feedback after its display duration.
    /// </summary>
    private void Update()
    {
        // Handle feedback timeout
        if (feedbackText != null && feedbackTimer > 0f)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f)
            {
                feedbackText.text = "";
            }
        }
    }
    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Score management
    /// <summary>
    /// Adds rhythm score to the base score and shows feedback.
    /// </summary>
    /// <param name="points">Points awarded for this action.</param>
    /// <param name="quality">Hit quality (Perfect, Good, Okay, Bad).</param>
    public void AddRhythmScore(int points, BeatHitQuality quality)
    {
        baseScore += points;
        UpdateUI();
        ShowFeedback(quality, points);
    }

    /// <summary>
    /// Registers a move (player movement, interaction, shape placement).
    /// </summary>
    public void RegisterMove()
    {
        moveCount++;
        UpdateUI();
    }

    /// <summary>
    /// Finalizes score at the end of the level by applying penalties.
    /// Call this when the player wins or completes a map.
    /// </summary>
    public void FinalizeScore()
    {
        finalScore = Mathf.Max(0, baseScore - (moveCount * penaltyPerMove));
        // TODO: hook into win screen UI to display results
        Debug.Log($"Level Complete! Base Score = {baseScore}, Moves = {moveCount}, Final Score = {finalScore}");
    }

    /// <summary>
    /// Resets score and moves, used when restarting levels.
    /// </summary>
    public void ResetScore()
    {
        baseScore = 0;
        moveCount = 0;
        finalScore = 0;
        UpdateUI();

        if (feedbackText != null)
            feedbackText.text = "";
    }
    
    /// <summary>
    /// Returns the last computed final score after penalties.
    /// </summary>
    public int GetFinalScore() => finalScore;

    /// <summary>
    /// Returns the number of registered moves.
    /// </summary>
    public int GetMoveCount() => moveCount;
    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region UI
    /// <summary>
    /// Updates score and move count on the UI.
    /// </summary>
    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {baseScore}";

        if (moveText != null)
            moveText.text = $"Moves: {moveCount}";
    }

    /// <summary>
    /// Displays rhythm hit quality feedback with message and color.
    /// </summary>
    /// <param name="quality">Timing grade for the hit.</param>
    /// <param name="points">Points awarded for the hit.</param>
    private void ShowFeedback(BeatHitQuality quality, int points)
    {
        if (feedbackText == null) return;

        string message = "";
        Color color = Color.white;

        switch (quality)
        {
            case BeatHitQuality.Perfect:
                message = $"PERFECT! +{points}";
                color = Color.green;
                break;
            case BeatHitQuality.Good:
                message = $"GOOD! +{points}";
                color = Color.yellow;
                break;
            case BeatHitQuality.Okay:
                message = $"Okay +{points}";
                color = Color.cyan;
                break;
            case BeatHitQuality.Bad:
                message = "Bad";
                color = Color.gray;
                break;
        }

        feedbackText.text = message;
        feedbackText.color = color;
        feedbackTimer = feedbackDuration; // reset timer
    }
    #endregion
}

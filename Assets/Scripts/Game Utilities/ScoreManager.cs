using UnityEngine;
using TMPro;
using UnityEditor;

/// <summary>
/// Rating tier awarded based on final score after penalties.
/// </summary>
public enum ScoreRating { Low, Medium, High }

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
    [Tooltip("Points lost per move within the expected move budget (Tier 1).")]
    [SerializeField] private int penaltyPerMove = 5;
    [Tooltip("Expected number of moves to complete this map. " +
             "Moves up to this count use the base penalty rate. " +
             "Set higher for larger maps so the escalating tiers stay fair.")]
    [SerializeField] private int movePenaltyThreshold = 30;
    [Tooltip("Penalty multiplier for moves between 1× and 2× the threshold (Tier 2).")]
    [SerializeField] private float tier2Multiplier = 1.5f;
    [Tooltip("Penalty multiplier for moves beyond 2× the threshold (Tier 3).")]
    [SerializeField] private float tier3Multiplier = 2.0f;
    [Tooltip("Scores above this threshold are considered high level gameplay")]
    [SerializeField, Range(0,1)] private float highScoreRatio = 0.75f;
    [Tooltip("Scores above this threshold and between high, are considered medium level gameplay")]
    [SerializeField, Range(0,1)] private float mediumScoreRatio = 0.40f;
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
    /// Finalizes score at the end of the level by applying tiered move penalties.
    /// Call this when the player wins or completes a map.
    /// </summary>
    public void FinalizeScore()
    {
        int penalty = CalculateMovePenalty();
        finalScore = Mathf.Max(0, baseScore - penalty); // Ensure score doesn't go negative
        Debug.Log($"Level Complete! Base Score = {baseScore}, Moves = {moveCount}, Penalty = {penalty}, Final Score = {finalScore}");
    }

    /// <summary>
    /// Calculate the total move penalty using escalating tiers beyond the threshold.
    /// Tier 1 (moves 1-threshold): penaltyPerMove x 1
    /// Tier 2 (threshold+1 to 2×threshold): penaltyPerMove x tier2Multiplier
    /// Tier 3 (beyond 2×threshold): penaltyPerMove x tier3Multiplier
    /// </summary>
    /// <returns></returns>
    private int CalculateMovePenalty()
    {
        int tier1Moves = Mathf.Min(moveCount, movePenaltyThreshold);
        int tier2Moves = Mathf.Clamp(moveCount - movePenaltyThreshold, 0, movePenaltyThreshold);
        int tier3Moves = Mathf.Max(0, moveCount - movePenaltyThreshold * 2);
        
        return Mathf.RoundToInt(tier1Moves * penaltyPerMove +
                                  tier2Moves * penaltyPerMove * tier2Multiplier +
                                  tier3Moves * penaltyPerMove * tier3Multiplier);
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
    
    /// <summary>
    /// Returns the original score.
    /// </summary>
    public int GetBaseScore() => baseScore;
    
    /// <summary>
    /// Determines the score rating (Low, Medium, High) based on the final score relative to the base score.
    /// Uses the ratio of finalScore to baseScore and compares it against defined thresholds to assign a rating.
    /// High (>= 75%) - mostly on-beat few wasted moves
    /// Mid (>= 40%) - average play
    /// Low (< 40%) - many off-beat hits or many moves leading to penalties
    /// </summary>
    public ScoreRating GetScoreRating()
    {
        if (baseScore <= 0) return ScoreRating.Low;
        
        float ratio = (float)finalScore / baseScore;
        if (ratio >=  highScoreRatio) return ScoreRating.High;
        return ratio >=  mediumScoreRatio ? ScoreRating.Medium : ScoreRating.Low;
    }
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
            case BeatHitQuality.OnBeat:
                message = $"ON BEAT! +{points}";
                color = Color.green;
                break;
            case BeatHitQuality.OffBeat:
                message = "OFF BEAT! +0";
                color = Color.gray;
                break;
        }

        feedbackText.text = message;
        feedbackText.color = color;
        feedbackTimer = feedbackDuration; // reset timer
    }
    
    /// <summary>
    /// Provides access to the live score label for UI composition.
    /// </summary>
    public TextMeshProUGUI ScoreTextUI => scoreText;

    /// <summary>
    /// Provides access to the live move counter label for UI composition.
    /// </summary>
    public TextMeshProUGUI MoveTextUI => moveText;
    #endregion
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Controls the win screen UI.
/// Displays final score/moves and provides buttons for restart or main menu,
/// with a smooth fade-in effect.
/// </summary>
public class WinScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI finalMovesText;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Score Rating Image")]
    [Tooltip("The image that will be displayed when the score is displayed")]
    [SerializeField] private Image scoreRatingImage;
    [Tooltip("Sprite shown when player's score efficiency is low")]
    [SerializeField] private Sprite lowScoreSprite;
    [Tooltip("Sprite shown when player's score efficiency is medium")]
    [SerializeField] private Sprite mediumScoreSprite;
    [Tooltip("Sprite shown when player's score efficiency is high")]
    [SerializeField] private Sprite highScoreSprite;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 2f;
    
    [Header("Next Level Settings")]
    [SerializeField] private string nextLevelName;

    private void Start()
    {
        // Ensure win screen starts hidden
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // Pull final values from ScoreManager
        if (ScoreManager.Instance != null)
        {
            finalScoreText.text = $"Base Score: {ScoreManager.Instance.GetBaseScore()}" +
                                  $"\nFinal Score: {ScoreManager.Instance.GetFinalScore()}";
            finalMovesText.text = $"Moves taken: {ScoreManager.Instance.GetMoveCount()}";
            
            // Choose rating image based on score efficiency
            if (scoreRatingImage != null)
            {
                Sprite ratingSprite = ScoreManager.Instance.GetScoreRating() switch
                {
                    ScoreRating.High => highScoreSprite,
                    ScoreRating.Medium => mediumScoreSprite,
                    ScoreRating.Low => lowScoreSprite,
                    _ => null
                };
                if (ratingSprite != null)
                    scoreRatingImage.sprite = ratingSprite;
            }
        }
        else
        {
            if (finalScoreText != null) 
                finalScoreText.text = "";
            if (finalMovesText != null) 
                finalMovesText.text = "";
            if (scoreRatingImage != null)
                scoreRatingImage.sprite = null;
        }

        // Reset state
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Start fade-in
            StartCoroutine(FadeIn());
        }
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // use unscaled so it works while Time.timeScale = 0
            float normalized = Mathf.Clamp01(t / fadeDuration);
            canvasGroup.alpha = normalized;
            yield return null;
        }

        // Enable interaction after fade
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Restart the current level.
    /// </summary>
    public void RestartLevel()
    {
        Utilities.UnfreezeGame();
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
    
    /// <summary>
    /// Go to the next level.
    /// </summary>
    public void NextLevel()
    {
        Utilities.UnfreezeGame();
        SceneManager.LoadScene(nextLevelName);
    }

    /// <summary>
    /// Load the main menu scene (set the scene name in Build Settings).
    /// </summary>
    public void LoadMainMenu()
    {
        Utilities.UnfreezeGame();
        SceneManager.LoadScene("MainMenu"); // replace with your actual main menu scene name
    }
    
    /// <summary>
    /// Quits the game.
    /// </summary>
    public void QuitGame()
    {
        Utilities.QuitGame();
    }
}

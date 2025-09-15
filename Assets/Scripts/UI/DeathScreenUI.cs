using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Controls the death screen UI.
/// Provides buttons for restart or main menu, with a smooth fade-in.
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 1f;

    private void Start()
    {
        // Ensure this screen starts inactive and invisible
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Start fade-in when activated
            StartCoroutine(FadeIn());
        }
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // use unscaled so fade still works while frozen
            float normalized = Mathf.Clamp01(t / fadeDuration);
            canvasGroup.alpha = normalized;
            yield return null;
        }

        // Enable UI interactions after fade is complete
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
    /// Load the main menu scene (set the scene name in Build Settings).
    /// </summary>
    public void LoadMainMenu()
    {
        Utilities.UnfreezeGame();
        SceneManager.LoadScene("MainMenu"); // replace with your main menu scene name
    }
    
    /// <summary>
    /// Quits the game.
    /// </summary>
    public void QuitGame()
    {
        Utilities.QuitGame();
    }
}

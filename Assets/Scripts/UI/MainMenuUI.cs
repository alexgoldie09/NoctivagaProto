using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the main menu UI: play, quit, and background pulse effect.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [Header("Background Pulse")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color pulseColorA = Color.blue;
    [SerializeField] private Color pulseColorB = Color.magenta;
    [SerializeField] private float pulseSpeed = 2f; // higher = faster pulse

    private void Start()
    {
        if (playButton != null)
            playButton.onClick.AddListener(PlayGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    private void Update()
    {
        if (backgroundImage != null)
        {
            // t oscillates smoothly between 0 and 1
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            backgroundImage.color = Color.Lerp(pulseColorA, pulseColorB, t);
        }
    }

    private void PlayGame()
    {
        // Replace with your actual game scene name
        SceneManager.LoadScene("LevelScene");
    }

    private void QuitGame()
    {
        Utilities.QuitGame();
    }
}
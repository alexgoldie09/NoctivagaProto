using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the main menu UI.
///
/// Buttons:
///   - New Game:    Clears all save data and loads Level_1.
///   - Load Level:  Loads the Load scene. Disabled if no save data exists.
///   - Credits:     Loads the Credits scene.
///   - Quit:        Exits the application.
///
/// Visual:
///   - Background image pulses between two colours.
///   - A full-screen animated image sits on top. When any button is pressed,
///     its Animator trigger fires and the scene loads after the clip finishes.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("Name of the first level scene. Loaded by New Game.")]
    [SerializeField] private string levelOneScene = "Level_1";
    [Tooltip("Name of the load/level-select scene.")]
    [SerializeField] private string loadScene = "LoadScene";
    [Tooltip("Name of the credits scene.")]
    [SerializeField] private string creditsScene = "CreditsScene";

    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadLevelButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Transition")]
    [Tooltip("Animator on the full-screen image that plays when a button is pressed.")]
    [SerializeField] private Animator transitionAnimator;
    [Tooltip("Animator trigger parameter name to fire on button press.")]
    [SerializeField] private string transitionTrigger = "Play";
    [Tooltip("Duration of the transition animation in seconds. Must match the clip length.")]
    [SerializeField] private float transitionDuration = 1f;

    [Header("Background Pulse")]
    [SerializeField] private Image  backgroundImage;
    [SerializeField] private Color  pulseColorA = Color.blue;
    [SerializeField] private Color  pulseColorB = Color.magenta;
    [Tooltip("Higher = faster pulse.")]
    [SerializeField] private float  pulseSpeed  = 2f;

    private bool isTransitioning = false;

    // ─────────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Start()
    {
        SetupButtons();
    }

    private void Update()
    {
        PulseBackground();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Setup

    private void SetupButtons()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGame);

        if (loadLevelButton != null)
        {
            // Disable if no levels have been completed yet
            bool hasSaveData = HasAnySaveData();
            loadLevelButton.interactable = hasSaveData;
            loadLevelButton.onClick.AddListener(OnLoadLevel);
        }

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCredits);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);
    }

    /// <summary>
    /// Returns true if at least one level has been completed in the save file.
    /// </summary>
    private bool HasAnySaveData()
    {
        SaveData data = SaveManager.LoadData();
        foreach (var level in data.levels)
            if (level.completed) return true;
        return false;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Button Handlers

    private void OnNewGame()
    {
        SaveManager.ClearAllData();
        StartTransition(levelOneScene);
    }

    private void OnLoadLevel()
    {
        StartTransition(loadScene);
    }

    private void OnCredits()
    {
        StartTransition(creditsScene);
    }

    private void OnQuit()
    {
        // Transition then quit — or quit immediately if no animator assigned
        if (transitionAnimator != null)
            StartCoroutine(QuitAfterTransition());
        else
            Utilities.QuitGame();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Transitions

    /// <summary>
    /// Fires the transition animation and loads the target scene once it finishes.
    /// Ignores subsequent button presses while transitioning.
    /// </summary>
    private void StartTransition(string sceneName)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToScene(sceneName));
    }

    private IEnumerator TransitionToScene(string sceneName)
    {
        isTransitioning = true;
        
        AudioManager.Instance?.PlaySFX("obstacle_click", 0.4f);

        if (transitionAnimator != null)
        {
            transitionAnimator.SetTrigger(transitionTrigger);
            yield return new WaitForSeconds(transitionDuration);
        }
        
        if (sceneName == loadScene || sceneName == creditsScene)
        {
            // Don't unfreeze or stop music when going to load screen, since the player can return to the menu without reloading
            SceneManager.LoadScene(sceneName);
            yield break;
        }
        
        Utilities.UnfreezeGame();
        RhythmManager.Instance?.Stop(); // clear the track before reload
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator QuitAfterTransition()
    {
        isTransitioning = true;
        
        AudioManager.Instance?.PlaySFX("obstacle_click", 0.4f);
        
        transitionAnimator.SetTrigger(transitionTrigger);
        yield return new WaitForSeconds(transitionDuration);
        Utilities.QuitGame();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Background Pulse

    private void PulseBackground()
    {
        if (backgroundImage == null) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
        backgroundImage.color = Color.Lerp(pulseColorA, pulseColorB, t);
    }

    #endregion
}
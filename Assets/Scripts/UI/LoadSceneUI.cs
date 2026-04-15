using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene manager for the Load / Level Select screen.
/// Finds all LevelButtonUI components in the scene, initialises them with save data,
/// and handles scene transitions with an animator.
///
/// Scene setup:
///   - Place 10 LevelButtonUI GameObjects in your fixed layout.
///   - Assign the back button, transition animator, and transition settings below.
///   - No need to manually wire level buttons — Initialise() is called automatically.
/// </summary>
public class LoadSceneUI : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("Main menu scene to return to when back is pressed.")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Buttons")]
    [SerializeField] private Button backButton;

    [Header("Transition")]
    [Tooltip("Animator on the full-screen image that plays when a button is pressed.")]
    [SerializeField] private Animator transitionAnimator;
    [Tooltip("Animator trigger parameter name to fire on button press.")]
    [SerializeField] private string   transitionTrigger   = "Play";
    [Tooltip("Duration of the transition animation in seconds. Must match the clip length.")]
    [SerializeField] private float    transitionDuration  = 1f;

    private bool isTransitioning = false;

    // ─────────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Start()
    {
        InitialiseLevelButtons();

        if (backButton != null)
            backButton.onClick.AddListener(() => StartTransition(mainMenuScene));
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Initialisation

    /// <summary>
    /// Finds every LevelButtonUI in the scene and initialises it with the
    /// transition callback. Order in the hierarchy doesn't matter — each button
    /// configures itself from its own levelIndex.
    /// </summary>
    private void InitialiseLevelButtons()
    {
        LevelButtonUI[] buttons = FindObjectsByType<LevelButtonUI>(FindObjectsSortMode.None);

        foreach (var button in buttons)
            button.Initialise(OnLevelButtonPressed);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Button Handlers

    private void OnLevelButtonPressed(string sceneName)
    {
        StartTransition(sceneName);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Transitions

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

        if (sceneName == mainMenuScene)
        {
            SceneManager.LoadScene(sceneName);
            yield break;
        }
        
        Utilities.UnfreezeGame();
        RhythmManager.Instance?.Stop(); // clear the track before reload
        SceneManager.LoadScene(sceneName);
    }

    #endregion
}
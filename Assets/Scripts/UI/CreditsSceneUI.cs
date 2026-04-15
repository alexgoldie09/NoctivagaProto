using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the Credits UI.
///
/// Buttons:
///   - Return to main menu: Loads to main menu scene
///
/// Visual:
///   - A full-screen animated image sits on top. When any button is pressed,
///     its Animator trigger fires and the scene loads after the clip finishes.
/// </summary>
public class CreditsSceneUI : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("Name of the main menu scene.")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Button")]
    [SerializeField] private Button mainMenuButton;

    [Header("Transition")]
    [Tooltip("Animator on the full-screen image that plays when a button is pressed.")]
    [SerializeField] private Animator transitionAnimator;
    [Tooltip("Animator trigger parameter name to fire on button press.")]
    [SerializeField] private string transitionTrigger = "Play";
    [Tooltip("Duration of the transition animation in seconds. Must match the clip length.")]
    [SerializeField] private float transitionDuration = 0.83f;

    private bool isTransitioning = false;

    // ─────────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Start()
    {
        SetupButtons();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Setup

    private void SetupButtons()
    {
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenu);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Button Handlers
    private void OnMainMenu()
    {
        StartTransition(mainMenuScene);
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

        SceneManager.LoadScene(sceneName);
    }
    #endregion
}
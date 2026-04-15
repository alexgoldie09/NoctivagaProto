using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the tab-style display switcher inside the pause menu panel.
/// Wire up each of the three buttons to call ShowDisplay(0), ShowDisplay(1),
/// and ShowDisplay(2) respectively via the Inspector OnClick events.
/// One display is open by default when the pause menu first appears.
/// Display states persist across pause/resume cycles since we only toggle
/// child GameObject active states — the parent is controlled by GameManager.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Displays")]
    [Tooltip("The three display GameObjects shown one at a time. " +
             "Index 0 = default open display.")]
    [SerializeField] private GameObject[] displays;

    [Header("Default Display")]
    [Tooltip("Index of the display to show when the pause menu first opens.")]
    [SerializeField] private int defaultDisplayIndex = 0;

    [Header("Volume Sliders")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;

    private void Start()
    {
        InitialiseVolumeSliders();
        ShowDisplay(defaultDisplayIndex);
    }

    /// <summary>
    /// Seeds each slider from the saved PlayerPrefs value and registers
    /// its listener so changes are forwarded to AudioManager.
    /// </summary>
    private void InitialiseVolumeSliders()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[PauseMenuUI] AudioManager instance not found. Volume sliders will not function.");
            return;
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = AudioManager.Instance.GetSFXVolume();
            sfxSlider.onValueChanged.AddListener(AudioManager.Instance.SetSFXVolume);
        }

        if (musicSlider != null)
        {
            musicSlider.value = AudioManager.Instance.GetMusicVolume();
            musicSlider.onValueChanged.AddListener(AudioManager.Instance.SetMusicVolume);
        }
    }

    /// <summary>
    /// Activates the display at the given index and deactivates all others.
    /// Wire each tab button's OnClick to call this with its respective index.
    /// </summary>
    public void ShowDisplay(int index)
    {
        for (int i = 0; i < displays.Length; i++)
        {
            if (displays[i] != null)
            {
                displays[i].SetActive(i == index);
                AudioManager.Instance?.PlaySFX("obstacle_click", 0.4f);
            }
        }
    }

    /// <summary>
    /// Restarts the current level.
    /// </summary>
    public void RestartLevel()
    {
        AudioManager.Instance?.PlaySFX("obstacle_click", 0.4f);
        Utilities.UnfreezeGame();
        RhythmManager.Instance?.Stop(); // clear the track before reload
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    /// <summary>
    /// Quits the game.
    /// </summary>
    public void QuitGame()
    {
        AudioManager.Instance?.PlaySFX("obstacle_click", 0.4f);
        Utilities.QuitGame();
    }
}
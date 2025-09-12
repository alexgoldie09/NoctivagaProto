using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Central game flow manager.
/// Handles level reset and (placeholder) death logic.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private void Awake()
    {
        if (Instance == null) 
            Instance = this;
        else 
            Destroy(gameObject);
    }

    /// <summary>
    /// Called when the player is killed by an enemy.
    /// Hides the player, waits, then resets the level.
    /// </summary>
    public void PlayerKilled(PlayerController player)
    {
        if (player == null) return;

        // Hide player object
        player.gameObject.SetActive(false);

        // Start delayed reset
        StartCoroutine(ResetAfterDelay(5f));
    }

    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Reload current scene
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
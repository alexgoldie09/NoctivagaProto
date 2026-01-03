using UnityEngine;

/// <summary>
/// Marks the win condition and notifies the game manager on player collision.
/// </summary>
public class WaifuGoal : MonoBehaviour
{
    /// <summary>
    /// Detects player entry and triggers the win flow before removing the goal.
    /// </summary>
    /// <param name="other">Collider that entered the trigger.</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance.PlayerWon();
            Destroy(gameObject);
        }
    }
}
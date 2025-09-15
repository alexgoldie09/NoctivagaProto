using UnityEngine;

/// <summary>
/// Marks the win condition. When the player collides with this object,
/// it triggers the GameManager win sequence.
/// </summary>
public class WaifuGoal : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance.PlayerWon();
            Destroy(gameObject);
        }
    }
}
using UnityEngine;

/// <summary>
/// Destroys a swipe visual effect after a short lifetime.
/// </summary>
public class SwipeVFX : MonoBehaviour
{
    [SerializeField] private float lifetime = 1.5f;

    /// <summary>
    /// Schedules the effect object for cleanup.
    /// </summary>
    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
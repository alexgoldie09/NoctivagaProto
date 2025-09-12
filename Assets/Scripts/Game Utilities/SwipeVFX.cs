// SwipeVFX.cs
using UnityEngine;

public class SwipeVFX : MonoBehaviour
{
    [SerializeField] private float lifetime = 1.5f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
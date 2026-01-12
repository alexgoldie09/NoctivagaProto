using UnityEngine;

public class SelfDestroy : MonoBehaviour
{
    [Header("Self Destruct Settings")]
    [SerializeField] float destroyDelay = 1.5f;    // set ≈ your effect length

    void Start()
    {
        Destroy(gameObject, destroyDelay);
    }
}

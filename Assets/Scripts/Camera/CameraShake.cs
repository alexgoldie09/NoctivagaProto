using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Singleton camera shake helper that triggers Cinemachine impulse bursts.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [SerializeField] private CinemachineImpulseSource impulseSource;
    
    /// <summary>
    /// Caches the singleton instance and resolves the impulse source if not assigned.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();
    }
    
    /// <summary>
    /// Emits an impulse with a randomized 2D direction and configurable force.
    /// </summary>
    /// <param name="force">Strength multiplier applied to the impulse velocity.</param>
    public void Shake(float force = 1f)
    {
        if (impulseSource == null) 
            return;

        Vector2 dir2 = Random.insideUnitCircle.normalized;   // random XY direction
        Vector3 vel = new Vector3(dir2.x, dir2.y, 0f) * force;

        impulseSource.GenerateImpulseWithVelocity(vel);
    }
}
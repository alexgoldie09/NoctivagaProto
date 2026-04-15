using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Singleton camera shake helper that triggers Cinemachine impulse bursts.
/// Coalesces multiple Shake() calls within the same frame into a single impulse,
/// using the strongest requested force. Prevents stacking from simultaneous enemies.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [SerializeField] private CinemachineImpulseSource impulseSource;

    private float pendingForce;
    private bool shakeQueued;

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
    /// Queues a shake request for this frame. Multiple calls are coalesced —
    /// only the strongest force will fire, once, in LateUpdate.
    /// </summary>
    /// <param name="force">Strength multiplier applied to the impulse velocity.</param>
    public void Shake(float force = 1f)
    {
        pendingForce = Mathf.Max(pendingForce, force);
        shakeQueued = true;
    }

    /// <summary>
    /// Fires at most one impulse per frame using the strongest queued force.
    /// </summary>
    private void LateUpdate()
    {
        if (!shakeQueued) return;

        if (impulseSource != null)
        {
            Vector2 dir2 = Random.insideUnitCircle.normalized;
            impulseSource.GenerateImpulseWithVelocity(
                new Vector3(dir2.x, dir2.y, 0f) * pendingForce
            );
        }

        pendingForce = 0f;
        shakeQueued = false;
    }
}
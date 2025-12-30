using UnityEngine;
using Unity.Cinemachine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [SerializeField] private CinemachineImpulseSource impulseSource;

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

    public void Shake(float force = 1f)
    {
        if (impulseSource == null) 
            return;

        Vector2 dir2 = Random.insideUnitCircle.normalized;   // random XY direction
        Vector3 vel = new Vector3(dir2.x, dir2.y, 0f) * force;

        impulseSource.GenerateImpulseWithVelocity(vel);
    }
}
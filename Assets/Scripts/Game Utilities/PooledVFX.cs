using UnityEngine;

/// <summary>
/// Add to any VFX prefab that should be managed by VFXPoolManager.
/// Automatically returns itself to the pool when its particle system finishes.
/// </summary>
public class PooledVFX : MonoBehaviour
{
    private ParticleSystem ps;
    private GameObject sourcePrefab;

    public void Init(GameObject prefab)
    {
        sourcePrefab = prefab;
        ps = GetComponent<ParticleSystem>();
    }

    private void Update()
    {
        if (ps == null || sourcePrefab == null) return;

        // Wait until the particle system has started AND finished
        if (ps.time > 0f && !ps.IsAlive())
            VFXPoolManager.Instance?.Return(sourcePrefab, gameObject);
    }
}
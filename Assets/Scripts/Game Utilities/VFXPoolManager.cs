using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton object pool for short-lived VFX prefabs.
/// Eliminates per-beat Instantiate/Destroy GC pressure from enemies and player.
/// </summary>
public class VFXPoolManager : MonoBehaviour
{
    public static VFXPoolManager Instance { get; private set; }

    [Tooltip("How many instances of each VFX to pre-warm on startup.")]
    [SerializeField] private int prewarmCount = 5;

    [Tooltip("VFX prefabs to pre-warm. Drag your swipe, slam, melee prefabs here.")]
    [SerializeField] private GameObject[] prewarmPrefabs;

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Pre-warm so the first beats don't cause any allocations
        foreach (var prefab in prewarmPrefabs)
        {
            if (prefab == null) continue;
            for (int i = 0; i < prewarmCount; i++)
                Return(prefab, CreateNew(prefab));
        }
    }

    /// <summary>
    /// Gets a pooled instance of the prefab, activates it at the given position.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return null;

        if (!pools.TryGetValue(prefab, out var queue))
            queue = pools[prefab] = new Queue<GameObject>();

        // Drain any destroyed references before trusting the next item
        GameObject obj = null;
        while (queue.Count > 0)
        {
            var candidate = queue.Dequeue();
            if (candidate != null) // destroyed objects fail this check
            {
                obj = candidate;
                break;
            }
        }

        if (obj == null)
            obj = CreateNew(prefab);

        obj.transform.position = position;
        obj.SetActive(true);

        var ps = obj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Play();
        }

        return obj;
    }

    /// <summary>
    /// Returns a used instance back to the pool. Called automatically by PooledVFX.
    /// </summary>
    public void Return(GameObject prefab, GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);

        if (!pools.TryGetValue(prefab, out var queue))
            queue = pools[prefab] = new Queue<GameObject>();

        queue.Enqueue(obj);
    }

    private GameObject CreateNew(GameObject prefab)
    {
        var obj = Instantiate(prefab);
        obj.SetActive(false);

        // Wire up the auto-return component
        var pooled = obj.GetComponent<PooledVFX>() ?? obj.AddComponent<PooledVFX>();
        pooled.Init(prefab);

        return obj;
    }
}
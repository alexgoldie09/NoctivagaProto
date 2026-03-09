using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public abstract class PickupBase : MonoBehaviour
{
    [Header("Bobbing")]
    [Tooltip("If enabled, the pickup will bob up and down.")]
    [SerializeField] private bool enableBobbing = true;

    [Tooltip("How high the bob moves (world units).")]
    [SerializeField] private float bobAmplitude = 0.1f;

    [Tooltip("How fast the bob cycles (cycles per second).")]
    [SerializeField] private float bobFrequency = 0.4f;

    [Tooltip("Randomizes the bob start so multiple pickups don't move in sync.")]
    [SerializeField] private bool randomizePhase = true;
    
    [Header("Self Destruct Settings")]
    [Tooltip("Is this pickup able to destroy itself.")]
    [SerializeField] private bool enableSelfDestruct = true;
    [Tooltip("Time for the effect or object to destroy itself.")]
    [SerializeField] float destroyDelay = 1.5f; 

    private Vector3 baseWorldPos;
    private float phaseOffset;
    private Collider2D col;
    
    public void Start()
    {
        // Ensure trigger collider
        col = GetComponent<Collider2D>();
        if (col != null) 
            col.isTrigger = true;

        var grid = TilemapGridManager.Instance;
        if (grid == null)
        {
            Debug.LogWarning("[Pickup] TilemapGridManager.Instance not found (cannot snap).");
            return;
        }

        Vector3Int cell = grid.WorldToCell(transform.position);
        transform.position = grid.CellToWorldCenter(cell);
        
        // Cache snapped base position for bobbing.
        baseWorldPos = transform.position;

        if (randomizePhase)
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        
        if(enableSelfDestruct)
            Destroy(gameObject, destroyDelay);
    }
    
    public void Update()
    {
        if (!enableBobbing)
            return;

        // Sin wave in world-space (keeps it simple).
        float t = Time.time * bobFrequency * Mathf.PI * 2f + phaseOffset;
        float yOffset = Mathf.Sin(t) * bobAmplitude;

        transform.position = baseWorldPos + (Vector3.up * yOffset);
    }
    
    protected virtual void OnTriggerEnter2D(Collider2D collision) {}

    private void OnDestroy()
    {
        if(AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("pick_up", 0.2f);
    }
}

using UnityEngine;

/// <summary>
/// Pickup that adds a shape to the player's shape inventory.
/// - Snaps itself to the nearest tile cell center on Start.
/// - On player trigger, adds the shape (and amount) to PlayerInventory.
/// - PlayerInventory/ShapePaletteUI will update automatically (event for new shapes,
///   per-frame icon refresh for count changes).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShapePickup : MonoBehaviour
{
    [Header("Shape Pickup")]
    [SerializeField] private TetrisShapeData shapeData;
    [SerializeField] private int amount = 1;
    
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

    private void Start()
    {
        // Ensure trigger collider
        var col = GetComponent<Collider2D>();
        if (col != null) 
            col.isTrigger = true;

        var grid = TilemapGridManager.Instance;
        if (grid == null)
        {
            Debug.LogWarning("[ShapePickup] TilemapGridManager.Instance not found (cannot snap).");
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
    
    private void Update()
    {
        if (!enableBobbing)
            return;

        // Sin wave in world-space (keeps it simple).
        float t = Time.time * bobFrequency * Mathf.PI * 2f + phaseOffset;
        float yOffset = Mathf.Sin(t) * bobAmplitude;

        transform.position = baseWorldPos + (Vector3.up * yOffset);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (shapeData == null)
        {
            Debug.LogWarning("[ShapePickup] No shapeData assigned.");
            return;
        }

        var inv = other.GetComponent<PlayerInventory>();
        if (inv == null)
        {
            Debug.LogWarning("[ShapePickup] Player has no PlayerInventory component.");
            return;
        }

        int addAmount = Mathf.Max(1, amount);
        inv.AddShape(shapeData, addAmount);

        // Optional: audio / VFX hooks later
        Destroy(gameObject);
    }
}
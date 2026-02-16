using UnityEngine;

/// <summary>
/// Handles the pickup logic for powerups on the grid.
/// Powerups activate an immediate effect (e.g., half-time rhythm, shadow mode)
/// instead of being stored in an inventory. They are aligned to grid positions.
/// When the player touches the powerup, it activates and the object is destroyed.
/// </summary>
public class PowerupPickup : MonoBehaviour
{
    [Header("Powerup Properties")]
    [Tooltip("Type of powerup this pickup will activate.")]
    public PowerupType type = PowerupType.HalfTime;

    [Tooltip("Duration of the powerup effect in seconds.")]
    public float duration = 5f;

    /// <summary>
    /// Supported powerup effect types.
    /// </summary>
    public enum PowerupType { HalfTime, ShadowMode, Melee }

    [Header("Bobbing")]
    [Tooltip("If enabled, the pickup will bob up and down.")]
    [SerializeField] private bool enableBobbing = true;

    [Tooltip("How high the bob moves (world units).")]
    [SerializeField] private float bobAmplitude = 0.08f;

    [Tooltip("How fast the bob cycles (cycles per second).")]
    [SerializeField] private float bobFrequency = 1.5f;

    [Tooltip("Randomizes the bob start so multiple pickups don't move in sync.")]
    [SerializeField] private bool randomizePhase = true;
    
    [Header("Self Destruct Settings")]
    [Tooltip("Is this pickup able to destroy itself.")]
    [SerializeField] private bool enableSelfDestruct = true;
    [Tooltip("Time for the effect or object to destroy itself.")]
    [SerializeField] float destroyDelay = 1.5f; 

    private Vector3 baseWorldPos;
    private float phaseOffset;

    /// <summary>
    /// Snaps the pickup to the center of its grid cell.
    /// </summary>
    private void Start()
    {
        var grid = TilemapGridManager.Instance;
        if (grid == null)
        {
            Debug.LogError("[PowerupPickup] TilemapGridManager.Instance not found.");
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

    /// <summary>
    /// Activates the powerup when the player enters the trigger.
    /// </summary>
    /// <param name="other">Collider that entered the trigger.</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        bool activated = GameManager.Instance.TryActivatePowerup(type, duration);

        if (activated)
            Destroy(gameObject);
    }
}

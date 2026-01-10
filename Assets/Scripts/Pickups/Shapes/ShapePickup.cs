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
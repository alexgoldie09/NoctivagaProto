using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Obstacle that toggles target cells between floor and void when interacted with.
/// </summary>
public class LeverObstacle : ObstacleBase
{
    [Header("Target Markers (recommended)")]
    [Tooltip("Place empty transforms snapped to grid cells. These will be converted to tilemap cells automatically.")]
    [SerializeField] private List<Transform> targetMarkers = new();

    [Header("Debug / Visuals")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawLabels = true;

    [Header("Enemy Void Elimination")]
    [Tooltip("If true, enemies standing on tiles that become Void will be eliminated.")]
    [SerializeField] private bool eliminateEnemiesOnVoidedTiles = true;

    // Runtime cached cells (derived from markers)
    private readonly List<Vector3Int> targetCells = new();

    /// <summary>
    /// Rebuilds cached target cells when inspector values change.
    /// </summary>
    private void OnValidate()
    {
        RebuildTargetCellsFromMarkers();
    }

    /// <summary>
    /// Initializes cached target cells at runtime.
    /// </summary>
    private void Awake()
    {
        RebuildTargetCellsFromMarkers();
    }

    /// <summary>
    /// Converts marker transforms into unique grid cell positions.
    /// </summary>
    private void RebuildTargetCellsFromMarkers()
    {
        targetCells.Clear();

        var grid = TilemapGridManager.Instance;
        // In edit mode, Instance might not exist; try to find one in the scene.
        if (grid == null) grid = FindFirstObjectByType<TilemapGridManager>();

        if (grid == null)
            return;

        foreach (var t in targetMarkers)
        {
            if (t == null) continue;
            var cell = grid.WorldToCell(t.position);
            if (!targetCells.Contains(cell))
                targetCells.Add(cell);
        }
    }

    /// <summary>
    /// Toggles target tiles, triggers player fall reset, and optionally eliminates enemies on new void tiles.
    /// </summary>
    public override void Interact()
    {
        var grid = TilemapGridManager.Instance;
        if (grid == null)
        {
            Debug.LogWarning("[LeverObstacle] No TilemapGridManager.Instance found.");
            return;
        }

        // Track which cells become void this interaction (event-driven enemy elimination)
        var voidedSet = new HashSet<Vector3Int>();

        foreach (var cell in targetCells)
        {
            var before = grid.GetTileKind(cell);

            grid.ToggleFloorVoidAt(cell);

            var after = grid.GetTileKind(cell);
            if (before != after && after == TileKind.Void)
                voidedSet.Add(cell);
        }

        // If player is now on Void, trigger the fall reset you already built
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            var playerCell = player.CellPosition;
            if (grid.GetTileKind(playerCell) == TileKind.Void)
            {
                Vector3 fallStartWorld = grid.CellToWorldCenter(playerCell);
                player.StartVoidFallReset(grid.GetStartCell(), fallStartWorld);
            }
        }

        // Eliminate enemies that were standing on tiles that just became Void
        if (eliminateEnemiesOnVoidedTiles && voidedSet.Count > 0)
        {
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null) continue;

                if (voidedSet.Contains(e.CellPosition))
                {
                    Vector3 fallStartWorld = grid.CellToWorldCenter(e.CellPosition);
                    e.KillByVoidFall(fallStartWorld);
                }
            }
        }
    }

    /// <summary>
    /// Levers remain blocking for player movement.
    /// </summary>
    public override bool BlocksMovement() => true;

    /// <summary>
    /// Levers remain blocking for shape placement.
    /// </summary>
    public override bool BlocksShapePlacement() => true;

#if UNITY_EDITOR
    /// <summary>
    /// Draws gizmos for target cells and optional labels in the editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        var grid = TilemapGridManager.Instance;
        if (grid == null) grid = FindFirstObjectByType<TilemapGridManager>();
        if (grid == null) return;

        // Always rebuild in editor so gizmos match moved markers
        RebuildTargetCellsFromMarkers();

        Gizmos.matrix = Matrix4x4.identity;

        foreach (var cell in targetCells)
        {
            Vector3 center = grid.CellToWorldCenter(cell);
            Vector3 size = Vector3.one * 0.9f;

            Gizmos.DrawWireCube(center, size);

            if (drawLabels)
            {
                Handles.Label(center + Vector3.up * 0.3f, $"{cell.x},{cell.y}");
            }
        }
    }
#endif
}

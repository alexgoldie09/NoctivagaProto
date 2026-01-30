using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Obstacle that swaps a set of target cells between their initial ("OFF") ground tiles
/// and a configured ("ON") tile when interacted with.
///
/// OFF tiles are captured from the ground tilemap at runtime (game start), so the lever
/// restores exactly what was painted on the map.
/// </summary>
public class LeverObstacle : ObstacleBase
{
    [Header("Target Markers (recommended)")]
    [Tooltip("Place empty transforms snapped to grid cells. These will be converted to tilemap cells automatically.")]
    [SerializeField] private List<Transform> targetMarkers = new();

    [Header("Lever Visuals")]
    [SerializeField] private Sprite leverOffSprite;
    [SerializeField] private Sprite leverOnSprite;

    [Header("Tile Swap")]
    [Tooltip("Tile to apply when the lever is ON. When OFF, each target cell is restored to the ground tile it had at game start.")]
    [SerializeField] private GameTile onTile;

    [Header("Debug / Visuals")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawLabels = true;

    private bool isOn = false;

    // Runtime cached cells (derived from markers)
    private readonly List<Vector3Int> targetCells = new();

    // Runtime cached "off" tiles per target cell (captured from the ground tilemap at game start).
    private readonly Dictionary<Vector3Int, GameTile> offTilesByCell = new();
    private bool hasCachedOffTiles = false;

    /// <summary>
    /// Rebuilds cached target cells when inspector values change.
    /// </summary>
    private void OnValidate()
    {
        RebuildTargetCellsFromMarkers();
    }

    /// <summary>
    /// Initializes cached target cells and captures OFF tiles at runtime.
    /// </summary>
    private void Awake()
    {
        RebuildTargetCellsFromMarkers();
        CacheOffTilesFromGrid();

        sr = GetComponent<SpriteRenderer>();
        ApplyLeverVisual();
    }

    /// <summary>
    /// Converts marker transforms into unique grid cell positions.
    /// </summary>
    private void RebuildTargetCellsFromMarkers()
    {
        targetCells.Clear();

        // In edit mode, Instance might not exist; try to find one in the scene.
        if (grid == null) 
            grid = FindFirstObjectByType<TilemapGridManager>();

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
    /// Caches the "off" (initial) ground tiles for each target cell at runtime.
    /// This allows the lever to restore the exact tiles that were painted on the map when the game started.
    /// </summary>
    private void CacheOffTilesFromGrid()
    {
        offTilesByCell.Clear();
        hasCachedOffTiles = false;
        
        if (grid == null)
            return;

        foreach (var cell in targetCells)
        {
            var ground = grid.GetGroundGameTile(cell);
            if (ground == null)
                continue;

            // Never allow the Start tile to be changed.
            if (ground.kind == TileKind.Start)
                continue;

            offTilesByCell[cell] = ground;
        }

        hasCachedOffTiles = true;
    }

    /// <summary>
    /// Toggles target tiles, triggers player enter effects, and optionally eliminates enemies
    /// on tiles that became hazardous as a result of the interaction.
    /// </summary>
    public override void Interact()
    {
        if (!hasCachedOffTiles)
            CacheOffTilesFromGrid();

        // Toggle
        bool nextIsOn = !isOn;

        // If turning ON, we must have an ON tile.
        if (nextIsOn && onTile == null)
        { 
            return;
        }

        isOn = nextIsOn;

        // Visual update
        ApplyLeverVisual();
        
        // Track which cells actually changed this interaction
        var changedCells = new HashSet<Vector3Int>();

        foreach (var cell in targetCells)
        {
            if (!offTilesByCell.TryGetValue(cell, out var offTile) || offTile == null)
                continue;

            var desiredTile = isOn ? onTile : offTile;

            if (grid.TrySetGroundTile(cell, desiredTile))
                changedCells.Add(cell);
        }

        // Player: only apply if player is on a changed cell (optional, but nice)
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && changedCells.Contains(player.CellPosition))
        {
            Vector3 fallStartWorld = grid.CellToWorldCenter(player.CellPosition);
            grid.HandleEnteredCell(player.CellPosition, player, fallStartWorld);
        }

        // Enemies: apply effects if enemy is on a changed cell
        if (changedCells.Count > 0)
        {
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null) continue;
                
                if (changedCells.Contains(e.CellPosition))
                    grid.HandleEnteredCellEnemy(e.CellPosition, e);
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

    /// <summary>
    /// Updates the lever sprite based on its state.
    /// </summary>
    private void ApplyLeverVisual()
    {
        if (sr == null) return;

        sr.sprite = isOn ? leverOnSprite : leverOffSprite;

        if (sr.sprite == null)
            Debug.LogWarning($"[LeverObstacle] Missing lever sprite (isOn={isOn}) on {name}", this);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws gizmos for target cells and optional labels in the editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        grid = TilemapGridManager.Instance;
        if (grid == null)
            grid = FindFirstObjectByType<TilemapGridManager>();

        if (grid == null)
            return;

        // Always rebuild in editor so gizmos match moved markers
        RebuildTargetCellsFromMarkers();

        Gizmos.matrix = Matrix4x4.identity;

        foreach (var cell in targetCells)
        {
            Vector3 center = grid.CellToWorldCenter(cell);
            Vector3 size = Vector3.one * 0.9f;

            Gizmos.DrawWireCube(center, size);

            if (drawLabels)
                Handles.Label(center + Vector3.up * 0.3f, $"{cell.x},{cell.y}");
        }
    }
#endif
}

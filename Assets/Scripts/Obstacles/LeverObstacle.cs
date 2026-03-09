using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Obstacle that can either swap target ground tiles or toggle target objects
/// active/inactive when interacted with.
///
/// For tile swap mode, OFF tiles are captured from the ground tilemap at runtime (game start),
/// so the lever restores exactly what was painted on the map.
/// </summary>
public class LeverObstacle : ObstacleBase
{
    [System.Serializable]
    private class LeverTarget
    {
        public Transform marker;
        [Tooltip("Optional per-marker ON tile. If empty, the default ON tile is used.")]
        public GameTile onTileOverride;
    }
    
    private enum LeverType
    {
        TileSwap,
        ObjectToggle
    }
    
    [Header("Lever Settings")]
    [Tooltip("TileSwap swaps ground tiles. ObjectToggle toggles target objects active/inactive.")]
    [SerializeField] private LeverType leverType = LeverType.TileSwap;
    
    [Header("Target Markers (recommended)")]
    [Tooltip("Place marker transforms snapped to grid cells. Each marker can optionally define its own ON tile.")]
    [SerializeField] private List<LeverTarget> targetMarkers = new();

    [Header("Lever Visuals")]
    [SerializeField] private Sprite leverOffSprite;
    [SerializeField] private Sprite leverOnSprite;

    [Header("Tile Swap")]
    [Tooltip("Tile to apply when the lever is ON. When OFF, each target cell is restored to the ground tile it had at game start.")]
    [SerializeField] private GameTile onTile;
    
    [Header("Object Toggle")]
    [Tooltip("Objects toggled on each interaction when Lever Type is ObjectToggle.")]
    [SerializeField] private List<GameObject> toggleTargets = new();
    
    [Header("Info Lighting")]
    [SerializeField] private Light2D infoLight;
    [SerializeField] private Color offLightColor = Color.red;
    [SerializeField] private Color onLightColor = Color.green;

    [Header("Debug / Visuals")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawLabels = true;

    private bool isOn = false;

    // Runtime cached cells (derived from markers)
    private readonly List<Vector3Int> targetCells = new();
    
    // Runtime cached ON tiles per target cell (from marker override or fallback default).
    private readonly Dictionary<Vector3Int, GameTile> onTilesByCell = new();

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
        
        if (infoLight != null)
        {
            infoLight.color = isOn ? onLightColor : offLightColor;
        }
    }

    /// <summary>
    /// Converts marker transforms into unique grid cell positions.
    /// </summary>
    private void RebuildTargetCellsFromMarkers()
    {
        targetCells.Clear();
        onTilesByCell.Clear();

        // In edit mode, Instance might not exist; try to find one in the scene.
        if (grid == null) 
            grid = FindFirstObjectByType<TilemapGridManager>();

        if (grid == null)
            return;

        foreach (var t in targetMarkers)
        {
            if (t == null || t.marker == null) continue;

            var cell = grid.WorldToCell(t.marker.position);
            if (!targetCells.Contains(cell))
                targetCells.Add(cell);
            
            if(!onTilesByCell.ContainsKey(cell))
                onTilesByCell[cell] = t.onTileOverride != null ? t.onTileOverride : onTile;
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
        base.Interact();
        
        isOn = !isOn;

        // Visual update
        ApplyLeverVisual();
        
        if (infoLight != null)
        {
            infoLight.color = isOn ? onLightColor : offLightColor;
        }

        // If turning ON, we must have an ON tile.
        if (leverType == LeverType.ObjectToggle)
        {
            ToggleTargetObjects();
            return;
        }

        ToggleTargetTiles();
    }
    
    /// <summary>
    /// Applies tile swap behavior for all configured target cells.
    /// </summary>
    private void ToggleTargetTiles()
    {
        if (!hasCachedOffTiles)
            CacheOffTilesFromGrid();
        
        // Track which cells actually changed this interaction
        var changedCells = new HashSet<Vector3Int>();

        foreach (var cell in targetCells)
        {
            if (!offTilesByCell.TryGetValue(cell, out var offTile) || offTile == null)
                continue;

            if (!onTilesByCell.TryGetValue(cell, out var onTileForCell))
                onTileForCell = onTile;

            // If turning ON and this cell has no ON tile configured, skip it.
            if (isOn && onTileForCell == null)
                continue;

            var desiredTile = isOn ? onTileForCell : offTile;

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
    /// Toggles configured target objects active/inactive each interaction.
    /// </summary>
    private void ToggleTargetObjects()
    {
        foreach (var target in toggleTargets)
        {
            if (target == null)
                continue;

            target.SetActive(!target.activeSelf);
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

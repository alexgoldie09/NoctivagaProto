using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.Tilemaps;

/// <summary>
/// Custom brush used to inspect tile assets and display custom GameTile metadata
/// when picking tiles from the Tile Palette or Tilemap.
/// </summary>
[CustomGridBrush(false, true, false, "GameTile Info Brush")]
public class GameTileInfoBrush : GridBrush
{
    [SerializeField] private TileBase _lastPickedTile;
    [SerializeField] private Vector3Int _lastPickedCell;
    [SerializeField] private string _lastPickedAssetPath;

    /// <summary>
    /// The most recently picked tile asset.
    /// </summary>
    public TileBase LastPickedTile => _lastPickedTile;

    /// <summary>
    /// The most recently picked grid cell position.
    /// </summary>
    public Vector3Int LastPickedCell => _lastPickedCell;

    /// <summary>
    /// Asset path of the last picked tile (if it is an asset-backed tile).
    /// </summary>
    public string LastPickedAssetPath => _lastPickedAssetPath;

    /// <summary>
    /// Called when using the Pick tool in the Tile Palette / GridBrush workflow.
    /// Captures the tile and its source asset path for inspection.
    /// </summary>
    public override void Pick(GridLayout gridLayout, GameObject brushTarget, BoundsInt position, Vector3Int pivot)
    {
        base.Pick(gridLayout, brushTarget, position, pivot);

        if (brushTarget == null)
        {
            _lastPickedTile = null;
            _lastPickedAssetPath = string.Empty;
            return;
        }

        var tilemap = brushTarget.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            _lastPickedTile = null;
            _lastPickedAssetPath = string.Empty;
            return;
        }

        // Use the minimum cell of the selection as the representative picked tile.
        _lastPickedCell = position.min;
        _lastPickedTile = tilemap.GetTile(_lastPickedCell);

        _lastPickedAssetPath = _lastPickedTile != null
            ? AssetDatabase.GetAssetPath(_lastPickedTile)
            : string.Empty;

        // Log quick debug info in Console too (optional, but handy)
        if (_lastPickedTile != null)
        {
            Debug.Log($"[GameTileInfoBrush] Picked tile: {_lastPickedTile.name} at {_lastPickedCell} (Path: {_lastPickedAssetPath})");
        }
        else
        {
            Debug.Log($"[GameTileInfoBrush] No tile found at {_lastPickedCell}");
        }
    }

    /// <summary>
    /// Optional: also capture info when painting, so you can inspect what you're placing.
    /// </summary>
    public override void Paint(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
    {
        base.Paint(gridLayout, brushTarget, position);

        // If the brush currently holds tiles, inspect the center tile of the brush
        if (cells != null && cells.Length > 0)
        {
            var tile = cells[0].tile;
            if (tile != null)
            {
                _lastPickedTile = tile;
                _lastPickedCell = position;
                _lastPickedAssetPath = AssetDatabase.GetAssetPath(tile);
            }
        }
    }
}
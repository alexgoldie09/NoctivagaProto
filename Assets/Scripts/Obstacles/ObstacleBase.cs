using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Base class for all tile-based obstacles (mirror, lever, etc).
/// Obstacles sit on top of a GridTile and block or interact with gameplay.
/// </summary>
public abstract class ObstacleBase : MonoBehaviour
{
    /// <summary>
    /// Save position and register the obstacle prefab on generation.
    /// </summary>
    protected virtual void OnEnable()
    {
        var grid = TilemapGridManager.Instance;
        if (grid == null)
        {
            Debug.LogWarning($"[ObstacleBase] No TilemapGridManager.Instance when enabling {name}");
            return;
        }

        Vector3Int cell = grid.WorldToCell(transform.position);
        grid.RegisterObstacle(this, cell);

        // Debug.Log($"[ObstacleBase] Registered {name} at cell {cell} (world {transform.position})");
    }

    /// <summary>
    /// Remove position and unregister the obstacle prefab on destruction.
    /// </summary>
    protected virtual void OnDisable()
    {
        var grid = TilemapGridManager.Instance;
        
        if (grid == null) 
            return;

        grid.UnregisterObstacle(this);
    }

    /// <summary>
    /// Whether this obstacle blocks player movement.
    /// Override for custom logic like rotating mirrors.
    /// </summary>
    public virtual bool BlocksMovement()
    {
        return true;
    }

    /// <summary>
    /// Whether this obstacle blocks shape placement.
    /// </summary>
    public virtual bool BlocksShapePlacement()
    {
        return true;
    }

    /// <summary>
    /// Optional: Called when the obstacle is interacted with (e.g. rotate or trigger).
    /// </summary>
    public virtual void Interact()
    {
        // Default: do nothing. Mirrors or levers can override.
    }
    
}
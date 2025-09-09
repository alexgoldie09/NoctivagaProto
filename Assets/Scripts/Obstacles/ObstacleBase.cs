using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Base class for all tile-based obstacles (mirror, lever, etc).
/// Obstacles sit on top of a GridTile and block or interact with gameplay.
/// </summary>
public abstract class ObstacleBase : MonoBehaviour
{
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

    /// <summary>
    /// Export metadata for saving into MapData (override per obstacle).
    /// </summary>
    public virtual Dictionary<string, string> GetMetadata()
    {
        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Import metadata when loading from MapData (override per obstacle).
    /// </summary>
    public virtual void SetMetadata(Dictionary<string, string> data)
    {
        // Override in child class to restore state
    }
}
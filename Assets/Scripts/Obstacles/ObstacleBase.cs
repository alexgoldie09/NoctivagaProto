using UnityEngine;

/// <summary>
/// Base class for all tile-based obstacles (mirror, wheel, etc).
/// Obstacles sit on top of a GridTile and block or interact with gameplay.
/// </summary>
public abstract class ObstacleBase : MonoBehaviour
{
    [Tooltip("Optional: Grid tile this obstacle is attached to.")]
    public GridTile hostTile;

    /// <summary>
    /// Called once after obstacle is placed or initialized.
    /// Useful for registration, linking to tile, etc.
    /// </summary>
    public virtual void Initialize(GridTile tile)
    {
        hostTile = tile;
        tile.obstacle = this;
    }

    /// <summary>
    /// Whether this obstacle blocks player movement.
    /// Override for custom logic like rotating mirrors.
    /// </summary>
    public virtual bool BlocksMovement()
    {
        return true; // Default blocks unless overridden
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
        // Default: do nothing. Mirrors or wheels can override.
    }
}
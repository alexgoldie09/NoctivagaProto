using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Base class for all tile-based obstacles (mirror, lever, etc).
/// Obstacles sit on top of a GridTile and block or interact with gameplay.
/// </summary>
public abstract class ObstacleBase : MonoBehaviour
{
    [Header("Interact Prompt")]
    [SerializeField, TextArea] private string interactPrompt = "Press Interact";
    [SerializeField] private WorldPromptBubble promptBubble;
    [SerializeField] private bool allowPrompt = true;
    
    protected TilemapGridManager grid;
    protected SpriteRenderer sr;
    
    /// <summary>
    /// Save position and register the obstacle prefab on generation.
    /// </summary>
    protected virtual void OnEnable()
    {
        grid = TilemapGridManager.Instance;
        if (grid == null)
        {
            Debug.LogWarning($"[ObstacleBase] No TilemapGridManager.Instance when enabling {name}");
            return;
        }

        Vector3Int cell = grid.WorldToCell(transform.position);
        grid.RegisterObstacle(cell, this);
        
        ResolvePlayerIfOnObstacle(grid);

        // Debug.Log($"[ObstacleBase] Registered {name} at cell {cell} (world {transform.position})");
    }

    /// <summary>
    /// Remove position and unregister the obstacle prefab on destruction.
    /// </summary>
    protected virtual void OnDisable()
    {
        grid = TilemapGridManager.Instance;
        
        if (grid == null) 
            return;

        grid.UnregisterObstacle(this);
    }

    /// <summary>
    /// Whether this obstacle blocks player movement.
    /// Override for custom logic like rotating mirrors.
    /// </summary>
    public virtual bool BlocksMovement() => true;

    /// <summary>
    /// Whether this obstacle blocks shape placement.
    /// </summary>
    public virtual bool BlocksShapePlacement() => true;
    
    private void ResolvePlayerIfOnObstacle(TilemapGridManager g)
    {
        if (g == null)
            return;

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null)
            return;

        if (!g.TryGetObstacle(player.CellPosition, out var obs))
            return;

        g.RelocatePlayerFromBlockedCell(player);
    }
    
    public bool CanInteract(PlayerController player) => true;
    public string GetInteractPrompt(PlayerController player) => interactPrompt;

    public WorldPromptBubble PromptBubble => promptBubble;

    public virtual void ShowPrompt(PlayerController player)
    {
        if (promptBubble != null && allowPrompt)
            promptBubble.Show(GetInteractPrompt(player));
    }

    public virtual void HidePrompt()
    {
        if (promptBubble != null)
            promptBubble.Hide();
    }

    /// <summary>
    /// Optional: Called when the obstacle is interacted with (e.g. rotate or trigger).
    /// </summary>
    public virtual void Interact()
    {
        // Default: do nothing. Mirrors or levers can override.
    }
    
}
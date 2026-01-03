using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A static enemy that performs a swipe attack every few beats.
/// The swipe covers a 2x2 quad of tiles in a chosen cardinal direction.
/// Only valid walkable tiles are considered for swipe effects and contact.
/// </summary>
public class EnemyStaticSwiping : EnemyStatic
{
    public enum AttackDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    [Header("Swipe Settings")]
    [SerializeField] private GameObject swipeVFXPrefab;

    [Tooltip("Offset from this enemy where the swipe quad starts (1 = one tile away).")]
    [SerializeField] private int attackRange = 1;

    [Tooltip("Direction the enemy swipes.")]
    [SerializeField] private AttackDirection attackDirection = AttackDirection.Up;

    [Header("Telegraph")]
    [SerializeField] private Color telegraphColor = new (1f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private float telegraphDuration = 0.30f;

    private List<Vector3Int> pendingAttackCells = new();

    /// <summary>
    /// Alternates between telegraphing and executing the swipe on active beats.
    /// </summary>
    protected override void OnBeatAction()
    {
        base.OnBeatAction();

        if (grid == null) return;

        if (pendingAttackCells.Count > 0)
        {
            // Execute the real swipe
            ExecuteSwipe(pendingAttackCells);
            pendingAttackCells.Clear();
        }
        else
        {
            // Telegraph this beat
            Vector3Int dir = GetDirectionVector(attackDirection);
            pendingAttackCells = GetSwipeCells(dir);

            // In-game telegraph using TM_Preview (per-owner safe)
            grid.FlashPreviewCellsForOwner(GetInstanceID(), pendingAttackCells, telegraphColor, telegraphDuration);
        }
    }

    /// <summary>
    /// Spawns swipe effects and applies player contact if they are inside the swipe cells.
    /// </summary>
    /// <param name="cells">Cells affected by the swipe.</param>
    private void ExecuteSwipe(List<Vector3Int> cells)
    {
        // Spawn swipe VFX at cell centers
        if (swipeVFXPrefab != null && grid != null)
        {
            // Cinemachine Impulse shake
            if (damageShakeForce > 0f && allowDamageShake)
                CameraShake.Instance?.Shake(damageShakeForce);
            
            foreach (var c in cells)
            {
                Vector3 worldPos = grid.CellToWorldCenter(c);
                Instantiate(swipeVFXPrefab, worldPos, Quaternion.identity);
            }
        }

        // Player contact check by cell
        if (player != null && cells.Contains(player.CellPosition))
            OnPlayerContact();
    }
    
    /// <summary>
    /// Returns valid cell positions for the swipe quad in the given direction.
    /// Filters out cells that are not enterable (out of bounds, walls, beam-blocked, etc).
    /// </summary>
    /// <param name="dir">Cardinal direction to build the swipe quad.</param>
    /// <returns>Filtered list of swipe target cells.</returns>
    private List<Vector3Int> GetSwipeCells(Vector3Int dir)
    {
        var cells = new List<Vector3Int>();

        Vector3Int baseCell = cellPos + dir * attackRange;
        var candidates = new List<Vector3Int>();

        if (dir == Vector3Int.up || dir == Vector3Int.down)
        {
            candidates.Add(baseCell);
            candidates.Add(baseCell + Vector3Int.right);
            candidates.Add(baseCell + Vector3Int.left);
            candidates.Add(baseCell + dir);
        }
        else
        {
            candidates.Add(baseCell);
            candidates.Add(baseCell + Vector3Int.up);
            candidates.Add(baseCell + Vector3Int.down);
            candidates.Add(baseCell + dir);
        }

        foreach (var c in candidates)
        {
            if (grid.IsInBounds(c) && grid.CanEnemyEnterCell(c))
                cells.Add(c);
        }

        return cells;
    }

    /// <summary>
    /// Converts an attack direction enum into a grid direction vector.
    /// </summary>
    /// <param name="dir">Attack direction enum.</param>
    /// <returns>Cardinal grid direction vector.</returns>
    private static Vector3Int GetDirectionVector(AttackDirection dir)
    {
        return dir switch
        {
            AttackDirection.Up => Vector3Int.up,
            AttackDirection.Down => Vector3Int.down,
            AttackDirection.Left => Vector3Int.left,
            AttackDirection.Right => Vector3Int.right,
            _ => Vector3Int.up
        };
    }

#if UNITY_EDITOR
    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawLabels = true;
    [SerializeField] private Color gizmoColor = new (1f, 0.3f, 0.3f, 0.7f);
    
    /// <summary>
    /// Draws editor gizmos showing the swipe quad area for this enemy.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        var g = TilemapGridManager.Instance;
        if (g == null) g = FindFirstObjectByType<TilemapGridManager>();
        if (g == null) return;

        Vector3Int enemyCell = Application.isPlaying ? cellPos : g.WorldToCell(transform.position);

        Vector3Int dir = GetDirectionVector(attackDirection);
        Vector3Int baseCell = enemyCell + dir * attackRange;

        // Build the same quad (without walkable filtering for editor visibility)
        var candidates = new List<Vector3Int>();
        if (dir == Vector3Int.up || dir == Vector3Int.down)
        {
            candidates.Add(baseCell);
            candidates.Add(baseCell + Vector3Int.right);
            candidates.Add(baseCell + Vector3Int.left);
            candidates.Add(baseCell + dir);
        }
        else
        {
            candidates.Add(baseCell);
            candidates.Add(baseCell + Vector3Int.up);
            candidates.Add(baseCell + Vector3Int.down);
            candidates.Add(baseCell + dir);
        }

        Gizmos.color = gizmoColor;

        foreach (var c in candidates)
        {
            Vector3 center = g.CellToWorldCenter(c);
            Gizmos.DrawWireCube(center, Vector3.one * 0.9f);

#if UNITY_EDITOR
            if (drawLabels)
                UnityEditor.Handles.Label(center + Vector3.up * 0.3f, $"{c.x},{c.y}");
#endif
        }
    }
#endif
}

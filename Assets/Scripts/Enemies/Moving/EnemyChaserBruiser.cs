using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A Chaser variant that periodically punishes nearby tiles.
/// - Telegraphs on (punishCycleBeats - 1), then slams all 8 surrounding tiles on punishCycleBeats.
/// - Chases like a normal EnemyChaser on other beats.
/// Uses TM_Preview telegraph via TilemapGridManager.
/// </summary>
public class EnemyChaserBruiser : EnemyChaser
{
    [Header("Bruiser Settings")]
    [Tooltip("Prefab for slam VFX (spawns on each attacked tile).")]
    [SerializeField] private GameObject slamVFXPrefab;

    [Tooltip("Cycle length in beats for the punish attack (telegraph then slam).")]
    [SerializeField] private int punishCycleBeats = 4;

    [Header("Telegraph")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private float telegraphDuration = 0.30f;

    private List<Vector3Int> pendingPunishCells = new();
    private int phaseCounter = 0;

    protected override void OnBeatAction()
    {
        if (punishCycleBeats < 2) punishCycleBeats = 2;
        phaseCounter++;

        // Slam beat
        if (phaseCounter == punishCycleBeats)
        {
            if (pendingPunishCells.Count > 0)
            {
                ExecutePunish(pendingPunishCells);
                pendingPunishCells.Clear();
            }

            phaseCounter = 0;
            return;
        }

        // Telegraph beat
        if (phaseCounter == punishCycleBeats - 1)
        {
            TelegraphPunish();
            return;
        }

        // Otherwise: chase like normal
        base.OnBeatAction();
    }

    private void TelegraphPunish()
    {
        if (grid == null) return;

        pendingPunishCells = GetSurroundingCellsFiltered();

        // In-game telegraph (TM_Preview)
        grid.FlashPreviewCellsForOwner(GetInstanceID(), pendingPunishCells, telegraphColor, telegraphDuration);

        if (animator != null)
            animator.SetTrigger("OnBeat");
    }

    private void ExecutePunish(List<Vector3Int> cells)
    {
        if (grid == null) 
            return;
        
        // Cinemachine Impulse shake
        if (damageShakeForce > 0f && allowDamageShake)
            CameraShake.Instance?.Shake(damageShakeForce);

        foreach (var c in cells)
        {
            if (slamVFXPrefab != null)
            {
                Vector3 worldPos = grid.CellToWorldCenter(c);
                Instantiate(slamVFXPrefab, worldPos, Quaternion.identity);
            }

            if (player != null && player.CellPosition == c)
                OnPlayerContact();
        }

        if (animator != null)
            animator.SetTrigger("OnBeat");
    }

    private List<Vector3Int> GetSurroundingCellsFiltered()
    {
        var cells = new List<Vector3Int>();

        Vector3Int[] offsets =
        {
            Vector3Int.up, Vector3Int.down,
            Vector3Int.left, Vector3Int.right,
            new (1, 1, 0), new (-1, 1, 0),
            new (1,-1, 0), new (-1,-1, 0)
        };

        foreach (var off in offsets)
        {
            Vector3Int pos = cellPos + off;

            // Match original intent: only punish tiles that are "valid walkable" tiles
            if (grid.IsInBounds(pos) && grid.CanEnemyEnterCell(pos))
                cells.Add(pos);
        }

        return cells;
    }

#if UNITY_EDITOR
    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawLabels = true;
    [SerializeField] private Color gizmoColor = new(1f, 0.3f, 0.3f, 0.7f);

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        var g = TilemapGridManager.Instance;
        if (g == null) g = FindFirstObjectByType<TilemapGridManager>();
        if (g == null) return;

        Vector3Int eCell = Application.isPlaying ? cellPos : g.WorldToCell(transform.position);

        Vector3Int[] offsets =
        {
            Vector3Int.up, Vector3Int.down,
            Vector3Int.left, Vector3Int.right,
            new (1, 1, 0), new (-1, 1, 0),
            new (1,-1, 0), new (-1,-1, 0)
        };

        Gizmos.color = gizmoColor;

        foreach (var off in offsets)
        {
            Vector3Int c = eCell + off;
            Vector3 center = g.CellToWorldCenter(c);
            Gizmos.DrawWireCube(center, Vector3.one * 0.9f);

            if (drawLabels)
            {
                UnityEditor.Handles.Label(center + Vector3.up * 0.3f, $"{c.x},{c.y}");
            }
        }
    }
#endif
}

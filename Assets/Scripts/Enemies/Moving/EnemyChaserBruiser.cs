using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A Chaser variant that periodically punishes nearby tiles.
/// - Telegraphs on (beatsPerAction - 1), then slams all 8 surrounding tiles on beatsPerAction.
/// - Optionally chases like a normal EnemyChaser between punish cycles.
/// </summary>
public class EnemyChaserBruiser : EnemyChaser
{
    [Header("Bruiser Settings")]
    [Tooltip("Prefab for slam VFX (spawns on each attacked tile).")]
    [SerializeField] private GameObject slamVFXPrefab;

    [Tooltip("Alpha-flash telegraph color for warning tiles.")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0f, 0f, 0.5f);

    private List<Vector2Int> pendingPunishTiles = new List<Vector2Int>();
    private int phaseCounter = 0;

    protected override void OnBeatAction()
    {
        phaseCounter++;

        // ─── Slam phase ────────────────────────────────────────────────
        if (phaseCounter == beatsPerAction)
        {
            if (pendingPunishTiles.Count > 0)
            {
                ExecutePunish(pendingPunishTiles);
                pendingPunishTiles.Clear();
            }

            phaseCounter = 0; // reset cycle
            return;
        }

        // ─── Telegraph phase ──────────────────────────────────────────
        if (phaseCounter == beatsPerAction - 1)
        {
            TelegraphPunish();
            return;
        }

        // ─── Default chase on other beats ─────────────────────────────
        base.OnBeatAction();
    }

    private void TelegraphPunish()
    {
        pendingPunishTiles = GetSurroundingTiles();

        foreach (var pos in pendingPunishTiles)
        {
            GridTile tile = grid.GetTileAt(pos.x, pos.y);
            if (tile != null)
            {
                tile.FlashWarning(telegraphColor, 0.3f);
            }
        }

        if (animator != null)
            animator.SetTrigger("OnBeat");
    }

    private void ExecutePunish(List<Vector2Int> tiles)
    {
        foreach (var tile in tiles)
        {
            if (slamVFXPrefab != null)
            {
                Vector3 worldPos = GridToWorld(tile);
                Instantiate(slamVFXPrefab, worldPos, Quaternion.identity);
            }

            if (player != null && player.GridPosition == tile)
            {
                OnPlayerContact();
            }
        }

        if (animator != null)
            animator.SetTrigger("OnBeat");
    }

    private List<Vector2Int> GetSurroundingTiles()
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        Vector2Int[] offsets = {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right,
            new Vector2Int(1,1), new Vector2Int(-1,1),
            new Vector2Int(1,-1), new Vector2Int(-1,-1)
        };

        foreach (var off in offsets)
        {
            Vector2Int pos = gridPos + off;
            if (grid != null && grid.IsInBounds(pos.x, pos.y) && grid.IsWalkable(pos.x, pos.y))
            {
                tiles.Add(pos);
            }
        }

        return tiles;
    }
}

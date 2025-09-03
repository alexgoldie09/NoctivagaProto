using System.Collections;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class MirrorObstacle : ObstacleBase
{
    public enum MirrorDirection
    {
        UpRight,  // ↗
        UpLeft,   // ↖
        DownRight,// ↘
        DownLeft  // ↙
    }

    [Header("Mirror Settings")]
    public MirrorDirection direction = MirrorDirection.UpRight;
    public bool beamActive = true;
    public bool blocksWhileActive = true;
    public float beamLength = 10f;
    public int maxReflections = 3;
    
    private LineRenderer lineRenderer;
    private List<GridTile> beamTiles = new List<GridTile>();

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.yellow;
        lineRenderer.endColor = Color.white;

        StartCoroutine(DelayedBeamInit());
    }

    private IEnumerator DelayedBeamInit()
    {
        yield return new WaitForEndOfFrame(); // Allows all mirrors to finish Start()
        UpdateBeam();
    }

    public override void Interact()
    {
        RotateClockwise();
    }

    public void RotateClockwise()
    {
        direction = (MirrorDirection)(((int)direction + 1) % 4);
        UpdateBeam();
    }

    public override bool BlocksMovement()
    {
        return beamActive && blocksWhileActive;
    }

    public override bool BlocksShapePlacement()
    {
        return true;
    }

    private void UpdateBeam()
    {
        ClearBeam();

        if (!beamActive)
        {
            lineRenderer.enabled = false;
            return;
        }

        Vector3 start = transform.position;
        Vector3 dir = GetDirectionVector(direction);
        CastBeamRecursive(start, dir, maxReflections);
    }

    private void CastBeamRecursive(Vector3 origin, Vector3 direction, int reflectionsLeft)
    {
        if (reflectionsLeft <= 0)
            return;

        Vector3 start = origin;
        Vector3 end = origin;

        Vector2Int current = Vector2Int.RoundToInt(origin);
        Vector2Int step = new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));

        for (int i = 0; i < beamLength; i++)
        {
            current += step;
            end = new Vector3(current.x, current.y, 0);

            GridTile tile = GridManager.Instance.GetTileAt(current.x, current.y);
            if (tile == null)
                break;

            // Check for blocking tile
            if (tile.tileType == TileType.Wall || tile.tileType == TileType.Gate)
                break;

            tile.isBeamBlocking = true;
            beamTiles.Add(tile);

            // Check for mirror
            if (tile.HasObstacle(out ObstacleBase obs) && obs is MirrorObstacle mirror && mirror != this)
            {
                Vector2Int incoming = -step;
                mirror.OnBeamHit(new Vector3(current.x, current.y, 0), incoming, reflectionsLeft - 1);
                break;
            }
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    public void OnBeamHit(Vector3 hitPoint, Vector2Int incomingDirection, int reflectionsLeft)
    {
        if (!beamActive || reflectionsLeft <= 0)
            return;

        Vector3 start = hitPoint;
        Vector3 newDir = GetReflectedDirection(incomingDirection);
        CastBeamRecursive(start, newDir, reflectionsLeft);
    }

    private void ClearBeam()
    {
        foreach (var tile in beamTiles)
            tile.isBeamBlocking = false;

        beamTiles.Clear();
    }

    private Vector3 GetDirectionVector(MirrorDirection dir)
    {
        switch (dir)
        {
            case MirrorDirection.UpRight: return new Vector3(1, 1, 0).normalized;
            case MirrorDirection.UpLeft: return new Vector3(-1, 1, 0).normalized;
            case MirrorDirection.DownRight: return new Vector3(1, -1, 0).normalized;
            case MirrorDirection.DownLeft: return new Vector3(-1, -1, 0).normalized;
            default: return Vector3.zero;
        }
    }

    private Vector3 GetReflectedDirection(Vector2Int incoming)
    {
        // Mirror reflection logic (diagonal flip based on mirror orientation)
        switch (direction)
        {
            case MirrorDirection.UpRight:
                if (incoming == new Vector2Int(-1, -1)) return new Vector3(1, 1, 0).normalized; // ↙ ➝ ↗
                break;
            case MirrorDirection.UpLeft:
                if (incoming == new Vector2Int(1, -1)) return new Vector3(-1, 1, 0).normalized; // ↘ ➝ ↖
                break;
            case MirrorDirection.DownRight:
                if (incoming == new Vector2Int(-1, 1)) return new Vector3(1, -1, 0).normalized; // ↖ ➝ ↘
                break;
            case MirrorDirection.DownLeft:
                if (incoming == new Vector2Int(1, 1)) return new Vector3(-1, -1, 0).normalized; // ↗ ➝ ↙
                break;
        }

        return Vector3.zero; // No reflection
    }
}

using System.Collections;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MirrorObstacle reflects laser beams in one of four diagonal directions.
/// Mirrors can rotate, activate/deactivate their beam, and reflect between each other.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MirrorObstacle : ObstacleBase
{
    /// <summary>
    /// The diagonal directions this mirror can face.
    /// </summary>
    public enum MirrorDirection
    {
        UpRight,   // ↗
        UpLeft,    // ↖
        DownRight, // ↘
        DownLeft   // ↙
    }

    [Header("Mirror Settings")]
    public MirrorDirection direction = MirrorDirection.UpRight; // Current facing direction of the mirror
    public bool beamActive = true;                              // Whether the beam is currently active
    public bool blocksWhileActive = true;                       // Whether this mirror blocks movement when beam is active
    public float beamLength = 10f;                              // Max distance the beam travels
    public int maxReflections = 3;                              // Max number of times the beam can reflect

    private LineRenderer lineRenderer;                          // Reference to the line renderer used for beam visuals
    private List<GridTile> beamTiles = new List<GridTile>();    // Tiles currently affected by the beam

    /// <summary>
    /// Initializes line renderer and delays beam setup to allow all mirrors to spawn.
    /// </summary>
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

    /// <summary>
    /// Waits until the end of the frame before initializing the beam.
    /// Ensures all other mirrors are placed in the scene.
    /// </summary>
    private IEnumerator DelayedBeamInit()
    {
        yield return new WaitForEndOfFrame();
        UpdateBeam();
    }

    /// <summary>
    /// Called when the mirror is interacted with. Rotates the mirror.
    /// </summary>
    public override void Interact()
    {
        RotateClockwise();
    }

    /// <summary>
    /// Rotates the mirror clockwise and updates the beam direction.
    /// </summary>
    public void RotateClockwise()
    {
        direction = (MirrorDirection)(((int)direction + 1) % 4);
        UpdateBeam();
    }

    /// <summary>
    /// Determines if the mirror blocks movement.
    /// </summary>
    public override bool BlocksMovement()
    {
        return beamActive && blocksWhileActive;
    }

    /// <summary>
    /// Mirrors always block shape placement.
    /// </summary>
    public override bool BlocksShapePlacement()
    {
        return true;
    }

    /// <summary>
    /// Updates the beam based on current direction, clearing old paths and rendering new ones.
    /// </summary>
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

    /// <summary>
    /// Recursively casts a beam from the origin in the given direction, handling reflections.
    /// </summary>
    private void CastBeamRecursive(Vector3 origin, Vector3 direction, int reflectionsLeft)
    {
        if (reflectionsLeft <= 0) return;

        Vector3 start = origin;
        Vector3 end = origin;

        Vector2Int current = Vector2Int.RoundToInt(origin);
        Vector2Int step = new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));

        for (int i = 0; i < beamLength; i++)
        {
            current += step;
            end = new Vector3(current.x, current.y, 0);

            GridTile tile = GridManager.Instance.GetTileAt(current.x, current.y);
            if (tile == null) break;

            // Stop beam on walls or gates
            if (tile.tileType == TileType.Wall || tile.tileType == TileType.Gate) break;

            tile.isBeamBlocking = true;
            beamTiles.Add(tile);

            // Reflect off other mirrors
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

    /// <summary>
    /// Handles being hit by a beam and reflects it in a new direction.
    /// </summary>
    public void OnBeamHit(Vector3 hitPoint, Vector2Int incomingDirection, int reflectionsLeft)
    {
        if (!beamActive || reflectionsLeft <= 0)
            return;

        Vector3 start = hitPoint;
        Vector3 newDir = GetReflectedDirection(incomingDirection);
        CastBeamRecursive(start, newDir, reflectionsLeft);
    }

    /// <summary>
    /// Clears the beam path from affected tiles and resets visuals.
    /// </summary>
    private void ClearBeam()
    {
        foreach (var tile in beamTiles)
            tile.isBeamBlocking = false;

        beamTiles.Clear();
    }

    /// <summary>
    /// Gets the normalized direction vector for the current mirror direction.
    /// </summary>
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

    /// <summary>
    /// Determines the reflected direction based on incoming beam and mirror orientation.
    /// </summary>
    private Vector3 GetReflectedDirection(Vector2Int incoming)
    {
        switch (direction)
        {
            case MirrorDirection.UpRight:
                if (incoming == new Vector2Int(-1, -1)) return new Vector3(1, 1, 0).normalized;
                break;
            case MirrorDirection.UpLeft:
                if (incoming == new Vector2Int(1, -1)) return new Vector3(-1, 1, 0).normalized;
                break;
            case MirrorDirection.DownRight:
                if (incoming == new Vector2Int(-1, 1)) return new Vector3(1, -1, 0).normalized;
                break;
            case MirrorDirection.DownLeft:
                if (incoming == new Vector2Int(1, 1)) return new Vector3(-1, -1, 0).normalized;
                break;
        }

        return Vector3.zero; // No valid reflection
    }

    /// <summary>
    /// Serializes the mirror's direction for saving into MapData.
    /// </summary>
    public override Dictionary<string, string> GetMetadata()
    {
        return new Dictionary<string, string>
        {
            { "direction", ((int)direction).ToString() }
        };
    }

    /// <summary>
    /// Loads the mirror's direction from saved metadata.
    /// </summary>
    public override void SetMetadata(Dictionary<string, string> data)
    {
        if (data.TryGetValue("direction", out string dirStr) &&
            int.TryParse(dirStr, out int dirInt) &&
            System.Enum.IsDefined(typeof(MirrorDirection), dirInt))
        {
            direction = (MirrorDirection)dirInt;
        }
    }
}

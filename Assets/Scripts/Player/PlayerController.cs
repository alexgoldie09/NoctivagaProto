using UnityEngine;

using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Controls player movement and interaction on a tile-based grid.
/// Delegates all scoring and feedback to ScoreManager.
/// </summary>
public class PlayerController : MonoBehaviour 
{
    [Header("Input (New Input System)")]
    [SerializeField] private InputActionReference moveActionRef;
    [SerializeField] private InputActionReference interactActionRef;

    private InputAction moveAction;
    private InputAction interactAction;

    private Vector3Int cellPos;
    private Vector2Int lastDirection = Vector2Int.right;

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private TilemapGridManager grid;
    
    // Shadow mode state
    public bool IsShadowMode { get; private set; } = false;

    // Facing state
    public bool FacingRight { get; private set; } = true;

    // Void reset VFX fields (keep your existing ones if you already added them)
    [Header("Void Fall Reset")]
    [SerializeField] private float voidFallDuration = 0.35f;
    [SerializeField] private float voidFallDropDistance = 0.25f;
    [SerializeField] private float voidShakeDuration = 0.15f;
    [SerializeField] private float voidShakeMagnitude = 0.08f;

    private bool isResetting;
    private Vector3 initialScale;
    private Coroutine resetRoutine;
    private Coroutine shakeRoutine;
    
    #region Unity Events
    // ─────────────────────────────────────────────
    private void Start() 
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        
        grid = TilemapGridManager.Instance;

        if (grid == null) 
        {
            Debug.LogError("TilemapGridManager instance not found.");
            return;
        }
        
        // Save player scale
        initialScale = transform.localScale;

        // Spawn at Start cell (or bounds center fallback)
        cellPos = grid.GetStartCell();

        // Snap player to center of that cell
        rb.position = grid.CellToWorldCenter(cellPos);
    }

    private void Update()
    {
        // Stop player input if game is frozen
        // Temporary fallback if you haven't wired interact yet
        if (Utilities.IsGameFrozen || isResetting) 
            return;

        // if (interactActionRef == null && Input.GetKeyDown(KeyCode.E))
        //     TryInteract();
    }
    
    private void OnEnable()
    {
        if (moveActionRef != null)
        {
            moveAction = moveActionRef.action;
            moveAction.performed += OnMovePerformed;
            moveAction.Enable();
        }

        if (interactActionRef != null)
        {
            interactAction = interactActionRef.action;
            interactAction.performed += OnInteractPerformed;
            interactAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.Disable();
        }

        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
            interactAction.Disable();
        }
    }
    #endregion

    #region Input

    // ─────────────────────────────────────────────
    // Input Callbacks
    // ─────────────────────────────────────────────
    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting) 
            return;

        Vector2 v = ctx.ReadValue<Vector2>();
        Vector2Int dir = ToCardinal(v);
        if (dir == Vector2Int.zero) return;

        lastDirection = dir;

        // Only update facing on horizontal input
        if (dir.x < 0) 
            Flip(false);
        else if (dir.x > 0) 
            Flip(true);

        TryMove(dir);
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting) 
            return;
        
        TryInteract();
    }

    private static Vector2Int ToCardinal(Vector2 v)
    {
        // With WASD composites, v is usually already cardinal,
        // but this makes it robust (and avoids diagonal if two keys pressed).
        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);

        if (ax < 0.01f && ay < 0.01f) return Vector2Int.zero;

        if (ax >= ay)
            return new Vector2Int(v.x > 0 ? 1 : -1, 0);
        
        return new Vector2Int(0, v.y > 0 ? 1 : -1);
    }

    // ─────────────────────────────────────────────
    // Movement / Actions
    // ─────────────────────────────────────────────
    private void TryMove(Vector2Int direction)
    {
        Vector3Int nextCell = cellPos + new Vector3Int(direction.x, direction.y, 0);

        if (!grid.CanEnterCell(nextCell))
            return;

        cellPos = nextCell;

        // Move player to cell center
        rb.MovePosition(grid.CellToWorldCenter(cellPos));
        
        // Apply scoring
        RegisterActionScore("Move");

        // Apply tile enter effects (e.g., reset)
        Vector3 fallStartWorld = grid.CellToWorldCenter(cellPos);
        grid.HandleEnteredCell(cellPos, this, fallStartWorld);
    }

    private void TryInteract()
    {
        Vector3Int targetCell = cellPos + new Vector3Int(lastDirection.x, lastDirection.y, 0);

        if (grid.TryGetObstacle(targetCell, out ObstacleBase obstacle) && obstacle != null)
        {
            // Debug.Log($"Obstacle {obstacle.name} to interact with at {targetCell}");
            obstacle.Interact();
        }

        // Optional: debug if nothing to interact with
        // Debug.Log($"No obstacle to interact with at {targetCell}");
    }

    #endregion
    
    #region Void Fall
    public void StartVoidFallReset(Vector3Int startCell, Vector3 fallStartWorld)
    {
        if (isResetting) return;

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = StartCoroutine(VoidFallResetRoutine(startCell, fallStartWorld));
    }
    
    private IEnumerator VoidFallResetRoutine(Vector3Int startCell, Vector3 fallStartWorld)
    {
        isResetting = true;

        rb.linearVelocity = Vector2.zero;

        // Make sure we're visually positioned on the void tile before shrinking
        rb.position = fallStartWorld;

        if (voidShakeDuration > 0f && voidShakeMagnitude > 0f)
        {
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(CameraShakeRoutine(voidShakeDuration, voidShakeMagnitude));
        }

        Vector3 startPos = fallStartWorld;
        Vector3 endPos = startPos + Vector3.down * voidFallDropDistance;

        float t = 0f;
        while (t < voidFallDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / voidFallDuration);
            float eased = a * a;

            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, eased);
            rb.MovePosition(Vector3.Lerp(startPos, endPos, eased));

            yield return null;
        }

        TeleportToCell(startCell);
        
        yield return new WaitForSeconds(voidFallDuration);
        
        transform.localScale = initialScale;

        isResetting = false;
    }

    private IEnumerator CameraShakeRoutine(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        Transform ct = cam.transform;
        Vector3 original = ct.localPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;

            // Random inside unit circle, scaled
            Vector2 offset2 = Random.insideUnitCircle * magnitude;
            ct.localPosition = original + new Vector3(offset2.x, offset2.y, 0f);

            yield return null;
        }

        ct.localPosition = original;
    }
    
    #endregion
    // ─────────────────────────────────────────────
    #region Scoring

    /// <summary>
    /// Determines rhythm quality, calculates points, and notifies ScoreManager.
    /// </summary>
    private void RegisterActionScore(string actionType)
    {
        BeatHitQuality quality = RhythmManager.Instance.GetHitQuality();
        int points = Utilities.GetPointsForQuality(quality);

        ScoreManager.Instance.RegisterMove();
        ScoreManager.Instance.AddRhythmScore(points, quality);
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Helpers

    /// <summary>
    /// Moves the player instantly to a new cell position.
    /// </summary>
    public void TeleportToCell(Vector3Int newCell)
    {
        cellPos = newCell;
        rb.position = grid.CellToWorldCenter(cellPos);
    }
    
    /// <summary>
    /// Flip the player sprite.
    /// </summary>
    /// <param name="faceRight"></param>
    private void Flip(bool faceRight)
    {
        FacingRight = faceRight;

        // In Unity 2D, flipX = true usually means facing LEFT
        if (sr != null)
            sr.flipX = !FacingRight;
    }

    public Vector3Int CellPosition => cellPos;
    public Vector2Int GridPosition => new (cellPos.x, cellPos.y);
    public Vector2Int FacingDirection => lastDirection;
    public bool ChangeShadowMode(bool isShadowMode) => IsShadowMode = isShadowMode;
    #endregion
}

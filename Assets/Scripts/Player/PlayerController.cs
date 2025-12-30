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
    [Header("Shape Placement")]
    [SerializeField] private ShapePlacer shapePlacer;

    [Header("Input (New Input System)")]
    [SerializeField] private InputActionReference moveActionRef;
    [SerializeField] private InputActionReference interactActionRef;
    [SerializeField] private InputActionReference placementModeActionRef;
    [SerializeField] private InputActionReference placeActionRef;
    [SerializeField] private InputActionReference rotateActionRef;      // float axis
    [SerializeField] private InputActionReference cycleShapeActionRef;  // float axis

    private InputAction moveAction;
    private InputAction interactAction;
    
    private InputAction placementModeAction;
    private InputAction placeAction;
    private InputAction rotateAction;
    private InputAction cycleShapeAction;

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
    
    [Tooltip("Cinemachine Impulse force used for shake on void fall (or damage, etc).")]
    [SerializeField] private float voidShakeForce = 0.7f;

    private bool isResetting;
    private Vector3 initialScale;
    private Coroutine resetRoutine;
    
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
        // MOVE
        if (moveActionRef != null)
        {
            moveAction = moveActionRef.action;
            moveAction.performed += OnMovePerformed;
            moveAction.Enable();
        }

        // INTERACT
        if (interactActionRef != null)
        {
            interactAction = interactActionRef.action;
            interactAction.performed += OnInteractPerformed;
            interactAction.Enable();
        }

        // PLACEMENT MODE TOGGLE
        if (placementModeActionRef != null)
        {
            placementModeAction = placementModeActionRef.action;
            placementModeAction.performed += OnPlacementModePerformed;
            placementModeAction.Enable();
        }

        // PLACE CONFIRM
        if (placeActionRef != null)
        {
            placeAction = placeActionRef.action;
            placeAction.performed += OnPlacePerformed;
            placeAction.Enable();
        }

        // ROTATE (AXIS: -1 / +1)
        if (rotateActionRef != null)
        {
            rotateAction = rotateActionRef.action;
            rotateAction.performed += OnRotatePerformed;
            rotateAction.Enable();
        }

        // CYCLE SHAPE (AXIS: -1 / +1)
        if (cycleShapeActionRef != null)
        {
            cycleShapeAction = cycleShapeActionRef.action;
            cycleShapeAction.performed += OnCycleShapePerformed;
            cycleShapeAction.Enable();
        }
    }

    private void OnDisable()
    {
        // MOVE
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.Disable();
            moveAction = null;
        }

        // INTERACT
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
            interactAction.Disable();
            interactAction = null;
        }

        // PLACEMENT MODE TOGGLE
        if (placementModeAction != null)
        {
            placementModeAction.performed -= OnPlacementModePerformed;
            placementModeAction.Disable();
            placementModeAction = null;
        }

        // PLACE CONFIRM
        if (placeAction != null)
        {
            placeAction.performed -= OnPlacePerformed;
            placeAction.Disable();
            placeAction = null;
        }

        // ROTATE
        if (rotateAction != null)
        {
            rotateAction.performed -= OnRotatePerformed;
            rotateAction.Disable();
            rotateAction = null;
        }

        // CYCLE SHAPE
        if (cycleShapeAction != null)
        {
            cycleShapeAction.performed -= OnCycleShapePerformed;
            cycleShapeAction.Disable();
            cycleShapeAction = null;
        }
    }
    #endregion

    #region Input Callback Stubs

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
    
    private void OnPlacementModePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting) 
            return;
        
        shapePlacer?.TogglePlacementMode();
    }

    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting) 
            return;
        
        shapePlacer?.TryPlace();
    }

    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting) 
            return;
        
        if (shapePlacer == null) 
            return;

        float v = ctx.ReadValue<float>();
        
        if (Mathf.Abs(v) < 0.5f) 
            return;

        if (v > 0) 
            shapePlacer.RotateCW();
        else 
            shapePlacer.RotateCCW();
    }

    private void OnCycleShapePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting) 
            return;
        
        if (shapePlacer == null) 
            return;

        float v = ctx.ReadValue<float>();
        
        if (Mathf.Abs(v) < 0.5f) 
            return;

        if (v > 0) 
            shapePlacer.CycleNext();
        else 
            shapePlacer.CyclePrev();
    }
    #endregion
    
    #region Actions
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

        // Ensure we start the fall *on the tile we stepped onto*
        rb.position = fallStartWorld;

        // Cinemachine Impulse shake (reusable for hurt, void, etc.)
        if (voidShakeForce > 0f)
            CameraShake.Instance?.Shake(voidShakeForce);

        Vector3 startPos = fallStartWorld;
        Vector3 endPos = startPos + Vector3.down * voidFallDropDistance;

        float t = 0f;
        while (t < voidFallDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / voidFallDuration);
            float eased = a * a; // ease-in

            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, eased);
            rb.MovePosition(Vector3.Lerp(startPos, endPos, eased));

            yield return null;
        }

        TeleportToCell(startCell);
        
        yield return new WaitForSeconds(voidFallDuration);
        
        transform.localScale = initialScale;

        isResetting = false;
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

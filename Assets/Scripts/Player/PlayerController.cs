using UnityEngine;

using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

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
    [SerializeField] private InputActionReference pauseActionRef;
    
    [Header("Melee Powerup")]
    [SerializeField] private GameObject meleeSwingPrefab;
    [SerializeField] private GameObject meleeHitVfxPrefab;
    [SerializeField] private float meleeHitVfxDelay = 0.05f;
    [SerializeField] private Color meleeValidColor = new(0f, 1f, 1f, 0.5f);
    [SerializeField] private Color meleeInvalidColor = new(1f, 0f, 0f, 0.5f);
    
    [Header("Fog VFX")]
    [SerializeField] private VisualEffect fogVFX;
    [SerializeField] private Vector3 fogCenterOffset = new (4f, 1f, 0f);
    
    [Header("Death Explosion")]
    [Tooltip("Body part prefabs to spawn when the player dies. Each prefab should have a Rigidbody2D.")]
    [SerializeField] private GameObject[] deathPartPrefabs;
    [Tooltip("The particle effect for the death explosion.")]
    [SerializeField] private GameObject deathVfxPrefab;
    [Tooltip("How many seconds the spawned parts should exist before being destroyed.")]
    [SerializeField] private float deathPartLifetime = 3.5f;
    [Tooltip("Impulse force range applied to each spawned part.")]
    [SerializeField] private Vector2 deathPartForceRange = new(3.5f, 6.5f);
    [Tooltip("Random torque range applied to each spawned part.")]
    [SerializeField] private Vector2 deathPartTorqueRange = new(40f, 140f);
    [Tooltip("Small random spawn offset from the player's center.")]
    [SerializeField] private float deathPartSpawnRadius = 0.05f;
    [Tooltip("If true, will also hide the player sprite/anim immediately when spawning parts.")]
    [SerializeField] private bool hideBodyOnDeath = true;
    
    [Header("MinimapIcon")] 
    [Tooltip("The minimap icon for the player icon.")]
    [SerializeField] private GameObject minimapIcon;

    private static readonly Vector3[] DIRECTIONS = new []
    {
        new Vector3(0,0,0), // Up
        new Vector3(0,0,-90), // Right
        new Vector3(0,0,90), // Left
        new Vector3(0,0,180), // Down
    };

    private InputAction moveAction;
    private InputAction interactAction;
    
    private InputAction placementModeAction;
    private InputAction placeAction;
    private InputAction rotateAction;
    private InputAction cycleShapeAction;
    private InputAction pauseAction;

    private Vector3Int cellPos;
    private Vector2Int lastDirection = Vector2Int.right;

    private SpriteRenderer sr;
    private Animator anim;
    private Rigidbody2D rb;
    private TilemapGridManager grid;
    private ObstacleBase currentPrompt;
    private bool isMeleePowerupActive;
    private int meleeTelegraphOwnerId;
    
    // Shadow mode state
    public bool IsShadowMode { get; private set; } = false;

    // Facing state
    public bool FacingRight { get; private set; } = true;
    
    // Death state
    public bool IsDead { get; private set; } = false;

    // Void reset VFX fields
    [Header("Void Fall Reset")]
    [SerializeField] private float voidFallDuration = 0.35f;
    [SerializeField] private float voidFallDropDistance = 0.25f;
    
    [Tooltip("Cinemachine Impulse force used for shake on void fall (or damage, etc).")]
    [SerializeField] private float voidShakeForce = 0.7f;

    private bool isResetting;
    private Vector3 initialScale;
    private Coroutine resetRoutine;
    private AudioManager audioManager;
    
    // ─────────────────────────────────────────────
    #region Unity Events
    // ─────────────────────────────────────────────
    /// <summary>
    /// Initializes component references and snaps the player to the start cell.
    /// </summary>
    private void Start() 
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (anim == null)
            anim = GetComponent<Animator>();
        
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
        
        // Rotate minimap icon to face right
        if(minimapIcon != null)
            minimapIcon.transform.localRotation = Quaternion.Euler(DIRECTIONS[1]);
        
        meleeTelegraphOwnerId = GetInstanceID() * 31 + 7;
        
        audioManager = AudioManager.Instance;
    }

    /// <summary>
    /// Per-frame update used to short-circuit input when the game is frozen.
    /// </summary>
    private void Update()
    {
        if (pauseAction == null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
        
        // Stop player input if game is frozen
        // Temporary fallback if you haven't wired interact yet
        if (Utilities.IsGameFrozen || isResetting || IsDead) 
            return;
        
        // Move fog vfx with player
        if(fogVFX  != null)
            fogVFX.SetVector3("ColliderPos", transform.position + fogCenterOffset);
        
        // Check for prompts
        UpdateInteractPrompt();
        
        if (isMeleePowerupActive)
            UpdateMeleePreview();
        else
            ClearMeleePreview();
    }
    
    /// <summary>
    /// Binds and enables input actions.
    /// </summary>
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
        
        // PAUSE TOGGLE
        if (pauseActionRef != null)
        {
            pauseAction = pauseActionRef.action;
            pauseAction.performed += OnPausePerformed;
            pauseAction.Enable();
        }
    }

    /// <summary>
    /// Unbinds and disables input actions.
    /// </summary>
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
        
        // PAUSE TOGGLE
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
            pauseAction.Disable();
            pauseAction = null;
        }
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Input Callback Stubs
    /// <summary>
    /// Handles movement input and attempts to move the player.
    /// </summary>
    /// <param name="ctx">Input callback context.</param>
    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting || IsDead) 
            return;

        Vector2 v = ctx.ReadValue<Vector2>();
        Vector2Int dir = ToCardinal(v);
        if (dir == Vector2Int.zero) return;

        lastDirection = dir;

        // Only update facing on horizontal input
        if (dir.x < 0 && dir.y == 0)
        {
            if(minimapIcon != null)
                minimapIcon.transform.localRotation = Quaternion.Euler(DIRECTIONS[2]);
            Flip(false);
        }
        else if (dir.x > 0 && dir.y == 0)
        {
            if (minimapIcon != null)
                minimapIcon.transform.localRotation = Quaternion.Euler(DIRECTIONS[1]);
            Flip(true);
        }
        else if (dir.y < 0 && dir.x == 0)
        {
            if (minimapIcon != null)
                minimapIcon.transform.localRotation = Quaternion.Euler(DIRECTIONS[3]);
        }
        else if (dir.y > 0 && dir.x == 0)
        {
            if (minimapIcon != null)
                minimapIcon.transform.localRotation = Quaternion.Euler(DIRECTIONS[0]);
        }

        TryMove(dir);
    }
    
    /// <summary>
    /// Handles interact input and attempts to use the forward obstacle.
    /// </summary>
    /// <param name="ctx">Input callback context.</param>
    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting || IsDead) 
            return;
        
        TryInteract();
    }
    
    /// <summary>
    /// Handles pause input and toggles the pause menu.
    /// </summary>
    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        TogglePause();
    }

    private void TogglePause()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause();
    }

    /// <summary>
    /// Converts a raw input vector into a cardinal grid direction.
    /// </summary>
    /// <param name="v">Raw input vector.</param>
    /// <returns>Cardinal direction or zero if input is negligible.</returns>
    private static Vector2Int ToCardinal(Vector2 v)
    {
        // With WASD composites, v is usually already cardinal,
        // but this makes it robust (and avoids diagonal if two keys pressed).
        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);

        if (ax < 0.01f && ay < 0.01f) return Vector2Int.zero;

        if (ax >= ay)
        {
            return new Vector2Int(v.x > 0 ? 1 : -1, 0);
        }

        return new Vector2Int(0, v.y > 0 ? 1 : -1);
    }
    
    /// <summary>
    /// Handles input for toggling shape placement mode.
    /// </summary>
    /// <param name="ctx">Input callback context.</param>
    private void OnPlacementModePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting || isMeleePowerupActive || IsDead) 
            return;
        
        shapePlacer?.TogglePlacementMode();
    }

    /// <summary>
    /// Handles input for confirming a shape placement.
    /// </summary>
    /// <param name="ctx">Input callback context.</param>
    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting || IsDead) 
            return;
        
        if (isMeleePowerupActive)
        {
            TryHit();
            return;
        }
        
        shapePlacer?.TryPlace();
    }

    /// <summary>
    /// Handles input for rotating the placement shape.
    /// </summary>
    /// <param name="ctx">Input callback context.</param>
    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting || IsDead) 
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

    /// <summary>
    /// Handles input for cycling through available shapes.
    /// </summary>
    /// <param name="ctx">Input callback context.</param>
    private void OnCycleShapePerformed(InputAction.CallbackContext ctx)
    {
        if (Utilities.IsGameFrozen || isResetting || IsDead) 
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
    // ─────────────────────────────────────────────
    #region Actions
    /// <summary>
    /// Attempts to move the player, unlocking gates if possible and applying enter effects.
    /// </summary>
    /// <param name="direction">Cardinal direction to move.</param>
    private void TryMove(Vector2Int direction)
    {
        Vector3Int nextCell = cellPos + new Vector3Int(direction.x, direction.y, 0);

        // If blocked, see if it's a gate and we can unlock it
        if (!grid.CanEnterCell(nextCell))
        {
            var inv = GetComponent<PlayerInventory>();
            if (grid.IsGateCell(nextCell, out _) && grid.TryUnlockGateAt(nextCell, inv))
            {
                // Now that it's unlocked, we should be able to enter.
                if (!grid.CanEnterCell(nextCell))
                    return;
            }
            else
            {
                return;
            }
        }

        cellPos = nextCell;

        // Move player to cell center
        rb.MovePosition(grid.CellToWorldCenter(cellPos));
        
        // Move player anim
        if (anim != null)
            anim.SetTrigger("Moving");
        
        // Play SFX
        if (audioManager != null)
            audioManager.PlaySFX("move_step", 0.3f);
        
        // Apply scoring
        RegisterActionScore("Move");

        // Apply tile enter effects (e.g., reset)
        Vector3 fallStartWorld = grid.CellToWorldCenter(cellPos);
        grid.HandleEnteredCell(cellPos, this, fallStartWorld);
    }

    /// <summary>
    /// Attempts to interact with the obstacle in front of the player.
    /// </summary>
    private void TryInteract()
    {
        Vector3Int targetCell = cellPos + new Vector3Int(lastDirection.x, lastDirection.y, 0);

        if (grid.TryGetObstacle(targetCell, out ObstacleBase obstacle) && obstacle != null)
        {
            // Debug.Log($"Obstacle {obstacle.name} to interact with at {targetCell}");
            // Apply scoring
            // RegisterActionScore("Obstacle");
            obstacle.Interact();
        }

        // Optional: debug if nothing to interact with
        // Debug.Log($"No obstacle to interact with at {targetCell}");
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Melee Powerup
    /// <summary>
    /// Enables or disables melee powerup behavior and previews.
    /// </summary>
    /// <param name="active">Whether the melee powerup is active.</param>
    public void SetMeleePowerupActive(bool active)
    {
        isMeleePowerupActive = active;

        if (!active)
            ClearMeleePreview();
    }

    private void UpdateMeleePreview()
    {
        if (grid == null)
            return;

        var targetCell = GetMeleeTargetCell();

        if (!grid.IsInBounds(targetCell))
        {
            ClearMeleePreview();
            return;
        }

        bool canHit = grid.CanEnterCell(targetCell);

        var cells = new List<Vector3Int> { targetCell };
        var color = canHit ? meleeValidColor : meleeInvalidColor;

        grid.SetPreviewCellsForOwner(meleeTelegraphOwnerId, cells, new List<Color> { color });
    }

    public void ClearMeleePreview()
    {
        grid?.ClearPreviewForOwner(meleeTelegraphOwnerId);
    }

    private Vector3Int GetMeleeTargetCell()
    {
        Vector2Int direction = lastDirection;
        if (direction == Vector2Int.zero)
            direction = Vector2Int.right;

        return cellPos + new Vector3Int(direction.x, direction.y, 0);
    }

    private void TryHit()
    {
        if (grid == null)
            return;

        var targetCell = GetMeleeTargetCell();

        if (!grid.IsInBounds(targetCell))
            return;

        if (!grid.CanEnterCell(targetCell))
            return;

        Vector3 targetWorld = grid.CellToWorldCenter(targetCell);

        if (meleeSwingPrefab != null)
            VFXPoolManager.Instance?.Get(meleeSwingPrefab, targetWorld);

        if (meleeHitVfxPrefab != null)
            StartCoroutine(SpawnMeleeHitVfx(targetWorld));
        
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy == null)
                continue;

            if (enemy.CellPosition != targetCell)
                continue;

            enemy.KillByMeleeStrike();
            RegisterActionScore("MeleeHit");
        }

        var shield = FindFirstObjectByType<BossShieldHealth>();
        var queen = FindFirstObjectByType<VampireQueenBossController>();
        if (shield != null && shield.IsCellShielded(targetCell))
        {
            if (shield.IsBroken)
            {
                if (queen != null)
                    queen.TryRegisterDirectHit();
            }
            else
            {
                shield.ApplyDamage(1);
            }

            return;
        }

        if (queen == null)
            return;

        if (queen.IsCellInDamageableFootprint(targetCell))
        {
            queen.TryRegisterDirectHit();
            return;
        }

        if (queen.IsCellInVulnerableFootprint(targetCell))
            queen.TryRegisterVulnerabilityHit();
    }

    private IEnumerator SpawnMeleeHitVfx(Vector3 targetWorld)
    {
        if (meleeHitVfxDelay > 0f)
            yield return new WaitForSeconds(meleeHitVfxDelay);

        AudioManager.Instance?.PlaySFX("impact_two", 0.5f);
        VFXPoolManager.Instance?.Get(meleeHitVfxPrefab, targetWorld);
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Void Fall
    /// <summary>
    /// Starts a void fall reset animation and respawns the player at the start cell.
    /// </summary>
    public void StartVoidFallReset(Vector3Int startCell, Vector3 fallStartWorld)
    {
        if (isResetting) 
            return;

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);
        
        if (audioManager != null)
            audioManager.PlaySFX("fall_down", 0.4f);

        resetRoutine = StartCoroutine(VoidFallRoutine(
            fallStartWorld,
            onComplete: () =>
            {
                TeleportToCell(startCell);
                StartCoroutine(RestoreAfterDelay(voidFallDuration));
            }));
    }
    
    /// <summary>
    /// Starts a void fall animation and then kills the player (boss void).
    /// </summary>
    public void StartVoidFallDeath(Vector3 fallStartWorld)
    {
        if (isResetting)
            return;

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);
        
        if (audioManager != null)
            audioManager.PlaySFX("fall_down", 0.4f);

        resetRoutine = StartCoroutine(VoidFallRoutine(
            fallStartWorld,
            onComplete: () =>
            {
                // Restore scale so death screen doesn't show a tiny player sprite in-scene
                transform.localScale = initialScale;

                // Trigger death flow
                GameManager.Instance?.PlayerKilled();
            }));
    }

    /// <summary>
    /// Shared fall routine (shrink + drop + shake). Calls onComplete at the end.
    /// </summary>
    private IEnumerator VoidFallRoutine(Vector3 fallStartWorld, System.Action onComplete)
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

        onComplete?.Invoke();
    }

    private IEnumerator RestoreAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        transform.localScale = initialScale;
        isResetting = false;
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Death
    /// <summary>
    /// Hides the player body (sprite + animator). Useful for death flows where you want to show
    /// other effects (e.g. exploding parts) without keeping the player visible.
    /// </summary>
    public void HideBody()
    {
        if (sr != null)
            sr.enabled = false;

        if (anim != null)
            anim.enabled = false;
    }

    /// <summary>
    /// Spawns the configured body part prefabs at the player's position and applies a random
    /// physics impulse + torque to each.
    /// </summary>
    public void SpawnDeathParts()
    {
        if (hideBodyOnDeath)
            HideBody();
        
        IsDead = true;

        if (deathPartPrefabs == null || deathPartPrefabs.Length == 0)
            return;

        Vector3 origin = transform.position;

        for (int i = 0; i < deathPartPrefabs.Length; i++)
        {
            var prefab = deathPartPrefabs[i];
            if (prefab == null)
                continue;

            Vector2 randomOffset = Random.insideUnitCircle * deathPartSpawnRadius;
            Vector3 spawnPos = origin + new Vector3(randomOffset.x, randomOffset.y, 0f);

            GameObject part = Instantiate(prefab, spawnPos, Quaternion.identity);

            Rigidbody2D partRb = part.GetComponent<Rigidbody2D>();
            if (partRb != null)
            {
                // Random direction (slightly biased upward so it reads as an "explosion")
                Vector2 dir = Random.insideUnitCircle;
                dir.y = Mathf.Abs(dir.y) + 0.15f;
                if (dir.sqrMagnitude < 0.0001f)
                    dir = Vector2.up;
                dir.Normalize();

                float force = Random.Range(deathPartForceRange.x, deathPartForceRange.y);
                partRb.AddForce(dir * force, ForceMode2D.Impulse);

                float torque = Random.Range(deathPartTorqueRange.x, deathPartTorqueRange.y);
                if (Random.value < 0.5f) torque *= -1f;
                partRb.AddTorque(torque, ForceMode2D.Impulse);
            }

            if (deathPartLifetime > 0f)
                Destroy(part, deathPartLifetime);
        }
        
        if (deathVfxPrefab != null)
            Instantiate(deathVfxPrefab, origin, Quaternion.identity);
    }
    #endregion
    // ─────────────────────────────────────────────
    #region Scoring
    /// <summary>
    /// Determines rhythm quality, calculates points, and notifies ScoreManager.
    /// </summary>
    /// <param name="actionType">Action label used for logging or future extensions.</param>
    private void RegisterActionScore(string actionType)
    {
        if (ScoreManager.Instance == null)
            return;
        
        BeatHitQuality quality = RhythmManager.Instance.GetHitQuality();
        int points = Utilities.GetPointsForQuality(quality);

        ScoreManager.Instance.RegisterMove();
        ScoreManager.Instance.AddRhythmScore(points, quality);
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Interact Prompts
    private void UpdateInteractPrompt()
    {
        if (TilemapGridManager.Instance == null)
        {
            ClearPrompt(); 
            return;
        }

        Vector3Int targetCell = cellPos + new Vector3Int(lastDirection.x, lastDirection.y, 0);

        if (TilemapGridManager.Instance.TryGetObstacle(targetCell, out ObstacleBase obstacle) &&
            obstacle != null &&
            obstacle.CanInteract(this))
        {
            if (currentPrompt != obstacle)
            {
                ClearPrompt();
                currentPrompt = obstacle;
                currentPrompt.ShowPrompt(this);
            }
            return;
        }

        ClearPrompt();
    }

    private void ClearPrompt()
    {
        if (currentPrompt != null)
        {
            currentPrompt.HidePrompt();
            currentPrompt = null;
        }
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
    /// <param name="faceRight">Whether the player should face right.</param>
    private void Flip(bool faceRight)
    {
        FacingRight = faceRight;

        // In Unity 2D, flipX = true usually means facing LEFT
        if (sr != null)
            sr.flipX = !FacingRight;
    }

    /// <summary>
    /// Gets the current cell position for the player.
    /// </summary>
    public Vector3Int CellPosition => cellPos;

    /// <summary>
    /// Gets the current grid position as a 2D coordinate.
    /// </summary>
    public Vector2Int GridPosition => new(cellPos.x, cellPos.y);

    /// <summary>
    /// Gets the last movement direction used for interactions.
    /// </summary>
    public Vector2Int FacingDirection => lastDirection;
    
    /// <summary>
    /// Gets player's shapeplacer component
    /// </summary>
    public ShapePlacer GetShapePlacer => shapePlacer;

    /// <summary>
    /// Enables or disables shadow mode on the player.
    /// </summary>
    /// <param name="isShadowMode">True to enable shadow mode.</param>
    /// <returns>The new shadow mode state.</returns>
    public void ChangeShadowMode(bool isShadowMode) => IsShadowMode = isShadowMode;
    #endregion
}

using System.Collections;
using UnityEngine;

/// <summary>
/// Rhythm-driven, grid-aware enemy base.
/// - Subscribes to RhythmManager.OnBeat and acts every N beats.
/// - Uses GridManager/Tile rules to move only onto walkable tiles.
/// - Uses Unity Physics for instant contact with the Player.
/// 
/// Derive and override:
///   - OnBeatAction() : what the enemy does on its active beat (move, attack, idle).
///   - OnPlayerContact(PlayerController player) : custom contact behavior (default = reset).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] protected bool snapToGridOnStart = true;

    [Header("Rhythm")]
    [Tooltip("Act every N beats (1 = every beat, 2 = every second beat, etc).")]
    [SerializeField] protected int beatsPerAction = 1;
    [Tooltip("Offset in beats before first action (0 = act on first eligible beat).")]
    [SerializeField] protected int beatOffset = 0;

    [Header("Contact")]
    [Tooltip("If true, contacting the player triggers a level reset.")]
    [SerializeField] protected bool lethalOnContact = true;

    [Header("Damage Force")]
    [Tooltip("If true, allow camera shake.")]
    [SerializeField] protected bool allowDamageShake = true;
    [Tooltip("Shake force for camera on damage.")]
    [SerializeField] protected float damageShakeForce = 0.8f;

    [Header("Void Elimination")]
    [Tooltip("How long the enemy takes to shrink/fall before being destroyed.")]
    [SerializeField] protected float voidDeathDuration = 0.25f;
    [Tooltip("How far the enemy sinks down while falling.")]
    [SerializeField] protected float voidDeathDropDistance = 0.25f;

    protected TilemapGridManager grid;
    protected PlayerController player;
    protected Animator animator;

    protected Vector3Int cellPos;

    private int localBeatCounter;
    private bool isDying;

    // ─────────────────────────────────────────────────────────────
    #region Unity lifecycle
    /// <summary>
    /// Subscribes to the rhythm beat event when the enemy becomes active.
    /// </summary>
    protected virtual void OnEnable()
    {
        RhythmManager.OnBeat += HandleBeat;
    }
    
    /// <summary>
    /// Unsubscribes from the rhythm beat event when the enemy is disabled.
    /// </summary>
    protected virtual void OnDisable()
    {
        RhythmManager.OnBeat -= HandleBeat;
    }

    /// <summary>
    /// Resolves grid, animator, and player references and snaps to the grid if requested.
    /// </summary>
    protected virtual void Start()
    {
        grid = TilemapGridManager.Instance;
        animator = GetComponent<Animator>();
        player = GameManager.Instance != null ? GameManager.Instance.player : null;

        // Ensure collider works for triggers
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        if (grid != null)
        {
            cellPos = grid.WorldToCell(transform.position);
            if (snapToGridOnStart)
                transform.position = grid.CellToWorldCenter(cellPos);
        }
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Rhythm
    /// <summary>
    /// Handles rhythm beat callbacks and triggers actions on the configured cadence.
    /// </summary>
    private void HandleBeat()
    {
        if (Utilities.IsGameFrozen) 
            return; // skip beat if frozen
        
        if (isDying) 
            return;

        int phase = localBeatCounter + beatOffset;
        
        if (beatsPerAction <= 0) 
            beatsPerAction = 1;

        if (phase % beatsPerAction == 0)
        {
            OnBeatAction();
        }

        localBeatCounter++;
    }

    /// <summary>
    /// Override in subclasses to perform actions on active beats.
    /// </summary>
    protected abstract void OnBeatAction();

    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Movement helpers
    /// <summary>
    /// Attempts to move the enemy by a grid direction if the destination is valid.
    /// </summary>
    /// <param name="dir">Grid direction delta.</param>
    /// <returns>True if the move succeeds.</returns>
    protected bool TryMove(Vector3Int dir)
    {
        if (isDying) return false;

        Vector3Int next = cellPos + dir;
        if (!CanEnter(next)) return false;

        cellPos = next;
        transform.position = grid.CellToWorldCenter(cellPos);
        return true;
    }

    /// <summary>
    /// Checks if the enemy can enter the requested grid cell.
    /// </summary>
    /// <param name="cell">Grid coordinate to test.</param>
    /// <returns>True if the cell is walkable for enemies.</returns>
    protected bool CanEnter(Vector3Int cell)
    {
        if (grid == null) 
            return false;
        
        return grid.CanEnemyEnterCell(cell);
    }
    
    /// <summary>
    /// Instantly moves the enemy to a grid cell without interpolation.
    /// </summary>
    /// <param name="cell">Target grid coordinate.</param>
    public void WarpTo(Vector3Int cell)
    {
        if (isDying) 
            return;

        cellPos = cell;
        if (grid != null)
            transform.position = grid.CellToWorldCenter(cellPos);
    }
    
    /// <summary>
    /// Gets the current grid cell position of the enemy.
    /// </summary>
    public Vector3Int CellPosition => cellPos;
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Void elimination
    /// <summary>
    /// Called when the tile under the enemy becomes Void.
    /// Plays a fall/shrink animation and destroys the enemy.
    /// </summary>
    public void KillByVoidFall(Vector3 fallStartWorld)
    {
        if (isDying) return;
        StartCoroutine(VoidDeathRoutine(fallStartWorld));
    }

    /// <summary>
    /// Runs a shrink-and-fall animation before destroying the enemy.
    /// </summary>
    /// <param name="fallStartWorld">World-space start position for the fall.</param>
    private IEnumerator VoidDeathRoutine(Vector3 fallStartWorld)
    {
        isDying = true;

        // stop participating in gameplay
        lethalOnContact = false;

        // snap to the cell where the void happened (visual consistency)
        transform.position = fallStartWorld;

        Vector3 startScale = transform.localScale;
        Vector3 startPos = fallStartWorld;
        Vector3 endPos = startPos + Vector3.down * voidDeathDropDistance;

        float t = 0f;
        while (t < voidDeathDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / voidDeathDuration);
            float eased = a * a; // ease-in

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            transform.position = Vector3.Lerp(startPos, endPos, eased);

            yield return null;
        }

        Destroy(gameObject);
    }
    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Contact handling (Physics)
    /// <summary>
    /// Detects trigger contact with the player and applies contact behavior.
    /// </summary>
    /// <param name="other">Collider that entered the trigger.</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!lethalOnContact) return;

        if (other.CompareTag("Player"))
        {
            OnPlayerContact();
        }
    }

    /// <summary>
    /// Default contact behavior: reset the level.
    /// Override to customize (knockback, SFX, etc).
    /// </summary>
    protected void OnPlayerContact()
    {
        if (player != null && player.IsShadowMode)
            return;
        
        if (GameManager.Instance != null)
            GameManager.Instance.PlayerKilled();
    }
    #endregion
}

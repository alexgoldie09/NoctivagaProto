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
    [SerializeField] protected Vector2Int gridPos;
    [SerializeField] protected bool snapToGridOnStart = true;

    [Header("Rhythm")]
    [Tooltip("Act every N beats (1 = every beat, 2 = every second beat, etc).")]
    [SerializeField] protected int beatsPerAction = 1;
    [Tooltip("Offset in beats before first action (0 = act on first eligible beat).")]
    [SerializeField] protected int beatOffset = 0;

    [Header("Contact")]
    [Tooltip("If true, contacting the player triggers a level reset.")]
    [SerializeField] protected bool lethalOnContact = true;

    protected GridManager grid;          // Grid reference
    protected PlayerController player;   // Player reference
    protected Animator animator;         // Animator reference
    protected int localBeatCounter = 0;  // Rhythm counter

    // ─────────────────────────────────────────────────────────────
    #region Unity lifecycle

    protected virtual void OnEnable()
    {
        RhythmManager.OnBeat += HandleBeat;
    }

    protected virtual void OnDisable()
    {
        RhythmManager.OnBeat -= HandleBeat;
    }

    protected virtual void Start()
    {
        if (snapToGridOnStart)
        {
            gridPos = Vector2Int.RoundToInt((Vector2)transform.position);
            transform.position = GridToWorld(gridPos);
        }

        grid = GridManager.Instance;
        animator = GetComponent<Animator>();
        player = GameManager.Instance.player;

        // Ensure collider works for triggers
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────

    #region Rhythm

    private void HandleBeat()
    {
        if (Utilities.IsGameFrozen) return; // skip beat if frozen
        
        int phase = (localBeatCounter + beatOffset);
        if (beatsPerAction <= 0) beatsPerAction = 1;

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

    protected bool TryStep(Vector2Int dir)
    {
        Vector2Int next = gridPos + dir;
        if (!CanEnter(next)) return false;

        gridPos = next;
        transform.position = GridToWorld(gridPos);
        return true;
    }

    protected bool CanEnter(Vector2Int pos)
    {
        if (grid == null) return false;
        if (!grid.IsInBounds(pos.x, pos.y)) return false;
        return grid.IsWalkable(pos.x, pos.y);
    }

    protected void WarpTo(Vector2Int pos)
    {
        gridPos = pos;
        transform.position = GridToWorld(pos);
    }

    protected Vector3 GridToWorld(Vector2Int pos) => new Vector3(pos.x, pos.y, 0f);

    #endregion
    // ─────────────────────────────────────────────────────────────

    #region Contact handling (Physics)

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
    protected virtual void OnPlayerContact()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.PlayerKilled();
    }

    #endregion
}

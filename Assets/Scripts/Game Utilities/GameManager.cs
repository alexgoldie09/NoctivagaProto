using UnityEngine;
using System.Collections;

/// <summary>
/// Central game flow manager.
/// Handles level reset and temporary powerup effects such as half-time rhythm and shadow mode.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [HideInInspector] public PlayerController player;
    
    [Header("Visual Effects")]
    [Tooltip("HalfTime Fog particle system that plays during half-time mode.")]
    [SerializeField] private ParticleSystem halfTimeFog;
    [Tooltip("HalfTime Player particle system that plays during half-time mode.")]
    [SerializeField] private ParticleSystem halfTimeFX;
    [Tooltip("Shadow Fog particle system that plays during shadow mode.")]
    [SerializeField] private ParticleSystem shadowModeFog;
    [Tooltip("Shadow Player particle system that plays during shadow mode.")]
    [SerializeField] private ParticleSystem shadowModeFX;

    [Header("UI")]
    [Tooltip("Reference to the Win Screen UI canvas (pre-placed, disabled by default).")]
    [SerializeField] private GameObject winScreenUI;
    [Tooltip("Reference to the Death Screen UI canvas (pre-placed, disabled by default).")]
    [SerializeField] private GameObject deathScreenUI;
    
    // Track currently active powerup
    private PowerupPickup.PowerupType? activePowerup = null;
    private Coroutine activePowerupRoutine = null;
    
    private void Awake()
    {
        if (Instance == null) 
            Instance = this;
        else 
            Destroy(gameObject);
        
        player = FindFirstObjectByType<PlayerController>();
    }
    
    // ─────────────────────────────────────────────
    #region Lose handling
    /// <summary>
    /// Called when the player is killed by an enemy.
    /// Hides the player, waits, then resets the level.
    /// </summary>
    public void PlayerKilled()
    {
        if (player == null) return;

        // Hide player object
        player.gameObject.SetActive(false);

        // Start delayed death screen
        StartCoroutine(ShowDeathScreenAfterDelay(3f)); // 3 second delay before UI fade
    }


    private IEnumerator ShowDeathScreenAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Freeze game
        Utilities.FreezeGame();
        
        // Pause rhythm/music
        if (RhythmManager.Instance != null)
            RhythmManager.Instance.Stop();

        // Show death screen
        if (deathScreenUI != null)
            deathScreenUI.SetActive(true);
    }
    
    #endregion
    // ─────────────────────────────────────────────
    #region Win handling

    /// <summary>
    /// Called when the player collides with the goal (waifu object).
    /// Finalizes score, freezes gameplay, pauses music, and shows the win screen UI.
    /// </summary>
    public void PlayerWon()
    {
        // Finalize score
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.FinalizeScore();

        // Freeze gameplay
        Utilities.FreezeGame();

        // Pause rhythm/music
        if (RhythmManager.Instance != null)
            RhythmManager.Instance.Stop();

        // Show Win Screen
        if (winScreenUI != null)
            winScreenUI.SetActive(true);
    }

    #endregion
    // ─────────────────────────────────────────────
    #region Powerups
    /// <summary>
    /// Attempts to activate a powerup effect.
    /// Returns true if activated, false if blocked.
    /// </summary>
    public bool TryActivatePowerup(PowerupPickup.PowerupType type, float duration)
    {
        // If another type is active, block
        if (activePowerup.HasValue && activePowerup.Value != type)
        {
            return false;
        }

        // If same type active, refresh timer
        if (activePowerupRoutine != null)
        {
            StopCoroutine(activePowerupRoutine);
            activePowerupRoutine = null;
        }

        activePowerup = type;

        switch (type)
        {
            case PowerupPickup.PowerupType.HalfTime:
                activePowerupRoutine = StartCoroutine(HalfTimeRoutine(duration));
                break;
            case PowerupPickup.PowerupType.ShadowMode:
                activePowerupRoutine = StartCoroutine(ShadowModeRoutine(duration));
                break;
        }

        return true;
    }
    
    /// <summary>
    /// Clears the active powerup state when a powerup finishes.
    /// </summary>
    private void ClearActivePowerup()
    {
        activePowerup = null;
        activePowerupRoutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Coroutine that slows down the rhythm system by half for a set duration.
    /// Music pitch and beat interval are adjusted via RhythmManager.
    /// </summary>
    private IEnumerator HalfTimeRoutine(float duration)
    {
        RhythmManager.Instance.SetTempoMultiplier(0.5f);
        GridManager.Instance.SetHalfTimeVisual(true); // turn grey

        if (halfTimeFog != null && halfTimeFX != null)
        {
            halfTimeFog.gameObject.SetActive(true);
            halfTimeFX.gameObject.SetActive(true);
            halfTimeFog.Play();
            halfTimeFX.Play();
        }

        yield return new WaitForSeconds(duration);

        RhythmManager.Instance.ResetTempo();
        GridManager.Instance.SetHalfTimeVisual(false); // restore

        if (halfTimeFog != null && halfTimeFX != null)
        {
            halfTimeFog.Stop();
            halfTimeFX.Stop();
            halfTimeFog.gameObject.SetActive(false);
            halfTimeFX.gameObject.SetActive(false);
        }
        
        ClearActivePowerup();
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Coroutine that makes the player hidden from enemies (shadow mode).
    /// While active, chasers do not target the player and the player sprite is semi-transparent.
    /// </summary>
    private IEnumerator ShadowModeRoutine(float duration)
    {
        if (player != null)
        {
            player.ChangeShadowMode(true);

            // Visual indicator: fade player sprite
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = new Color(1f, 1f, 1f, 0.02f);

            // Play particle FX
            if (shadowModeFX != null && shadowModeFog  != null)
            {
                shadowModeFX.gameObject.SetActive(true);
                shadowModeFog.gameObject.SetActive(true);
                shadowModeFX.Play();
                shadowModeFog.Play();
            }
        }

        yield return new WaitForSeconds(duration);

        if (player != null)
        {
            player.ChangeShadowMode(false);

            // Restore visuals
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = Color.white;

            // Stop FX
            if (shadowModeFX != null && shadowModeFog != null)
            {
                shadowModeFX.Stop();
                shadowModeFog.Stop();
                shadowModeFX.gameObject.SetActive(false);
                shadowModeFog.gameObject.SetActive(false);
            }
        }
        
        ClearActivePowerup(); 
    }
    #endregion
}

using UnityEngine;


/// <summary>
/// Shared helper functions used across systems.
/// Keep this focused on cross-cutting concerns (math, scoring, etc).
/// </summary>
public static class Utilities
{
    public static bool IsPlacementModeActive = false;
    
    /// <summary>
    /// Whether the game is currently frozen (e.g., win screen, pause).
    /// </summary>
    public static bool IsGameFrozen { get; private set; }

    /// <summary>
    /// Freezes all time-based gameplay by setting Time.timeScale = 0.
    /// </summary>
    public static void FreezeGame()
    {
        Time.timeScale = 0f;
        IsGameFrozen = true;
    }

    /// <summary>
    /// Unfreezes gameplay by restoring Time.timeScale = 1.
    /// </summary>
    public static void UnfreezeGame()
    {
        Time.timeScale = 1f;
        IsGameFrozen = false;
        IsPlacementModeActive = false;
    }
    
    /// <summary>
    /// Exits game.
    /// </summary>
    public static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    
    /// <summary>
    /// Returns point values for each rhythm hit quality.
    /// Centralized so PlayerController, ShapePlacer, etc. all use the same mapping.
    /// </summary>
    public static int GetPointsForQuality(BeatHitQuality quality)
    {
        switch (quality)
        {
            case BeatHitQuality.Perfect: return 40;
            case BeatHitQuality.Good:    return 20;
            case BeatHitQuality.Okay:    return 10;
            case BeatHitQuality.Bad:     return 0;
            default: return 0;
        }
    }
}

using System.IO;
using UnityEngine;

/// <summary>
/// Persistent save data for a single level.
/// Score-based levels use all three fields.
/// Boss levels only use 'completed' — highScore and highRating are ignored.
/// </summary>
[System.Serializable]
public class LevelSaveData
{
    public bool completed  = false;
    public int  highScore  = 0;
    public ScoreRating highRating = ScoreRating.Low;
}

/// <summary>
/// Root save data container. Holds one entry per level.
/// Indices 0–9 map to levels 1–10 (use levelIndex - 1 when accessing).
/// </summary>
[System.Serializable]
public class SaveData
{
    public LevelSaveData[] levels = new LevelSaveData[10];
}

/// <summary>
/// Static save system. No scene presence required — call directly from any script.
/// All data is stored as a single JSON file in Application.persistentDataPath.
///
/// Usage:
///   SaveManager.SaveLevel(levelIndex, finalScore, rating);   // score-based levels
///   SaveManager.SaveBossLevel(levelIndex);                   // boss levels
///   SaveData data = SaveManager.LoadData();                  // read all levels
///   SaveManager.ClearAllData();                              // wipe save file
/// </summary>
public static class SaveManager
{
    private static readonly string SavePath =
        Path.Combine(Application.persistentDataPath, "save.json");

    // ─────────────────────────────────────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// Saves the result of a score-based level. Only updates high score and rating
    /// if the new score beats the previously saved one. Always marks the level complete.
    /// </summary>
    /// <param name="levelIndex">1-based level index (1–8 for score levels).</param>
    /// <param name="newScore">Final score after penalties from ScoreManager.</param>
    /// <param name="newRating">Score rating from ScoreManager.GetScoreRating().</param>
    public static void SaveLevel(int levelIndex, int newScore, ScoreRating newRating)
    {
        SaveData data = LoadData();
        LevelSaveData level = GetLevel(data, levelIndex);

        level.completed = true;

        if (newScore > level.highScore)
        {
            level.highScore  = newScore;
            level.highRating = newRating;
        }

        WriteData(data);
        Debug.Log($"[SaveManager] Level {levelIndex} saved. Score: {newScore}, Rating: {newRating}");
    }

    /// <summary>
    /// Saves the result of a boss level. Only marks completion — no score is recorded.
    /// </summary>
    /// <param name="levelIndex">1-based level index (9–10 for boss levels).</param>
    public static void SaveBossLevel(int levelIndex)
    {
        SaveData data = LoadData();
        GetLevel(data, levelIndex).completed = true;

        WriteData(data);
        Debug.Log($"[SaveManager] Boss level {levelIndex} completed and saved.");
    }

    /// <summary>
    /// Loads and returns the full save data. If no save file exists (first run),
    /// returns a fresh SaveData with all levels at default values.
    /// </summary>
    public static SaveData LoadData()
    {
        if (!File.Exists(SavePath))
            return CreateFreshSaveData();

        try
        {
            string json = File.ReadAllText(SavePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // Guard against a corrupt or outdated file with too few entries
            if (data == null || data.levels == null || data.levels.Length < 10)
            {
                Debug.LogWarning("[SaveManager] Save file invalid or outdated. Starting fresh.");
                return CreateFreshSaveData();
            }

            // Ensure no individual entries are null (can happen if levels were added later)
            for (int i = 0; i < data.levels.Length; i++)
                data.levels[i] ??= new LevelSaveData();

            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to load save file: {e.Message}. Starting fresh.");
            return CreateFreshSaveData();
        }
    }

    /// <summary>
    /// Returns the save data for a single level by its 1-based index.
    /// Useful for the Load scene when populating individual level buttons.
    /// </summary>
    /// <param name="levelIndex">1-based level index.</param>
    public static LevelSaveData LoadLevel(int levelIndex)
    {
        return GetLevel(LoadData(), levelIndex);
    }

    /// <summary>
    /// Deletes the save file entirely, resetting all progress.
    /// Called from the main menu's fresh-start button.
    /// </summary>
    public static void ClearAllData()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("[SaveManager] Save data cleared.");
        }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Internal helpers

    /// <summary>
    /// Returns the LevelSaveData entry for a 1-based level index.
    /// </summary>
    private static LevelSaveData GetLevel(SaveData data, int levelIndex)
    {
        int i = Mathf.Clamp(levelIndex - 1, 0, data.levels.Length - 1);
        return data.levels[i];
    }

    /// <summary>
    /// Serializes and writes SaveData to disk.
    /// </summary>
    private static void WriteData(SaveData data)
    {
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to write save file: {e.Message}");
        }
    }

    /// <summary>
    /// Creates a SaveData with all 10 levels initialised to defaults.
    /// </summary>
    private static SaveData CreateFreshSaveData()
    {
        SaveData data = new SaveData();
        for (int i = 0; i < data.levels.Length; i++)
            data.levels[i] = new LevelSaveData();
        return data;
    }

    #endregion
}
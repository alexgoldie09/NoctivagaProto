using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry for mapping obstacle type IDs to their corresponding prefabs.
/// Allows dynamic spawning of obstacles from string keys stored in MapData.
/// </summary>
[CreateAssetMenu(menuName = "Grid/Obstacle Registry")]
public class ObstacleRegistry : ScriptableObject
{
    [Serializable]
    public class ObstacleEntry
    {
        public string id;             // Unique ID used in MapData
        public GameObject prefab;     // The actual prefab to instantiate
    }

    [SerializeField]
    private List<ObstacleEntry> obstacles = new List<ObstacleEntry>();

    private Dictionary<string, GameObject> lookupTable;

    /// <summary>
    /// Initializes the lookup dictionary if it hasn't been built yet.
    /// </summary>
    private void EnsureInitialized()
    {
        if (lookupTable != null)
            return;

        lookupTable = new Dictionary<string, GameObject>();

        foreach (var entry in obstacles)
        {
            if (!string.IsNullOrEmpty(entry.id) && entry.prefab != null)
            {
                if (!lookupTable.ContainsKey(entry.id))
                    lookupTable.Add(entry.id, entry.prefab);
                else
                    Debug.LogWarning($"Duplicate obstacle ID detected: {entry.id}");
            }
        }
    }

    /// <summary>
    /// Retrieves the prefab associated with the given ID.
    /// </summary>
    public GameObject GetPrefab(string id)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(id)) return null;

        lookupTable.TryGetValue(id, out GameObject prefab);
        return prefab;
    }
}

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Serializable dictionary for storing key-value pairs of string metadata.
/// Used by TileInfo.obstacleMetadata.
/// </summary>
[System.Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> keys = new();
    [SerializeField] private List<TValue> values = new();


    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (var pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }


    public void OnAfterDeserialize()
    {
        this.Clear();
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            this[keys[i]] = values[i];
        }
    }
}
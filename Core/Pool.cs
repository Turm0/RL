using System;
using System.Collections.Generic;

namespace RoguelikeEngine.Core;

/// <summary>
/// A weighted list of options where one gets picked randomly.
/// Used everywhere: skin colors, hair styles, equipment, etc.
/// </summary>
public class Pool<T>
{
    private readonly List<PoolEntry<T>> _entries = new();
    private float _totalWeight;

    public int Count => _entries.Count;
    public bool IsEmpty => _entries.Count == 0;

    public void Add(T item, float weight)
    {
        if (weight <= 0) return;
        _entries.Add(new PoolEntry<T> { Item = item, Weight = weight });
        _totalWeight += weight;
    }

    /// <summary>
    /// Picks a random item weighted by the entries' weights.
    /// Returns default(T) if pool is empty.
    /// </summary>
    public T Pick(Random rng)
    {
        if (_entries.Count == 0) return default;
        if (_entries.Count == 1) return _entries[0].Item;

        float roll = (float)rng.NextDouble() * _totalWeight;
        float cumulative = 0f;

        foreach (var entry in _entries)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative)
                return entry.Item;
        }

        return _entries[^1].Item;
    }

    /// <summary>
    /// Creates a pool with a single fixed value (always returns this value).
    /// </summary>
    public static Pool<T> Fixed(T value)
    {
        var pool = new Pool<T>();
        pool.Add(value, 1f);
        return pool;
    }
}

public struct PoolEntry<T>
{
    public T Item;
    public float Weight;
}

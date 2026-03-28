using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Caches Texture2D instances by string key to avoid repeated rasterization.
/// </summary>
public class TextureCache
{
    private readonly Dictionary<string, Texture2D> _cache = new();

    /// <summary>
    /// Returns a cached texture for the key, or creates and caches one using the factory.
    /// </summary>
    public Texture2D GetOrCreate(string key, Func<Texture2D> factory)
    {
        if (!_cache.TryGetValue(key, out var tex))
        {
            tex = factory();
            _cache[key] = tex;
        }
        return tex;
    }

    /// <summary>Removes a single cached entry.</summary>
    public void Invalidate(string key)
    {
        if (_cache.Remove(key, out var tex))
            tex.Dispose();
    }

    /// <summary>Disposes and removes all cached textures.</summary>
    public void Clear()
    {
        foreach (var tex in _cache.Values)
            tex.Dispose();
        _cache.Clear();
    }
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.Data;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Draws terrain effects as a screen-space overlay AFTER entities and lighting,
/// so effects like snow and wet visually cover everything on the tile (terrain, creatures, objects).
/// </summary>
public class EffectOverlayRenderer
{
    private readonly TextureCache _cache = new();
    private readonly FastNoiseLite _noise;
    private readonly FastNoiseLite _noiseDetail;

    public EffectOverlayRenderer()
    {
        _noise = new FastNoiseLite(9137);
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);

        _noiseDetail = new FastNoiseLite(2741);
        _noiseDetail.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    }

    public void Draw(SpriteBatch spriteBatch, TileMap map, Camera camera, FogOfWar fow, float totalSeconds)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = camera.GetVisibleTileRect(tileSize);

        int startX = visibleRect.X;
        int startY = visibleRect.Y;
        int endX = startX + visibleRect.Width;
        int endY = startY + visibleRect.Height;

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if (!map.IsInBounds(x, y)) continue;
                if (!fow.IsVisible(x, y)) continue;

                var effects = map.GetEffects(x, y);
                if (effects.Count == 0) continue;

                foreach (var effect in effects)
                {
                    if (effect.Intensity <= 0.01f) continue;

                    int qi = (int)(effect.Intensity * 10f + 0.5f);
                    long cacheKey = (long)(byte)effect.Type | ((long)qi << 8) | ((long)TileMap.ComputeVariantSeed(x, y) << 12);
                    var texture = _cache.GetOrCreate(cacheKey, () =>
                        GenerateOverlay(spriteBatch.GraphicsDevice, effect, x, y));

                    var worldPos = new Vector2(x * tileSize, y * tileSize);
                    var screenPos = camera.WorldToScreen(worldPos);
                    int sx = (int)Math.Floor(screenPos.X);
                    int sy = (int)Math.Floor(screenPos.Y);
                    int sx2 = (int)Math.Floor(screenPos.X + tileSize);
                    int sy2 = (int)Math.Floor(screenPos.Y + tileSize);
                    var destRect = new Rectangle(sx, sy, sx2 - sx, sy2 - sy);

                    spriteBatch.Draw(texture, destRect, Color.White);
                }
            }
        }
    }

    /// <summary>
    /// Invalidates cached overlay textures (call when effect intensities change).
    /// </summary>
    public void InvalidateAll() => _cache.Clear();

    private Texture2D GenerateOverlay(GraphicsDevice device, TileEffect effect, int worldX, int worldY)
    {
        int size = GameConfig.TileSize;
        var pixels = new Color[size * size];
        var def = EffectRegistry.Get(effect.Type);
        float intensity = effect.Intensity;
        int seed = TileMap.ComputeVariantSeed(worldX, worldY);

        switch (effect.Type)
        {
            case TerrainEffectType.Snow:
                GenerateSnowOverlay(pixels, size, intensity, worldX, worldY, seed);
                break;
            case TerrainEffectType.Wet:
                GenerateWetOverlay(pixels, size, intensity, worldX, worldY, seed);
                break;
            case TerrainEffectType.Dust:
                GenerateDustOverlay(pixels, size, intensity, worldX, worldY, seed);
                break;
            case TerrainEffectType.Scorched:
                GenerateScorchedOverlay(pixels, size, intensity, worldX, worldY, seed);
                break;
            case TerrainEffectType.Blood:
                GenerateBloodOverlay(pixels, size, intensity, worldX, worldY, seed);
                break;
            case TerrainEffectType.Moss:
                GenerateMossOverlay(pixels, size, intensity, worldX, worldY, seed);
                break;
            default:
                GenerateGenericOverlay(pixels, size, intensity, def.TintColor, worldX, worldY, seed);
                break;
        }

        var tex = new Texture2D(device, size, size);
        tex.SetData(pixels);
        return tex;
    }

    private void GenerateSnowOverlay(Color[] pixels, int size, float intensity, int wx, int wy, int seed)
    {
        // Snow: white particles and drifts, coverage scales with intensity
        // Low intensity: scattered white specks. High: nearly full white blanket
        var white = new Color(230, 235, 240);
        var lightGray = new Color(200, 210, 218);

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float wpx = wx * size + px;
                float wpy = wy * size + py;

                // Smooth, large-scale drift pattern
                float drift = (_noise.GetNoise(wpx * 0.12f, wpy * 0.12f) + 1f) * 0.5f;
                // Finer detail
                float detail = (_noiseDetail.GetNoise(wpx * 0.4f, wpy * 0.4f) + 1f) * 0.5f;

                float coverage = drift * 0.7f + detail * 0.3f;
                // Threshold based on intensity: higher intensity = lower threshold = more coverage
                float threshold = 1f - intensity;

                if (coverage > threshold)
                {
                    float alpha = Math.Min((coverage - threshold) / (1f - threshold + 0.01f), 1f) * intensity;
                    // Vary between white and light gray
                    var c = detail > 0.5f ? white : lightGray;
                    int a = (int)(alpha * 180); // not fully opaque so terrain shows slightly
                    pixels[py * size + px] = new Color(c.R, c.G, c.B, a);
                }
            }
        }
    }

    private void GenerateWetOverlay(Color[] pixels, int size, float intensity, int wx, int wy, int seed)
    {
        // Wet: subtle blue-tinted sheen with specular highlights
        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float wpx = wx * size + px;
                float wpy = wy * size + py;

                float n = (_noise.GetNoise(wpx * 0.2f, wpy * 0.2f) + 1f) * 0.5f;
                float coverage = n * intensity;

                if (coverage > 0.15f)
                {
                    // Specular highlight spots
                    float spec = _noiseDetail.GetNoise(wpx * 0.8f, wpy * 0.8f);
                    bool isHighlight = spec > 0.6f;

                    int r = isHighlight ? 120 : 30;
                    int g = isHighlight ? 140 : 50;
                    int b = isHighlight ? 160 : 80;
                    int a = (int)(coverage * 60);
                    pixels[py * size + px] = new Color(r, g, b, a);
                }
            }
        }
    }

    private void GenerateDustOverlay(Color[] pixels, int size, float intensity, int wx, int wy, int seed)
    {
        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float wpx = wx * size + px;
                float wpy = wy * size + py;

                int h = HashPixel((int)wpx, (int)wpy, seed);
                float chance = (h & 0xFF) / 255f;
                if (chance < intensity * 0.4f)
                {
                    int a = (int)(intensity * 50);
                    pixels[py * size + px] = new Color(140, 120, 80, a);
                }
            }
        }
    }

    private void GenerateScorchedOverlay(Color[] pixels, int size, float intensity, int wx, int wy, int seed)
    {
        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float wpx = wx * size + px;
                float wpy = wy * size + py;
                float n = (_noise.GetNoise(wpx * 0.3f, wpy * 0.3f) + 1f) * 0.5f;
                if (n > 1f - intensity)
                {
                    int a = (int)(intensity * 100);
                    pixels[py * size + px] = new Color(15, 12, 8, a);
                }
            }
        }
    }

    private void GenerateBloodOverlay(Color[] pixels, int size, float intensity, int wx, int wy, int seed)
    {
        var rng = new Random(seed);
        int splatCount = 1 + (int)(intensity * 4);
        for (int i = 0; i < splatCount; i++)
        {
            int cx = rng.Next(size);
            int cy = rng.Next(size);
            int r = 2 + rng.Next((int)(intensity * 6));
            for (int py = Math.Max(0, cy - r); py < Math.Min(size, cy + r); py++)
                for (int px = Math.Max(0, cx - r); px < Math.Min(size, cx + r); px++)
                {
                    float d = MathF.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    if (d < r)
                    {
                        int a = (int)((1f - d / r) * intensity * 140);
                        pixels[py * size + px] = new Color(100, 10, 5, a);
                    }
                }
        }
    }

    private void GenerateMossOverlay(Color[] pixels, int size, float intensity, int wx, int wy, int seed)
    {
        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float wpx = wx * size + px;
                float wpy = wy * size + py;
                float n = (_noise.GetNoise(wpx * 0.25f, wpy * 0.25f) + 1f) * 0.5f;
                // Moss concentrates at edges
                float edgeDist = Math.Min(Math.Min(px, size - 1 - px), Math.Min(py, size - 1 - py)) / (float)size;
                float coverage = n * (1f - edgeDist * 2f) * intensity;
                if (coverage > 0.3f)
                {
                    int a = (int)(coverage * 80);
                    pixels[py * size + px] = new Color(40, 70, 30, a);
                }
            }
        }
    }

    private void GenerateGenericOverlay(Color[] pixels, int size, float intensity, Color tint, int wx, int wy, int seed)
    {
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                int a = (int)(intensity * 60);
                pixels[py * size + px] = new Color(tint.R, tint.G, tint.B, a);
            }
    }

    private static int HashPixel(int x, int y, int seed)
    {
        int h = x * 374761393 + y * 668265263 + seed * 1274126177;
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }
}

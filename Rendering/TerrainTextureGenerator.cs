using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.Data;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

public class TerrainTextureGenerator
{
    private const int Size = GameConfig.TileSize; // 32

    public Texture2D Generate(GraphicsDevice device, TileData tile, NeighborContext neighbors,
        int worldX, int worldY, IReadOnlyList<TileEffect> effects)
    {
        var pixels = new Color[Size * Size];

        if (tile.HasWall)
            GenerateWall(pixels, tile, neighbors);
        else
            GenerateTerrain(pixels, tile);

        var texture = new Texture2D(device, Size, Size);
        texture.SetData(pixels);
        return texture;
    }

    private static void GenerateTerrain(Color[] pixels, TileData tile)
    {
        var def = TerrainRegistry.Get(tile.Terrain);
        int seed = tile.VariantSeed;

        for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
                pixels[py * Size + px] = SampleTerrain(def, seed, px, py);
    }

    private static void GenerateWall(Color[] pixels, TileData tile, NeighborContext neighbors)
    {
        var def = WallRegistry.Get(tile.Wall);
        int seed = tile.VariantSeed;

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                var color = SampleWall(def, seed, px, py);

                if (!neighbors.WallS && py >= Size - 4)
                {
                    float blend = (py - (Size - 4)) / 4f;
                    color = LerpColor(color, def.HighlightColor, blend * 0.7f);
                }

                pixels[py * Size + px] = color;
            }
        }
    }

    private static Color SampleTerrain(TerrainDefinition def, int seed, int px, int py)
    {
        int h = HashPixel(px, py, seed);
        float rand01 = (h & 0xFFF) / 4095f;

        float t = rand01 * def.NoiseAmplitude + (1f - def.NoiseAmplitude) * 0.5f;
        var color = LerpColor(def.VariantColorMin, def.VariantColorMax, t);

        color = ApplyPatternLocal(color, def.Pattern, seed, px, py);

        return color;
    }

    private static Color SampleWall(WallDefinition def, int seed, int px, int py)
    {
        int h = HashPixel(px, py, seed);
        float rand01 = (h & 0xFFF) / 4095f;

        float t = rand01 * def.NoiseAmplitude + (1f - def.NoiseAmplitude) * 0.5f;
        var color = LerpColor(def.VariantColorMin, def.VariantColorMax, t);

        color = ApplyPatternLocal(color, def.Pattern, seed, px, py);

        return color;
    }

    private static Color ApplyPatternLocal(Color baseColor, TerrainPattern pattern, int seed, int px, int py)
    {
        switch (pattern)
        {
            case TerrainPattern.Speckled:
            {
                int h = HashPixel(px, py, seed + 101);
                if ((h & 0x1F) == 0)
                {
                    float shift = ((h >> 5) & 0xFF) / 255f * 0.3f - 0.15f;
                    return ShiftBrightness(baseColor, shift);
                }
                float grain = ((HashPixel(px, py, seed + 202) & 0xFF) / 255f - 0.5f) * 0.06f;
                return ShiftBrightness(baseColor, grain);
            }

            case TerrainPattern.Organic:
            {
                int h1 = HashPixel(px / 4, py / 4, seed + 303);
                int h2 = HashPixel(px / 6, py / 6, seed + 404);
                float patch = ((h1 & 0xFF) / 255f - 0.5f) * 0.2f
                            + ((h2 & 0xFF) / 255f - 0.5f) * 0.12f;
                var result = ShiftBrightness(baseColor, patch);

                int h3 = HashPixel(px, py, seed + 505);
                if ((h3 & 0x3F) == 0)
                    result = ShiftBrightness(result, -0.18f);

                return result;
            }

            case TerrainPattern.Ripple:
            {
                float phase = (seed & 0xFF) * 0.1f;
                float wave = MathF.Sin(py * 0.5f + MathF.Sin(px * 0.3f + phase) * 1.5f + phase);
                return ShiftBrightness(baseColor, wave * 0.1f);
            }

            case TerrainPattern.Plank:
            {
                int offset = seed & 7;
                int plankY = (py + offset) % 8;
                if (plankY == 0)
                    return ShiftBrightness(baseColor, -0.14f);
                int h = HashPixel(px, py, seed + 606);
                float grain = ((h & 0xFF) / 255f - 0.5f) * 0.06f;
                return ShiftBrightness(baseColor, grain);
            }

            case TerrainPattern.Flat:
            default:
            {
                int h = HashPixel(px, py, seed + 707);
                float micro = ((h & 0xFF) / 255f - 0.5f) * 0.05f;
                return ShiftBrightness(baseColor, micro);
            }
        }
    }

    public Texture2D GenerateMemory(GraphicsDevice device, TileData tile, NeighborContext neighbors,
        int worldX, int worldY)
    {
        var pixels = new Color[Size * Size];

        if (tile.HasWall)
            GenerateWall(pixels, tile, neighbors);
        else
            GenerateTerrain(pixels, tile);

        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            float gray = c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
            gray *= 0.3f;
            pixels[i] = new Color((int)gray, (int)gray, (int)gray);
        }

        var texture = new Texture2D(device, Size, Size);
        texture.SetData(pixels);
        return texture;
    }

    public Texture2D GenerateAnimated(GraphicsDevice device, TileData tile, NeighborContext neighbors,
        int worldX, int worldY, IReadOnlyList<TileEffect> effects, int frame, int totalFrames)
    {
        var pixels = new Color[Size * Size];
        var def = TerrainRegistry.Get(tile.Terrain);
        int seed = tile.VariantSeed;

        float timeOffset = (float)frame / totalFrames * MathF.PI * 2f;

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                int h = HashPixel(px, py, seed);
                float rand01 = (h & 0xFFF) / 4095f;
                float t = rand01 * def.NoiseAmplitude + (1f - def.NoiseAmplitude) * 0.5f;
                var color = LerpColor(def.VariantColorMin, def.VariantColorMax, t);

                float phase = (seed & 0xFF) * 0.1f;
                float wave = MathF.Sin(py * 0.5f + MathF.Sin(px * 0.3f + phase) * 1.5f + timeOffset);
                float wave2 = MathF.Sin(px * 0.4f + py * 0.2f + phase * 0.7f + timeOffset * 1.3f);
                color = ShiftBrightness(color, (wave * 0.10f + wave2 * 0.06f));

                if (tile.Terrain == TerrainId.Water || tile.Terrain == TerrainId.DeepWater)
                {
                    float spec = MathF.Max(0, wave * wave2) * 0.15f;
                    color = ShiftBrightness(color, spec);
                }

                pixels[py * Size + px] = color;
            }
        }

        var texture = new Texture2D(device, Size, Size);
        texture.SetData(pixels);
        return texture;
    }

    // --- Helpers ---

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color ShiftBrightness(Color c, float shift)
    {
        return new Color(
            (byte)Math.Clamp(c.R + (int)(shift * 255), 0, 255),
            (byte)Math.Clamp(c.G + (int)(shift * 255), 0, 255),
            (byte)Math.Clamp(c.B + (int)(shift * 255), 0, 255));
    }

    private static int HashPixel(int x, int y, int seed)
    {
        int h = x * 374761393 + y * 668265263 + seed * 1274126177;
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    public static string BuildCacheKey(TileData tile, NeighborContext neighbors)
    {
        long neighborHash = neighbors.GetHash();
        return $"{(byte)tile.Terrain}_{(byte)tile.Wall}_{tile.VariantSeed}_{neighborHash}";
    }
}

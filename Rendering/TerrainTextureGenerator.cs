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

    public static bool IsLiquid(TerrainId terrain) =>
        terrain == TerrainId.Water || terrain == TerrainId.DeepWater || terrain == TerrainId.Lava;

    // Map reference for per-pixel water depth interpolation
    private TileMap _map;
    public void SetMap(TileMap map) => _map = map;

    public Texture2D Generate(GraphicsDevice device, TileData tile, NeighborContext neighbors,
        int worldX, int worldY, IReadOnlyList<TileEffect> effects, int animFrame = 0, int animTotalFrames = 4)
    {
        var pixels = new Color[Size * Size];

        if (tile.HasWall)
            GenerateWall(pixels, tile, neighbors);
        else if (IsLiquid(tile.Terrain))
            GenerateLiquid(pixels, tile, animFrame, animTotalFrames, worldX, worldY);
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

        PixelUtil.Pixelize(pixels, Size, def.PixelSize);
    }

    private static void GenerateWall(Color[] pixels, TileData tile, NeighborContext neighbors)
    {
        var def = WallRegistry.Get(tile.Wall);
        int seed = tile.VariantSeed;

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                const int faceDepth = 12; // front face (south)
                const int sideFaceDepth = 5; // side face (east/west)

                bool openS = !neighbors.WallS;
                bool openW = !neighbors.WallW;
                bool openE = !neighbors.WallE;

                int capLineS = Size - faceDepth;

                Color color;
                bool drawn = false;

                // SOUTH FACE — the front surface (highest priority)
                if (openS && py >= capLineS)
                {
                    int faceY = py - capLineS;
                    float faceT = (float)faceY / (faceDepth - 1);

                    color = SampleWallBlock(def, seed, px, py);
                    color = ShiftBrightness(color, -0.12f - faceT * 0.10f);

                    if (faceY <= 1)
                        color = LerpColor(color, def.HighlightColor, 0.6f - faceY * 0.3f);

                    drawn = true;
                }
                // WEST FACE — left side visible (room is to the left)
                else if (openW && px < sideFaceDepth)
                {
                    float faceT = 1f - (float)px / (sideFaceDepth - 1);

                    color = SampleWallBlock(def, seed, px, py);
                    color = ShiftBrightness(color, -0.06f - faceT * 0.06f);

                    if (px == 0)
                        color = LerpColor(color, def.HighlightColor, 0.3f);

                    drawn = true;
                }
                // EAST FACE — right side visible (room is to the right)
                else if (openE && px >= Size - sideFaceDepth)
                {
                    float faceT = (float)(px - (Size - sideFaceDepth)) / (sideFaceDepth - 1);

                    color = SampleWallBlock(def, seed, px, py);
                    color = ShiftBrightness(color, -0.06f - faceT * 0.06f);

                    if (px == Size - 1)
                        color = LerpColor(color, def.HighlightColor, 0.3f);

                    drawn = true;
                }
                else
                {
                    color = default;
                }

                if (!drawn)
                {
                    // WALL TOP — flat top surface
                    color = SampleWallBlock(def, seed, px, py);

                    if (!openS && !openW && !openE && neighbors.WallN)
                        color = ShiftBrightness(color, -0.06f);
                }

                pixels[py * Size + px] = color;
            }
        }

        PixelUtil.Pixelize(pixels, Size, def.PixelSize);
    }

    private static Color SampleWallBlock(WallDefinition def, int seed, int px, int py)
    {
        int blockW = def.BlockWidth;
        int blockH = def.BlockHeight;
        int mortar = def.MortarWidth;

        // Stagger every other row by half a block width
        int row = py / blockH;
        int staggerOffset = (def.Staggered && (row & 1) != 0) ? blockW / 2 : 0;
        int seedOffset = (seed & 0xF) * 2;

        int localX = (px + staggerOffset + seedOffset) % blockW;
        int localY = py % blockH;

        // Mortar lines between blocks
        if (localX < mortar || localY < mortar)
            return ShiftBrightness(def.BaseColor, -0.07f);

        // Each block gets its own shade
        int blockCol = (px + staggerOffset + seedOffset) / blockW;
        int blockSeed = HashPixel(blockCol, row, seed + 808);
        float blockShade = ((blockSeed & 0xFF) / 255f - 0.5f) * 0.12f;

        // Per-pixel variation
        int h = HashPixel(px, py, seed);
        float pixelVar = ((h & 0xFF) / 255f - 0.5f) * 0.04f;

        float t = ((h >> 8) & 0xFFF) / 4095f;
        var color = LerpColor(def.VariantColorMin, def.VariantColorMax,
            t * def.NoiseAmplitude + (1f - def.NoiseAmplitude) * 0.5f);

        color = ShiftBrightness(color, blockShade + pixelVar);

        // Top highlight on block face
        if (localY == mortar)
            color = ShiftBrightness(color, 0.06f);
        else if (localY == mortar + 1)
            color = ShiftBrightness(color, 0.03f);

        // Bottom shadow on block face
        if (localY == blockH - 1)
            color = ShiftBrightness(color, -0.05f);

        // Optional roughness (e.g. cave walls)
        if (def.Roughness > 0f)
        {
            int hRough = HashPixel(px / 2, py / 2, seed + 909);
            float rough = ((hRough & 0xFF) / 255f - 0.5f) * def.Roughness;
            color = ShiftBrightness(color, rough);
        }

        return color;
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

    private static Color ApplyPatternLocal(Color baseColor, TerrainPattern pattern, int seed, int px, int py)
    {
        switch (pattern)
        {
            case TerrainPattern.Speckled:
            {
                int h1 = HashPixel(px / 3, py / 3, seed + 101);
                int h2 = HashPixel(px, py, seed + 202);
                float broad = ((h1 & 0xFF) / 255f - 0.5f) * 0.08f;
                float fine = ((h2 & 0xFF) / 255f - 0.5f) * 0.04f;
                return ShiftBrightness(baseColor, broad + fine);
            }

            case TerrainPattern.Organic:
            {
                int h1 = HashPixel(px / 3, py / 3, seed + 303);
                int h2 = HashPixel(px / 5, py / 5, seed + 404);
                float patch = ((h1 & 0xFF) / 255f - 0.5f) * 0.10f
                            + ((h2 & 0xFF) / 255f - 0.5f) * 0.08f;
                return ShiftBrightness(baseColor, patch);
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
                int plankY = (py + offset) % 16;
                // Subtle gap between planks (1px, gentle darkening)
                if (plankY == 0)
                    return ShiftBrightness(baseColor, -0.06f);
                // Each plank gets its own shade
                int plankIdx = (py + offset) / 16;
                int plankSeed = HashPixel(0, plankIdx, seed + 606);
                float plankShade = ((plankSeed & 0xFF) / 255f - 0.5f) * 0.06f;
                // Subtle horizontal grain
                int h = HashPixel(px / 3, py, seed + 707);
                float grain = ((h & 0xFF) / 255f - 0.5f) * 0.03f;
                return ShiftBrightness(baseColor, plankShade + grain);
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
        var baseTex = Generate(device, tile, neighbors, worldX, worldY, null);
        var pixels = new Color[Size * Size];
        baseTex.GetData(pixels);
        baseTex.Dispose();

        ToGrayscale(pixels);

        var texture = new Texture2D(device, Size, Size);
        texture.SetData(pixels);
        return texture;
    }

    public static void ToGrayscale(Color[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            float gray = c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
            gray *= 0.3f;
            pixels[i] = new Color((int)gray, (int)gray, (int)gray, c.A);
        }
    }

    public Texture2D GenerateAnimated(GraphicsDevice device, TileData tile, NeighborContext neighbors,
        int worldX, int worldY, IReadOnlyList<TileEffect> effects, int frame, int totalFrames)
    {
        return Generate(device, tile, neighbors, worldX, worldY, effects, frame, totalFrames);
    }

    private void GenerateLiquid(Color[] pixels, TileData tile, int frame, int totalFrames,
        int worldX, int worldY)
    {
        if (tile.Terrain == TerrainId.Lava)
            GenerateLava(pixels, tile, frame, totalFrames);
        else
            GenerateWater(pixels, tile, frame, totalFrames, worldX, worldY);
    }

    private void GenerateWater(Color[] pixels, TileData tile, int frame, int totalFrames,
        int worldX, int worldY)
    {
        var def = TerrainRegistry.Get(tile.Terrain);
        var style = WaterStyleRegistry.Get(tile.WaterStyle);
        int seed = tile.VariantSeed;
        float frameT = (float)frame / totalFrames * MathF.PI * 2f;

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float depth = _map != null
                    ? _map.GetInterpolatedWaterDepth(worldX, worldY, px, py)
                    : tile.WaterDepth;

                var color = LerpColor(style.ShallowColor, style.DeepColor, depth);

                // Per-pixel shimmer: each pixel has a random brightness offset
                // and a phase that determines when it's active
                int h = HashPixel(px, py, seed + 100);
                int pixelPhase = h & 7;
                float pixelBright = ((h >> 3) & 0xFF) / 255f; // 0..1 how bright this pixel is

                if (pixelPhase == (frame % 8))
                    color = ShiftBrightness(color, pixelBright * 0.07f * (1f - depth * 0.5f));
                else
                    color = ShiftBrightness(color, pixelBright * 0.02f * (1f - depth * 0.5f));

                pixels[py * Size + px] = color;
            }
        }

        PixelUtil.Pixelize(pixels, Size, def.PixelSize);
    }

    private static void GenerateLava(Color[] pixels, TileData tile, int frame, int totalFrames)
    {
        var def = TerrainRegistry.Get(tile.Terrain);
        int seed = tile.VariantSeed;
        float frameT = (float)frame / totalFrames;

        int cellCount = 20;
        var cellX = new float[cellCount];
        var cellY = new float[cellCount];
        var cellShade = new float[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            int ch = HashPixel(i * 17, i * 31, seed + 900 + i);
            float baseX = (ch & 0xFF) / 255f * Size;
            float baseY = ((ch >> 8) & 0xFF) / 255f * Size;
            float drift = frameT * MathF.PI * 2f;
            cellX[i] = baseX + MathF.Sin(drift + i * 2.1f) * 1f;
            cellY[i] = baseY + MathF.Cos(drift + i * 1.7f) * 1f;
            cellShade[i] = ((ch >> 16) & 0xFF) / 255f;
        }

        var highlightColor = new Color(240, 140, 30);

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float minDist = float.MaxValue;
                float minDist2 = float.MaxValue;
                float nearestShade = 0f;

                for (int i = 0; i < cellCount; i++)
                {
                    float dx = px - cellX[i];
                    float dy = py - cellY[i];
                    float dist = dx * dx + dy * dy;
                    if (dist < minDist)
                    {
                        minDist2 = minDist;
                        minDist = dist;
                        nearestShade = cellShade[i];
                    }
                    else if (dist < minDist2)
                    {
                        minDist2 = dist;
                    }
                }

                minDist = MathF.Sqrt(minDist);
                minDist2 = MathF.Sqrt(minDist2);

                float edgeFactor = minDist / (minDist + minDist2 + 0.01f);

                float t = nearestShade * 0.4f + 0.3f;
                var color = LerpColor(def.VariantColorMin, def.VariantColorMax, t);

                float edgeDarken = edgeFactor > 0.40f ? (edgeFactor - 0.40f) * 2f : 0f;
                color = ShiftBrightness(color, -edgeDarken * 0.18f);

                float centerBright = Math.Max(0f, 0.33f - edgeFactor) * 0.5f;
                color = LerpColor(color, highlightColor, centerBright);

                pixels[py * Size + px] = color;
            }
        }

        PixelUtil.Pixelize(pixels, Size, def.PixelSize);
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

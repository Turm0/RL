using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;

namespace RoguelikeEngine.Rendering;

public class TerrainObjectGenerator
{
    private const int Size = GameConfig.TileSize;
    private readonly FastNoiseLite _noise;

    public TerrainObjectGenerator()
    {
        _noise = new FastNoiseLite(3571);
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    }

    public Texture2D Generate(GraphicsDevice device, string objectType, int variantSeed)
    {
        return objectType switch
        {
            "tree_oak" => GenerateTree(device, variantSeed, new Color(35, 55, 25), new Color(50, 75, 35), new Color(65, 45, 25)),
            "tree_pine" => GeneratePineTree(device, variantSeed, new Color(25, 50, 30), new Color(40, 65, 38)),
            "tree_dead" => GenerateDeadTree(device, variantSeed),
            "well" => GenerateWell(device, variantSeed),
            "table" => GenerateTable(device, variantSeed),
            "chair" => GenerateChair(device, variantSeed),
            "barrel" => GenerateBarrel(device, variantSeed),
            "crate" => GenerateCrate(device, variantSeed),
            "rock" => GenerateRock(device, variantSeed),
            "bush" => GenerateBush(device, variantSeed),
            "flower" => GenerateFlower(device, variantSeed),
            "mushroom" => GenerateMushroom(device, variantSeed),
            _ => GenerateDefault(device, variantSeed)
        };
    }

    private Texture2D GenerateTree(GraphicsDevice device, int seed, Color canopyDark, Color canopyLight, Color trunk)
    {
        var pixels = new Color[Size * Size];
        var rng = new Random(seed);

        // Trunk: 3px wide, bottom half
        int trunkX = Size / 2 - 1;
        for (int y = Size / 2; y < Size - 2; y++)
            for (int x = trunkX; x < trunkX + 3; x++)
            {
                float shift = _noise.GetNoise(x * 5f, y * 5f) * 0.1f;
                pixels[y * Size + x] = ShiftBrightness(trunk, shift);
            }

        // Canopy: blob in upper portion
        int cx = Size / 2, cy = Size / 3;
        int radius = 9 + (seed & 3);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float noiseR = _noise.GetNoise(x * 3f + seed, y * 3f) * 3f;
                if (dist < radius + noiseR)
                {
                    float t = dist / (radius + noiseR);
                    var c = LerpColor(canopyLight, canopyDark, t);
                    // Add noise variation
                    float n = _noise.GetNoise(x * 6f + seed, y * 6f) * 0.15f;
                    pixels[y * Size + x] = ShiftBrightness(c, n);
                }
            }

        // Outline
        AddOutline(pixels, new Color(15, 20, 10));

        return CreateTexture(device, pixels);
    }

    private Texture2D GeneratePineTree(GraphicsDevice device, int seed, Color dark, Color light)
    {
        var pixels = new Color[Size * Size];
        var trunk = new Color(60, 40, 22);

        // Trunk
        int trunkX = Size / 2 - 1;
        for (int y = Size - 8; y < Size - 1; y++)
            for (int x = trunkX; x < trunkX + 3; x++)
                pixels[y * Size + x] = trunk;

        // Triangular canopy layers
        int tipY = 2;
        int baseY = Size - 8;
        for (int y = tipY; y < baseY; y++)
        {
            float t = (float)(y - tipY) / (baseY - tipY);
            int halfWidth = (int)(t * 10) + 1;
            for (int x = Size / 2 - halfWidth; x <= Size / 2 + halfWidth; x++)
            {
                if (x < 0 || x >= Size) continue;
                float n = _noise.GetNoise(x * 4f + seed, y * 4f) * 0.2f;
                var c = LerpColor(light, dark, t + n);
                pixels[y * Size + x] = c;
            }
        }

        AddOutline(pixels, new Color(10, 18, 8));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateDeadTree(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var bark = new Color(55, 42, 30);

        // Main trunk, slightly crooked
        float lean = ((seed & 7) - 3) * 0.3f;
        for (int y = 5; y < Size - 2; y++)
        {
            int cx = Size / 2 + (int)(lean * (Size - 2 - y) / Size);
            for (int x = cx - 1; x <= cx + 1; x++)
                if (x >= 0 && x < Size)
                    pixels[y * Size + x] = ShiftBrightness(bark, _noise.GetNoise(x * 3f + seed, y * 3f) * 0.1f);
        }

        // A few stubby branches
        var rng = new Random(seed);
        for (int i = 0; i < 3; i++)
        {
            int by = 8 + rng.Next(10);
            int dir = rng.Next(2) == 0 ? -1 : 1;
            int bx = Size / 2 + (int)(lean * (Size - 2 - by) / Size);
            for (int j = 0; j < 4 + rng.Next(3); j++)
            {
                int px = bx + dir * j;
                int py = by - j / 2;
                if (px >= 0 && px < Size && py >= 0 && py < Size)
                    pixels[py * Size + px] = bark;
            }
        }

        AddOutline(pixels, new Color(25, 20, 15));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateWell(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var stone = new Color(90, 85, 78);
        var water = new Color(25, 40, 60);

        // Stone ring (ellipse)
        int cx = Size / 2, cy = Size / 2 + 2;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - cx) / 8f;
                float dy = (y - cy) / 6f;
                float d = dx * dx + dy * dy;
                if (d < 1f && d > 0.45f)
                    pixels[y * Size + x] = ShiftBrightness(stone, _noise.GetNoise(x * 4f + seed, y * 4f) * 0.1f);
                else if (d <= 0.45f)
                    pixels[y * Size + x] = ShiftBrightness(water, _noise.GetNoise(x * 2f, y * 2f) * 0.08f);
            }

        AddOutline(pixels, new Color(30, 28, 25));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateTable(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var wood = new Color(82, 58, 35);

        // Tabletop
        for (int y = Size / 3; y < Size / 3 + 6; y++)
            for (int x = 4; x < Size - 4; x++)
            {
                float n = _noise.GetNoise(x * 4f + seed, y * 0.5f) * 0.08f;
                pixels[y * Size + x] = ShiftBrightness(wood, n);
            }

        // Legs
        var leg = ShiftBrightness(wood, -0.1f);
        for (int y = Size / 3 + 6; y < Size - 3; y++)
        {
            pixels[y * Size + 6] = leg;
            pixels[y * Size + Size - 7] = leg;
        }

        AddOutline(pixels, new Color(25, 18, 10));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateChair(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var wood = new Color(75, 52, 32);

        // Seat
        for (int y = Size / 2; y < Size / 2 + 4; y++)
            for (int x = 8; x < Size - 8; x++)
                pixels[y * Size + x] = wood;

        // Back
        for (int y = Size / 4; y < Size / 2; y++)
            for (int x = 8; x < 12; x++)
                pixels[y * Size + x] = ShiftBrightness(wood, -0.05f);

        // Legs
        var leg = ShiftBrightness(wood, -0.12f);
        for (int y = Size / 2 + 4; y < Size - 4; y++)
        {
            pixels[y * Size + 9] = leg;
            pixels[y * Size + Size - 9] = leg;
        }

        AddOutline(pixels, new Color(22, 16, 8));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateBarrel(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var wood = new Color(90, 62, 35);
        var band = new Color(60, 55, 50);

        int cx = Size / 2, cy = Size / 2;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - cx) / 9f;
                float dy = (y - cy) / 11f;
                if (dx * dx + dy * dy < 1f)
                {
                    // Metal bands at top and bottom
                    bool isBand = Math.Abs(y - cy) > 8;
                    var c = isBand ? band : wood;
                    float n = _noise.GetNoise(x * 3f + seed, y * 1f) * 0.08f;
                    pixels[y * Size + x] = ShiftBrightness(c, n);
                }
            }

        AddOutline(pixels, new Color(25, 18, 10));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateCrate(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var wood = new Color(95, 70, 42);

        for (int y = 4; y < Size - 4; y++)
            for (int x = 4; x < Size - 4; x++)
            {
                float n = _noise.GetNoise(x * 3f + seed, y * 0.5f) * 0.08f;
                // Cross planks
                bool isCross = Math.Abs(x - Size / 2) + Math.Abs(y - Size / 2) < 3;
                var c = isCross ? ShiftBrightness(wood, -0.1f) : wood;
                pixels[y * Size + x] = ShiftBrightness(c, n);
            }

        AddOutline(pixels, new Color(30, 22, 12));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateRock(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var stone = new Color(80, 75, 68);

        int cx = Size / 2, cy = Size / 2 + 3;
        int rx = 8 + (seed & 3), ry = 6 + ((seed >> 2) & 3);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (float)(x - cx) / rx;
                float dy = (float)(y - cy) / ry;
                float noiseR = _noise.GetNoise(x * 3f + seed, y * 3f) * 0.3f;
                if (dx * dx + dy * dy < 1f + noiseR)
                {
                    float n = _noise.GetNoise(x * 5f + seed, y * 5f) * 0.12f;
                    pixels[y * Size + x] = ShiftBrightness(stone, n);
                }
            }

        AddOutline(pixels, new Color(25, 23, 20));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateBush(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var green = new Color(40, 60, 30);

        int cx = Size / 2, cy = Size / 2 + 2;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float noiseR = _noise.GetNoise(x * 4f + seed, y * 4f) * 3f;
                if (dist < 10 + noiseR)
                {
                    float n = _noise.GetNoise(x * 6f + seed, y * 6f) * 0.18f;
                    pixels[y * Size + x] = ShiftBrightness(green, n);
                }
            }

        AddOutline(pixels, new Color(15, 22, 10));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateFlower(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var stem = new Color(35, 55, 25);
        var rng = new Random(seed);

        // Multiple small flowers
        for (int i = 0; i < 3; i++)
        {
            int fx = 8 + rng.Next(16);
            int fy = 8 + rng.Next(16);
            // Stem
            for (int y = fy; y < Math.Min(fy + 6, Size); y++)
                if (fx >= 0 && fx < Size && y >= 0 && y < Size)
                    pixels[y * Size + fx] = stem;
            // Petals
            var petalColor = new Color(
                150 + rng.Next(100),
                40 + rng.Next(80),
                40 + rng.Next(80));
            for (int dy = -2; dy <= 0; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = fx + dx, py = fy + dy;
                    if (px >= 0 && px < Size && py >= 0 && py < Size)
                        pixels[py * Size + px] = petalColor;
                }
        }

        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateMushroom(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        var rng = new Random(seed);
        var cap = new Color(140 + rng.Next(60), 40 + rng.Next(40), 30);
        var stalk = new Color(200, 190, 170);

        int cx = Size / 2, baseY = Size / 2 + 4;

        // Stalk
        for (int y = baseY; y < baseY + 8 && y < Size; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
                if (x >= 0 && x < Size && y < Size)
                    pixels[y * Size + x] = stalk;

        // Cap (dome)
        for (int y = baseY - 5; y < baseY + 1; y++)
            for (int x = cx - 5; x <= cx + 5; x++)
            {
                if (x < 0 || x >= Size || y < 0 || y >= Size) continue;
                float dx = (x - cx) / 5f;
                float dy = (y - (baseY - 2)) / 3.5f;
                if (dx * dx + dy * dy < 1f)
                {
                    float brightness = -dy * 0.15f;
                    pixels[y * Size + x] = ShiftBrightness(cap, brightness);
                }
            }

        AddOutline(pixels, new Color(20, 15, 10));
        return CreateTexture(device, pixels);
    }

    private Texture2D GenerateDefault(GraphicsDevice device, int seed)
    {
        var pixels = new Color[Size * Size];
        pixels[Size / 2 * Size + Size / 2] = Color.Magenta;
        return CreateTexture(device, pixels);
    }

    // --- Helpers ---

    private static void AddOutline(Color[] pixels, Color outlineColor)
    {
        var outline = new bool[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (pixels[y * Size + x].A == 0) continue;
                // Check 4 cardinal neighbors
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (nx < 0 || nx >= Size || ny < 0 || ny >= Size || pixels[ny * Size + nx].A == 0)
                        outline[y * Size + x] = true;
                }
            }

        // Only draw outline on transparent pixels adjacent to filled ones
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (pixels[y * Size + x].A != 0) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (nx >= 0 && nx < Size && ny >= 0 && ny < Size && pixels[ny * Size + nx].A != 0)
                    {
                        pixels[y * Size + x] = outlineColor;
                        break;
                    }
                }
            }
    }

    private static Texture2D CreateTexture(GraphicsDevice device, Color[] pixels)
    {
        var tex = new Texture2D(device, Size, Size);
        tex.SetData(pixels);
        return tex;
    }

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
}

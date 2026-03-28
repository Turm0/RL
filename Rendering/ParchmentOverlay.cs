using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.Data;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Generates a parchment paper texture for rendering explored-but-not-visible (memory) tiles.
/// Uses layered FastNoiseLite for paper grain, staining, and fiber effects.
/// Walls are drawn as dark ink strokes on the parchment.
/// </summary>
public class ParchmentOverlay
{
    private Texture2D _texture;

    /// <summary>The generated parchment texture covering the full map.</summary>
    public Texture2D Texture => _texture;

    /// <summary>
    /// Generates the parchment texture for the given map. Call once at initialization.
    /// </summary>
    public void Generate(TileMap map, GraphicsDevice device)
    {
        int tileSize = GameConfig.TileSize;
        int texW = map.Width * tileSize;
        int texH = map.Height * tileSize;

        var pixels = new Color[texW * texH];

        // Noise layers
        var noiseLarge = new FastNoiseLite(42);
        noiseLarge.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noiseLarge.SetFrequency(0.005f);

        var noiseMedium = new FastNoiseLite(137);
        noiseMedium.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noiseMedium.SetFrequency(0.02f);

        var noiseFine = new FastNoiseLite(293);
        noiseFine.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noiseFine.SetFrequency(0.08f);

        // Base parchment color
        const float baseR = 210f;
        const float baseG = 190f;
        const float baseB = 150f;

        // Wall ink color
        const float wallR = 90f;
        const float wallG = 70f;
        const float wallB = 50f;

        // Water ink color — slightly blue-tinted ink
        const float waterR = 110f;
        const float waterG = 130f;
        const float waterB = 140f;

        // Lava ink color — reddish-brown ink
        const float lavaR = 130f;
        const float lavaG = 75f;
        const float lavaB = 55f;

        for (int py = 0; py < texH; py++)
        {
            for (int px = 0; px < texW; px++)
            {
                int tileX = px / tileSize;
                int tileY = py / tileSize;

                // Noise contributions
                float large = noiseLarge.GetNoise(px, py);   // -1 to 1
                float medium = noiseMedium.GetNoise(px, py);
                float fine = noiseFine.GetNoise(px, py);

                // Combine: base brightness modulated by noise layers
                float brightness = 1f + large * 0.20f + medium * 0.08f + fine * 0.04f;

                float r, g, b;

                if (map.IsInBounds(tileX, tileY))
                {
                    var tile = map.GetTile(tileX, tileY);
                    switch (tile)
                    {
                        case TileType.Wall:
                            // Dark ink strokes for walls
                            r = wallR * brightness;
                            g = wallG * brightness;
                            b = wallB * brightness;
                            // Darken edges near floor tiles for ink bleed effect
                            r = ApplyWallEdgeDarkening(px, py, tileX, tileY, tileSize, map, r);
                            g = ApplyWallEdgeDarkening(px, py, tileX, tileY, tileSize, map, g);
                            b = ApplyWallEdgeDarkening(px, py, tileX, tileY, tileSize, map, b);
                            break;

                        case TileType.Water:
                            r = waterR * brightness;
                            g = waterG * brightness;
                            b = waterB * brightness;
                            break;

                        case TileType.Lava:
                            r = lavaR * brightness;
                            g = lavaG * brightness;
                            b = lavaB * brightness;
                            break;

                        default: // Floor
                            r = baseR * brightness;
                            g = baseG * brightness;
                            b = baseB * brightness;
                            break;
                    }
                }
                else
                {
                    r = baseR * brightness * 0.5f;
                    g = baseG * brightness * 0.5f;
                    b = baseB * brightness * 0.5f;
                }

                pixels[py * texW + px] = new Color(
                    (byte)MathHelper.Clamp(r, 0, 255),
                    (byte)MathHelper.Clamp(g, 0, 255),
                    (byte)MathHelper.Clamp(b, 0, 255));
            }
        }

        _texture?.Dispose();
        _texture = new Texture2D(device, texW, texH);
        _texture.SetData(pixels);
    }

    /// <summary>
    /// Darkens wall pixels that are near floor tile boundaries, simulating ink bleed.
    /// </summary>
    private static float ApplyWallEdgeDarkening(int px, int py, int tileX, int tileY,
        int tileSize, TileMap map, float value)
    {
        // Distance from pixel to each tile edge in pixels
        int localX = px - tileX * tileSize;
        int localY = py - tileY * tileSize;

        float minDistToFloor = tileSize; // start large

        // Check each neighboring tile — if it's a floor, compute distance
        if (IsFloorLike(map, tileX - 1, tileY))
            minDistToFloor = MathF.Min(minDistToFloor, localX);
        if (IsFloorLike(map, tileX + 1, tileY))
            minDistToFloor = MathF.Min(minDistToFloor, tileSize - 1 - localX);
        if (IsFloorLike(map, tileX, tileY - 1))
            minDistToFloor = MathF.Min(minDistToFloor, localY);
        if (IsFloorLike(map, tileX, tileY + 1))
            minDistToFloor = MathF.Min(minDistToFloor, tileSize - 1 - localY);

        if (minDistToFloor < 6)
        {
            float darken = 1f - (1f - minDistToFloor / 6f) * 0.3f;
            return value * darken;
        }
        return value;
    }

    private static bool IsFloorLike(TileMap map, int x, int y)
    {
        if (!map.IsInBounds(x, y)) return false;
        var t = map.GetTile(x, y);
        return t == TileType.Floor || t == TileType.Water;
    }
}

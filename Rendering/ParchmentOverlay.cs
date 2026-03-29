using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.Data;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

public class ParchmentOverlay
{
    private Texture2D _texture;

    public Texture2D Texture => _texture;

    public void Generate(TileMap map, GraphicsDevice device)
    {
        int tileSize = GameConfig.TileSize;
        int texW = map.Width * tileSize;
        int texH = map.Height * tileSize;

        var pixels = new Color[texW * texH];

        var noiseLarge = new FastNoiseLite(42);
        noiseLarge.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noiseLarge.SetFrequency(0.005f);

        var noiseMedium = new FastNoiseLite(137);
        noiseMedium.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noiseMedium.SetFrequency(0.02f);

        var noiseFine = new FastNoiseLite(293);
        noiseFine.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noiseFine.SetFrequency(0.08f);

        const float baseR = 210f, baseG = 190f, baseB = 150f;
        const float wallR = 90f, wallG = 70f, wallB = 50f;
        const float waterR = 110f, waterG = 130f, waterB = 140f;
        const float lavaR = 130f, lavaG = 75f, lavaB = 55f;
        const float grassR = 100f, grassG = 120f, grassB = 80f;
        const float sandR = 150f, sandG = 140f, sandB = 100f;

        for (int py = 0; py < texH; py++)
        {
            for (int px = 0; px < texW; px++)
            {
                int tileX = px / tileSize;
                int tileY = py / tileSize;

                float large = noiseLarge.GetNoise(px, py);
                float medium = noiseMedium.GetNoise(px, py);
                float fine = noiseFine.GetNoise(px, py);
                float brightness = 1f + large * 0.20f + medium * 0.08f + fine * 0.04f;

                float r, g, b;

                if (map.IsInBounds(tileX, tileY))
                {
                    var tile = map.GetTile(tileX, tileY);

                    if (tile.HasWall)
                    {
                        r = wallR * brightness;
                        g = wallG * brightness;
                        b = wallB * brightness;
                        r = ApplyWallEdgeDarkening(px, py, tileX, tileY, tileSize, map, r);
                        g = ApplyWallEdgeDarkening(px, py, tileX, tileY, tileSize, map, g);
                        b = ApplyWallEdgeDarkening(px, py, tileX, tileY, tileSize, map, b);
                    }
                    else
                    {
                        switch (tile.Terrain)
                        {
                            case TerrainId.Water:
                            case TerrainId.DeepWater:
                                r = waterR * brightness;
                                g = waterG * brightness;
                                b = waterB * brightness;
                                break;
                            case TerrainId.Lava:
                                r = lavaR * brightness;
                                g = lavaG * brightness;
                                b = lavaB * brightness;
                                break;
                            case TerrainId.Grass:
                                r = grassR * brightness;
                                g = grassG * brightness;
                                b = grassB * brightness;
                                break;
                            case TerrainId.Sand:
                                r = sandR * brightness;
                                g = sandG * brightness;
                                b = sandB * brightness;
                                break;
                            default:
                                r = baseR * brightness;
                                g = baseG * brightness;
                                b = baseB * brightness;
                                break;
                        }
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

    private static float ApplyWallEdgeDarkening(int px, int py, int tileX, int tileY,
        int tileSize, TileMap map, float value)
    {
        int localX = px - tileX * tileSize;
        int localY = py - tileY * tileSize;

        float minDistToFloor = tileSize;

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
        var tile = map.GetTile(x, y);
        return !tile.HasWall;
    }
}

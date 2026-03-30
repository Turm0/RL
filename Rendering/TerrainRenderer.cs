using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.Data;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

public class TerrainRenderer
{
    private readonly TerrainTextureGenerator _generator;
    private readonly TextureCache _terrainCache = new();
    private readonly TextureCache _memoryCache = new();

    public TerrainRenderer(TileMap map)
    {
        _generator = new TerrainTextureGenerator();
        _generator.SetMap(map);
    }

    // Animated tile frame caches: key → Texture2D[] (8 frames)
    private readonly Dictionary<long, Texture2D[]> _animCache = new();
    private const int AnimFrameCount = 8;
    private const float AnimFps = 1.5f;

    public void Draw(SpriteBatch spriteBatch, TileMap map, Camera camera, FogOfWar fow, float totalSeconds,
        Vector3 ambientColor = default)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = camera.GetVisibleTileRect(tileSize);

        // Memory tiles: ambient-scaled with a subtle cool tint
        // Grayscale itself is the main distinction; tint just adds slight desaturation
        var memoryTint = new Color(
            ambientColor.X * 0.88f,
            ambientColor.Y * 0.93f,
            ambientColor.Z * 0.97f);

        int startX = visibleRect.X;
        int startY = visibleRect.Y;
        int endX = startX + visibleRect.Width;
        int endY = startY + visibleRect.Height;

        int animFrame = (int)(totalSeconds * AnimFps) % AnimFrameCount;

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if (!map.IsInBounds(x, y)) continue;

                bool visible = fow.IsVisible(x, y);
                bool explored = fow.IsExplored(x, y);

                if (!explored) continue;

                var tile = map.GetTile(x, y);
                var neighbors = NeighborContext.FromMap(map, x, y);
                long cacheKey = TerrainTextureGenerator.BuildCacheKey(tile, neighbors);

                Texture2D texture;

                bool isAnimated = visible && (TerrainTextureGenerator.IsLiquid(tile.Terrain)
                    || HasLiquidNeighbor(neighbors));

                if (isAnimated)
                {
                    texture = GetAnimatedFrame(spriteBatch.GraphicsDevice, cacheKey, tile, neighbors, x, y, animFrame);
                }
                else if (visible)
                {
                    texture = _terrainCache.GetOrCreate(cacheKey, () =>
                        _generator.Generate(spriteBatch.GraphicsDevice, tile, neighbors, x, y, null));
                }
                else
                {
                    // Use a distinct key space for memory textures by flipping the sign bit
                    long memKey = cacheKey | unchecked((long)0x8000000000000000);
                    texture = _memoryCache.GetOrCreate(memKey, () =>
                        _generator.GenerateMemory(spriteBatch.GraphicsDevice, tile, neighbors, x, y));
                }

                var destRect = camera.TileToScreenRect(x, y, tileSize);

                var tint = visible ? Color.White : memoryTint;
                spriteBatch.Draw(texture, destRect, tint);
            }
        }
    }

    private Texture2D GetAnimatedFrame(GraphicsDevice device, long baseKey, TileData tile,
        NeighborContext neighbors, int worldX, int worldY, int frame)
    {
        if (!_animCache.TryGetValue(baseKey, out var frames))
        {
            frames = new Texture2D[AnimFrameCount];
            _animCache[baseKey] = frames;
        }
        if (frames[frame] == null)
            frames[frame] = _generator.GenerateAnimated(device, tile, neighbors, worldX, worldY, null, frame, AnimFrameCount);
        return frames[frame];
    }

    private static bool HasLiquidNeighbor(NeighborContext n)
    {
        return TerrainTextureGenerator.IsLiquid(n.N) || TerrainTextureGenerator.IsLiquid(n.S) ||
               TerrainTextureGenerator.IsLiquid(n.E) || TerrainTextureGenerator.IsLiquid(n.W) ||
               TerrainTextureGenerator.IsLiquid(n.NE) || TerrainTextureGenerator.IsLiquid(n.NW) ||
               TerrainTextureGenerator.IsLiquid(n.SE) || TerrainTextureGenerator.IsLiquid(n.SW);
    }
}

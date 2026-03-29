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

    // Animated tile frame caches: key → Texture2D[] (4 frames)
    private readonly Dictionary<string, Texture2D[]> _animCache = new();
    private const int AnimFrameCount = 8;
    private const float AnimFps = 1.5f;

    public void Draw(SpriteBatch spriteBatch, TileMap map, Camera camera, FogOfWar fow, float totalSeconds)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = camera.GetVisibleTileRect(tileSize);

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
                string cacheKey = TerrainTextureGenerator.BuildCacheKey(tile, neighbors);

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
                    string memKey = "mem_" + cacheKey;
                    texture = _memoryCache.GetOrCreate(memKey, () =>
                        _generator.GenerateMemory(spriteBatch.GraphicsDevice, tile, neighbors, x, y));
                }

                var destRect = camera.TileToScreenRect(x, y, tileSize);

                spriteBatch.Draw(texture, destRect, Color.White);
            }
        }
    }

    private Texture2D GetAnimatedFrame(GraphicsDevice device, string baseKey, TileData tile,
        NeighborContext neighbors, int worldX, int worldY, int frame)
    {
        if (!_animCache.TryGetValue(baseKey, out var frames))
        {
            frames = new Texture2D[AnimFrameCount];
            for (int f = 0; f < AnimFrameCount; f++)
                frames[f] = _generator.GenerateAnimated(device, tile, neighbors, worldX, worldY, null, f, AnimFrameCount);
            _animCache[baseKey] = frames;
        }
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

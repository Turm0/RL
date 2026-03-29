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
    private readonly TerrainTextureGenerator _generator = new();
    private readonly TextureCache _terrainCache = new();
    private readonly TextureCache _memoryCache = new();

    // Animated tile frame caches: key → Texture2D[] (4 frames)
    private readonly Dictionary<string, Texture2D[]> _animCache = new();
    private const int AnimFrameCount = 4;
    private const float AnimFps = 2f;

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

                bool isAnimated = visible && !tile.HasWall &&
                    (tile.Terrain == TerrainId.Water || tile.Terrain == TerrainId.DeepWater || tile.Terrain == TerrainId.Lava);

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

}

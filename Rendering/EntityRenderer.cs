using System;
using System.Collections.Generic;
using System.IO;
using DefaultEcs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.World;
using BodyGenerator.Pipeline;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Draws entities as cached textures, y-sorted.
/// Supports both VectorRasterizer (legacy) and BodyGenerator sprites.
/// </summary>
public class EntityRenderer
{
    private readonly VectorRasterizer _rasterizer;
    private readonly TextureCache _cache;
    private readonly SpriteGenerator _spriteGenerator;

    public EntityRenderer(VectorRasterizer rasterizer, TextureCache cache)
    {
        _rasterizer = rasterizer;
        _cache = cache;

        string contentRoot = Path.Combine(AppContext.BaseDirectory, "Content", "Sprites");
        _spriteGenerator = new SpriteGenerator(contentRoot);
    }

    /// <summary>
    /// Draws all entities with Position + SpriteShape, sorted by Y then X.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera camera, DefaultEcs.World world, int tileSize, FogOfWar fow)
    {
        using var entitySet = world.GetEntities()
            .With<Position>()
            .With<SpriteShape>()
            .AsSet();

        // Collect entities that are in viewport AND currently visible in FOV
        var visible = new List<Entity>();
        foreach (ref readonly var entity in entitySet.GetEntities())
        {
            ref readonly var pos = ref entity.Get<Position>();
            if (camera.IsInView(pos.TileX, pos.TileY, tileSize) && fow.IsVisible(pos.TileX, pos.TileY))
                visible.Add(entity);
        }

        // Y-sort (then X for ties)
        visible.Sort((a, b) =>
        {
            ref readonly var pa = ref a.Get<Position>();
            ref readonly var pb = ref b.Get<Position>();
            int cmp = pa.TileY.CompareTo(pb.TileY);
            return cmp != 0 ? cmp : pa.TileX.CompareTo(pb.TileX);
        });

        // Draw each entity
        foreach (var entity in visible)
        {
            ref readonly var pos = ref entity.Get<Position>();
            ref readonly var shape = ref entity.Get<SpriteShape>();

            string creatureType = shape.CreatureType;
            float size = shape.Size;
            string cacheKey = $"{creatureType}_{size}";

            var texture = _cache.GetOrCreate(cacheKey, () =>
            {
                if (creatureType.EndsWith(".yaml"))
                    return _spriteGenerator.Generate(spriteBatch.GraphicsDevice, creatureType);
                return _rasterizer.RasterizeCreature(creatureType, size, tileSize, spriteBatch.GraphicsDevice);
            });

            Rectangle destRect;

            if (creatureType.EndsWith(".yaml"))
            {
                // BodyGenerator: content is centered in 32x32, draw 1:1 on tile
                var tileOrigin = new Vector2(pos.TileX * tileSize, pos.TileY * tileSize);
                var screenPos = camera.WorldToScreen(tileOrigin);
                destRect = new Rectangle((int)screenPos.X, (int)screenPos.Y, tileSize, tileSize);
            }
            else
            {
                // Legacy vector sprites: center on tile
                var tileCenter = new Vector2(
                    pos.TileX * tileSize + tileSize / 2f,
                    pos.TileY * tileSize + tileSize / 2f);
                var screenPos = camera.WorldToScreen(tileCenter);
                destRect = new Rectangle(
                    (int)(screenPos.X - texture.Width / 2f),
                    (int)(screenPos.Y - texture.Height / 2f),
                    texture.Width, texture.Height);
            }

            spriteBatch.Draw(texture, destRect, Color.White);
        }
    }
}

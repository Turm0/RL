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

public class EntityRenderer
{
    private readonly VectorRasterizer _rasterizer;
    private readonly TextureCache _cache;
    private readonly SpriteGenerator _spriteGenerator;
    private readonly TerrainObjectGenerator _objectGenerator;

    public EntityRenderer(VectorRasterizer rasterizer, TextureCache cache)
    {
        _rasterizer = rasterizer;
        _cache = cache;
        _objectGenerator = new TerrainObjectGenerator();

        string contentRoot = Path.Combine(AppContext.BaseDirectory, "Content", "Sprites");
        _spriteGenerator = new SpriteGenerator(contentRoot);
    }

    public void Draw(SpriteBatch spriteBatch, Camera camera, DefaultEcs.World world,
        int tileSize, FogOfWar fow, Func<int, int, bool> isHiddenByRoof = null)
    {
        using var entitySet = world.GetEntities()
            .With<Position>()
            .With<SpriteShape>()
            .AsSet();

        var visible = new List<Entity>();
        foreach (ref readonly var entity in entitySet.GetEntities())
        {
            ref readonly var pos = ref entity.Get<Position>();
            if (!camera.IsInView(pos.TileX, pos.TileY, tileSize)) continue;
            if (!fow.IsVisible(pos.TileX, pos.TileY)) continue;
            if (isHiddenByRoof != null && isHiddenByRoof(pos.TileX, pos.TileY)) continue;
            visible.Add(entity);
        }

        // Sort by: RenderLayer → Y → X
        visible.Sort((a, b) =>
        {
            byte layerA = a.Has<RenderLayer>() ? a.Get<RenderLayer>().Layer : RenderLayer.CreatureLayer;
            byte layerB = b.Has<RenderLayer>() ? b.Get<RenderLayer>().Layer : RenderLayer.CreatureLayer;
            int cmp = layerA.CompareTo(layerB);
            if (cmp != 0) return cmp;

            ref readonly var pa = ref a.Get<Position>();
            ref readonly var pb = ref b.Get<Position>();
            cmp = pa.TileY.CompareTo(pb.TileY);
            return cmp != 0 ? cmp : pa.TileX.CompareTo(pb.TileX);
        });

        foreach (var entity in visible)
        {
            ref readonly var pos = ref entity.Get<Position>();
            ref readonly var shape = ref entity.Get<SpriteShape>();

            string creatureType = shape.CreatureType;
            float size = shape.Size;
            int tileX = pos.TileX, tileY = pos.TileY;
            bool isTerrainObj = entity.Has<TerrainObject>();
            string objType = isTerrainObj ? entity.Get<TerrainObject>().ObjectType : null;
            string cacheKey = isTerrainObj ? $"obj_{objType}_{tileX}_{tileY}" : $"{creatureType}_{size}";

            var texture = _cache.GetOrCreate(cacheKey, () =>
            {
                if (isTerrainObj)
                    return _objectGenerator.Generate(spriteBatch.GraphicsDevice, objType,
                        (int)(tileX * 374761393 + tileY * 668265263));

                if (creatureType.EndsWith(".yaml"))
                    return _spriteGenerator.Generate(spriteBatch.GraphicsDevice, creatureType);
                return _rasterizer.RasterizeCreature(creatureType, size, tileSize, spriteBatch.GraphicsDevice);
            });

            Rectangle destRect;

            if (creatureType.EndsWith(".yaml") || entity.Has<TerrainObject>() || entity.Has<GroundItem>())
            {
                var tileOrigin = new Vector2(pos.TileX * tileSize, pos.TileY * tileSize);
                var screenPos = camera.WorldToScreen(tileOrigin);
                destRect = new Rectangle((int)screenPos.X, (int)screenPos.Y, tileSize, tileSize);
            }
            else
            {
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

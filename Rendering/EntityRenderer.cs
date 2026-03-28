using System.Collections.Generic;
using DefaultEcs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Draws entities as cached vector-rasterized textures, y-sorted.
/// </summary>
public class EntityRenderer
{
    private readonly VectorRasterizer _rasterizer;
    private readonly TextureCache _cache;

    public EntityRenderer(VectorRasterizer rasterizer, TextureCache cache)
    {
        _rasterizer = rasterizer;
        _cache = cache;
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
            var texture = _cache.GetOrCreate(cacheKey,
                () => _rasterizer.RasterizeCreature(creatureType, size, tileSize, spriteBatch.GraphicsDevice));

            // Center the texture on the tile center
            var tileCenter = new Vector2(
                pos.TileX * tileSize + tileSize / 2f,
                pos.TileY * tileSize + tileSize / 2f);
            var screenPos = camera.WorldToScreen(tileCenter);

            var destRect = new Rectangle(
                (int)(screenPos.X - texture.Width / 2f),
                (int)(screenPos.Y - texture.Height / 2f),
                texture.Width,
                texture.Height);

            spriteBatch.Draw(texture, destRect, Color.White);
        }
    }
}

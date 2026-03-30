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
    private readonly ObjectSpriteGenerator _objectSpriteGenerator;
    private readonly List<Entity> _visibleEntities = new();
    private readonly Dictionary<string, Texture2D[]> _objectAnimCache = new();
    private float _animTime;

    public EntityRenderer(VectorRasterizer rasterizer, TextureCache cache)
    {
        _rasterizer = rasterizer;
        _cache = cache;
        _objectGenerator = new TerrainObjectGenerator();

        string contentRoot = Path.Combine(AppContext.BaseDirectory, "Content", "Sprites");
        _spriteGenerator = new SpriteGenerator(contentRoot);
        _objectSpriteGenerator = new ObjectSpriteGenerator(contentRoot);
    }

    public void Draw(SpriteBatch spriteBatch, Camera camera, DefaultEcs.World world,
        int tileSize, FogOfWar fow, float deltaTime, Func<int, int, bool> isHiddenByRoof = null)
    {
        using var entitySet = world.GetEntities()
            .With<Position>()
            .With<SpriteShape>()
            .AsSet();

        _animTime += deltaTime;
        _visibleEntities.Clear();
        foreach (ref readonly var entity in entitySet.GetEntities())
        {
            ref readonly var pos = ref entity.Get<Position>();
            if (!camera.IsInView(pos.TileX, pos.TileY, tileSize)) continue;
            if (!fow.IsVisible(pos.TileX, pos.TileY)) continue;
            if (isHiddenByRoof != null && isHiddenByRoof(pos.TileX, pos.TileY)) continue;
            _visibleEntities.Add(entity);
        }

        // Sort by: RenderLayer → Y → X
        _visibleEntities.Sort((a, b) =>
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

        foreach (var entity in _visibleEntities)
        {
            ref readonly var pos = ref entity.Get<Position>();
            ref readonly var shape = ref entity.Get<SpriteShape>();

            string creatureType = shape.CreatureType;
            float size = shape.Size;
            int tileX = pos.TileX, tileY = pos.TileY;
            bool isTerrainObj = entity.Has<TerrainObject>();
            string objType = isTerrainObj ? entity.Get<TerrainObject>().ObjectType : null;
            // Use creature type directly as key (avoids string allocation per frame)
            // Size variants are rare, so append only when non-default
            string cacheKey;
            if (isTerrainObj)
                cacheKey = $"obj_{objType}_{tileX}_{tileY}";
            else if (size == 1.0f)
                cacheKey = creatureType;
            else
                cacheKey = $"{creatureType}_{size}";

            Texture2D texture;
            bool isObjectYaml = creatureType.StartsWith("objects/") && creatureType.EndsWith(".yaml");

            if (isObjectYaml)
            {
                var def = _objectSpriteGenerator.GetDefinition(creatureType);
                if (def.Animated && def.FrameCount > 1)
                {
                    // Animated object — use frame cache
                    if (!_objectAnimCache.TryGetValue(creatureType, out var frames))
                    {
                        frames = new Texture2D[def.FrameCount];
                        _objectAnimCache[creatureType] = frames;
                    }
                    int frameIdx = (int)(deltaTime * def.FrameRate) % def.FrameCount;
                    // Use total time, not delta — need to pass total time
                    // For now, use a simple global frame counter
                    frameIdx = (int)(_animTime * def.FrameRate) % def.FrameCount;
                    if (frames[frameIdx] == null)
                        frames[frameIdx] = _objectSpriteGenerator.GenerateFrame(
                            spriteBatch.GraphicsDevice, creatureType, frameIdx);
                    texture = frames[frameIdx];
                }
                else
                {
                    texture = _cache.GetOrCreate(cacheKey, () =>
                        _objectSpriteGenerator.Generate(spriteBatch.GraphicsDevice, creatureType));
                }
            }
            else
            {
                texture = _cache.GetOrCreate(cacheKey, () =>
                {
                    if (isTerrainObj)
                        return _objectGenerator.Generate(spriteBatch.GraphicsDevice, objType,
                            (int)(tileX * 374761393 + tileY * 668265263));

                    if (creatureType.EndsWith(".yaml"))
                        return _spriteGenerator.Generate(spriteBatch.GraphicsDevice, creatureType);
                    return _rasterizer.RasterizeCreature(creatureType, size, tileSize, spriteBatch.GraphicsDevice);
                });
            }

            // Compute world position with movement animation offset
            float worldPixelX = pos.TileX * tileSize;
            float worldPixelY = pos.TileY * tileSize;
            float yOffset = 0f;

            if (entity.Has<MovementAnimation>())
            {
                ref var anim = ref entity.Get<MovementAnimation>();
                if (anim.Moving)
                {
                    anim.Progress += anim.Speed * deltaTime;
                    if (anim.Progress >= 1f)
                    {
                        anim.Progress = 1f;
                        anim.Moving = false;
                    }

                    float t = anim.Progress;

                    switch (anim.Type)
                    {
                        case MoveAnimType.Slide:
                            // Smooth lerp from old to new position + subtle bob
                            float st = t * t * (3f - 2f * t); // smoothstep
                            worldPixelX = anim.FromX * tileSize + (pos.TileX * tileSize - anim.FromX * tileSize) * st;
                            worldPixelY = anim.FromY * tileSize + (pos.TileY * tileSize - anim.FromY * tileSize) * st;
                            yOffset = -MathF.Sin(t * MathF.PI) * 1.0f; // subtle 1px bob
                            break;

                        case MoveAnimType.Hop:
                            // Slide + vertical arc
                            float ht = t * t * (3f - 2f * t);
                            worldPixelX = anim.FromX * tileSize + (pos.TileX * tileSize - anim.FromX * tileSize) * ht;
                            worldPixelY = anim.FromY * tileSize + (pos.TileY * tileSize - anim.FromY * tileSize) * ht;
                            yOffset = -MathF.Sin(t * MathF.PI) * tileSize * 0.3f;
                            break;

                        case MoveAnimType.Bob:
                            // Instant position, subtle vertical bob
                            yOffset = -MathF.Sin(t * MathF.PI) * tileSize * 0.12f;
                            break;
                    }
                }
            }

            Rectangle destRect;

            if (entity.Has<TerrainObject>() || entity.Has<GroundItem>())
            {
                var screenPos = camera.WorldToScreen(new Vector2(worldPixelX, worldPixelY + yOffset));
                destRect = new Rectangle((int)screenPos.X, (int)screenPos.Y, tileSize, tileSize);
            }
            else if (creatureType.EndsWith(".yaml"))
            {
                // Creatures are slightly larger than a tile, anchored at bottom
                int drawH = (int)(tileSize * 1.3f);
                int drawW = (int)(tileSize * 1.3f);
                var screenPos = camera.WorldToScreen(new Vector2(worldPixelX, worldPixelY + yOffset));
                int xOff = (tileSize - drawW) / 2;
                destRect = new Rectangle((int)screenPos.X + xOff, (int)screenPos.Y + tileSize - drawH, drawW, drawH);
            }
            else
            {
                // Vector-rasterized sprites — draw at their size, centered and bottom-anchored
                int drawW = (int)(tileSize * size);
                int drawH = (int)(tileSize * size);
                var screenPos = camera.WorldToScreen(new Vector2(worldPixelX, worldPixelY + yOffset));
                int xOff = (tileSize - drawW) / 2;
                destRect = new Rectangle((int)screenPos.X + xOff, (int)screenPos.Y + tileSize - drawH, drawW, drawH);
            }

            spriteBatch.Draw(texture, destRect, Color.White);
        }
    }
}

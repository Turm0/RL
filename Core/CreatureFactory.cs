using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RoguelikeEngine.ECS.Components;

namespace RoguelikeEngine.Core;

/// <summary>
/// High-level API for spawning creatures from layered definitions.
/// Loads layers from the content registry, resolves them, creates ECS entities.
/// </summary>
public class CreatureFactory
{
    private readonly ContentRegistry _registry;
    private readonly Random _rng;

    public CreatureFactory(ContentRegistry registry, Random rng = null)
    {
        _registry = registry;
        _rng = rng ?? new Random();
    }

    /// <summary>
    /// Spawns a creature from an ordered list of layer ids.
    /// Example: Spawn(world, 10, 20, "human", "ranger")
    /// Example: Spawn(world, 10, 20, "human", "guard", "city_watch", "noble_district")
    /// </summary>
    public DefaultEcs.Entity Spawn(DefaultEcs.World world, int x, int y, params string[] layerIds)
    {
        // Load all layers
        var layers = new List<EntityLayerDefinition>();
        foreach (var id in layerIds)
        {
            string path = _registry.ResolvePath(id);
            var layer = EntityLayerLoader.Load(path);
            layers.Add(layer);
        }

        // Resolve (merge + roll pools)
        var resolved = EntityLayerResolver.Resolve(layers, _rng);

        // Create entity
        return CreateEntity(world, x, y, resolved);
    }

    /// <summary>
    /// Spawns a creature from a pre-resolved definition.
    /// </summary>
    public DefaultEcs.Entity Spawn(DefaultEcs.World world, int x, int y, ResolvedEntity resolved)
    {
        return CreateEntity(world, x, y, resolved);
    }

    private DefaultEcs.Entity CreateEntity(DefaultEcs.World world, int x, int y, ResolvedEntity resolved)
    {
        var entity = world.CreateEntity();

        entity.Set(new Position(x, y));

        // SpriteShape — use the body template as a creature YAML path for now
        // The appearance system will override the rendering
        entity.Set(new SpriteShape("creatures/human_ranger.yaml", 1.0f));
        entity.Set(new RenderLayer(RenderLayer.CreatureLayer));

        // Appearance
        var appearance = new Appearance(
            resolved.Skin.Base,
            resolved.Skin.Shadow,
            resolved.Skin.Highlight,
            resolved.EyeColor,
            resolved.OutlineColor);

        foreach (var att in resolved.FeatureAttachments)
        {
            if (att.ObjectId == null) continue;
            string objPath = ResolveObjectPath(att.ObjectId);
            if (objPath == null) continue;
            appearance.Attachments.Add(new AttachmentSlot(
                objPath, att.Joint, att.ZOrder,
                materialOverrides: att.MaterialOverrides));
        }

        entity.Set(appearance);

        // Equipment
        if (resolved.EquipmentAttachments.Count > 0)
        {
            var equipment = new Equipment();
            foreach (var att in resolved.EquipmentAttachments)
            {
                if (att.ObjectId == null) continue;
                string objPath = ResolveObjectPath(att.ObjectId);
                if (objPath == null) continue;
                equipment.Slots.Add(new AttachmentSlot(
                    objPath, att.Joint, att.ZOrder,
                    materialOverrides: att.MaterialOverrides));
            }
            entity.Set(equipment);
        }

        return entity;
    }

    /// <summary>
    /// Resolves a content id to a file path relative to the Sprites directory.
    /// Falls back to using the id as-is if it looks like a path.
    /// </summary>
    private string ResolveObjectPath(string id)
    {
        if (id == null) return null;

        // If it already looks like a path, use it
        if (id.Contains('/') || id.EndsWith(".yaml"))
            return id;

        // Try to resolve via registry
        if (_registry.Has(id))
        {
            string fullPath = _registry.GetPath(id);
            // Convert to relative path from Sprites directory
            string spritesDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Content", "Sprites");
            if (fullPath.StartsWith(spritesDir))
                return fullPath.Substring(spritesDir.Length + 1).Replace('\\', '/');
            return fullPath;
        }

        return id;
    }
}

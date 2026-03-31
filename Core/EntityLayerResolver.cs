using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Core;

/// <summary>
/// Takes an ordered list of entity layers, merges them (flat merge),
/// rolls all pools, and produces a ResolvedEntity with concrete values.
/// </summary>
public static class EntityLayerResolver
{
    public static ResolvedEntity Resolve(List<EntityLayerDefinition> layers, Random rng)
    {
        // Phase 1: Merge all layers into a single combined layer
        var merged = MergeLayers(layers);

        // Phase 2: Roll all pools to get concrete values
        return RollPools(merged, rng);
    }

    private static EntityLayerDefinition MergeLayers(List<EntityLayerDefinition> layers)
    {
        var result = new EntityLayerDefinition();

        foreach (var layer in layers)
        {
            // Single values: later replaces earlier
            if (layer.Name != null) result.Name = layer.Name;
            if (layer.BodyTemplate != null) result.BodyTemplate = layer.BodyTemplate;
            if (layer.Pose != null) result.Pose = layer.Pose;

            // Appearance: merge per-field
            if (layer.Appearance != null)
            {
                result.Appearance ??= new AppearanceLayer();

                if (layer.Appearance.Skin != null)
                    result.Appearance.Skin = layer.Appearance.Skin;
                if (layer.Appearance.Eyes != null)
                    result.Appearance.Eyes = layer.Appearance.Eyes;
                if (layer.Appearance.Outline.HasValue)
                    result.Appearance.Outline = layer.Appearance.Outline;
                if (layer.Appearance.HairColors != null)
                    result.Appearance.HairColors = layer.Appearance.HairColors;

                // Features: merge per-category (later replaces pool for that category)
                if (layer.Appearance.Features != null)
                {
                    result.Appearance.Features ??= new Dictionary<string, FeaturePoolDef>();
                    foreach (var kv in layer.Appearance.Features)
                        result.Appearance.Features[kv.Key] = kv.Value;
                }
            }

            // Equipment: merge per-slot
            if (layer.Equipment != null)
            {
                result.Equipment ??= new EquipmentLayer { Slots = new Dictionary<string, Pool<string>>() };
                foreach (var kv in layer.Equipment.Slots)
                    result.Equipment.Slots[kv.Key] = kv.Value;
            }
        }

        return result;
    }

    private static ResolvedEntity RollPools(EntityLayerDefinition merged, Random rng)
    {
        var entity = new ResolvedEntity
        {
            Name = merged.Name,
            BodyTemplate = merged.BodyTemplate ?? "humanoid",
            Pose = merged.Pose ?? "idle"
        };

        if (merged.Appearance != null)
        {
            // Roll skin
            if (merged.Appearance.Skin != null && !merged.Appearance.Skin.IsEmpty)
                entity.Skin = merged.Appearance.Skin.Pick(rng);
            else
                entity.Skin = new SkinColorSet(new Color(200, 160, 120), new Color(160, 120, 80), new Color(230, 190, 150));

            // Roll eyes
            if (merged.Appearance.Eyes != null && !merged.Appearance.Eyes.IsEmpty)
                entity.EyeColor = merged.Appearance.Eyes.Pick(rng).Color;
            else
                entity.EyeColor = Color.Black;

            // Outline
            if (merged.Appearance.Outline.HasValue)
                entity.OutlineColor = merged.Appearance.Outline.Value;

            // Roll hair colors (used for all hair-like features)
            HairColorSet hairColors = default;
            if (merged.Appearance.HairColors != null && !merged.Appearance.HairColors.IsEmpty)
                hairColors = merged.Appearance.HairColors.Pick(rng);

            // Roll features
            if (merged.Appearance.Features != null)
            {
                foreach (var kv in merged.Appearance.Features)
                {
                    var fpd = kv.Value;
                    if (fpd.ObjectPool == null || fpd.ObjectPool.IsEmpty) continue;

                    string objectId = fpd.ObjectPool.Pick(rng);
                    if (objectId == null) continue; // rolled "none"

                    var attachment = new ResolvedAttachment
                    {
                        ObjectId = objectId,
                        Joint = fpd.Joint,
                        ZOrder = fpd.ZOrder,
                        MaterialOverrides = hairColors.MaterialOverrides
                    };
                    entity.FeatureAttachments.Add(attachment);
                }
            }
        }

        // Roll equipment
        if (merged.Equipment?.Slots != null)
        {
            foreach (var kv in merged.Equipment.Slots)
            {
                string slot = kv.Key;
                var pool = kv.Value;
                if (pool == null || pool.IsEmpty) continue;

                string itemId = pool.Pick(rng);
                if (itemId == null) continue; // rolled "none"

                // Equipment items know their own joint/z-order from their definition
                // For now, use slot-based defaults
                var attachment = new ResolvedAttachment
                {
                    ObjectId = itemId,
                    Joint = SlotToJoint(slot),
                    ZOrder = SlotToZOrder(slot)
                };
                entity.EquipmentAttachments.Add(attachment);
            }
        }

        return entity;
    }

    private static string SlotToJoint(string slot) => slot switch
    {
        "main_hand" => "grip_right",
        "off_hand" => "grip_left",
        "head" => "hair_top",
        "chest" => "chest",
        _ => "chest"
    };

    private static int SlotToZOrder(string slot) => slot switch
    {
        "main_hand" => 16,
        "off_hand" => 16,
        "head" => 17,
        "chest" => 13,
        _ => 14
    };
}

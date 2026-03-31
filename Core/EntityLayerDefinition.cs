using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Core;

/// <summary>
/// Unified layer format. Every layer — species, occupation, faction, location,
/// individual — uses the exact same structure. Layers stack, later overrides earlier.
/// </summary>
public class EntityLayerDefinition
{
    public string Id;
    public string Name;
    public string BodyTemplate;    // e.g. "humanoid"
    public string Pose;            // e.g. "idle"

    public AppearanceLayer Appearance;
    public EquipmentLayer Equipment;

    // Future: StatsLayer, AbilitiesLayer, etc.
}

public class AppearanceLayer
{
    public Pool<SkinColorSet> Skin;
    public Pool<EyeColorSet> Eyes;
    public Color? Outline;
    public Dictionary<string, FeaturePoolDef> Features; // "hair" → pool of feature objects
    public Pool<HairColorSet> HairColors;
}

public class FeaturePoolDef
{
    public string Joint;
    public int ZOrder = 15;
    public Pool<string> ObjectPool; // pool of content ids (or null for "none")
}

public class EquipmentLayer
{
    public Dictionary<string, Pool<string>> Slots; // "main_hand" → pool of equipment ids
}

// --- Value types for pools ---

public struct SkinColorSet
{
    public Color Base, Shadow, Highlight;

    public SkinColorSet(Color b, Color s, Color h) { Base = b; Shadow = s; Highlight = h; }
}

public struct EyeColorSet
{
    public Color Color;
    public EyeColorSet(Color c) { Color = c; }
}

public struct HairColorSet
{
    public Dictionary<string, Color> MaterialOverrides;

    public HairColorSet(Dictionary<string, Color> overrides) { MaterialOverrides = overrides; }
}

/// <summary>
/// The result of resolving all layers — concrete values, no pools.
/// </summary>
public class ResolvedEntity
{
    public string Name;
    public string BodyTemplate = "humanoid";
    public string Pose = "idle";

    // Concrete appearance
    public SkinColorSet Skin;
    public Color EyeColor;
    public Color OutlineColor = new(26, 26, 42);
    public List<ResolvedAttachment> FeatureAttachments = new();

    // Concrete equipment
    public List<ResolvedAttachment> EquipmentAttachments = new();
}

public class ResolvedAttachment
{
    public string ObjectId;  // content registry id
    public string Joint;
    public int ZOrder;
    public Dictionary<string, Color> MaterialOverrides;
}

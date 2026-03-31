using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using YamlDotNet.RepresentationModel;

namespace RoguelikeEngine.Core;

/// <summary>
/// Parses any entity layer YAML (species, occupation, faction, location, individual)
/// into an EntityLayerDefinition. All layer types use the same format.
/// </summary>
public static class EntityLayerLoader
{
    public static EntityLayerDefinition Load(string path)
    {
        string text = File.ReadAllText(path);
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        var def = new EntityLayerDefinition();
        def.Id = GetStringOr(root, "id", null);
        def.Name = GetStringOr(root, "name", null);
        def.BodyTemplate = GetStringOr(root, "body_template", null);
        def.Pose = GetStringOr(root, "pose", null);

        // Parse appearance section
        if (root.Children.ContainsKey("appearance"))
        {
            def.Appearance = ParseAppearance((YamlMappingNode)root["appearance"]);
        }

        // Parse equipment section
        if (root.Children.ContainsKey("equipment"))
        {
            def.Equipment = ParseEquipment((YamlMappingNode)root["equipment"]);
        }

        return def;
    }

    private static AppearanceLayer ParseAppearance(YamlMappingNode node)
    {
        var app = new AppearanceLayer();

        // Skin — pool of SkinColorSet or fixed value
        if (node.Children.ContainsKey("skin"))
        {
            var skinNode = node["skin"];
            if (skinNode is YamlMappingNode skinMap && skinMap.Children.ContainsKey("pool"))
            {
                app.Skin = new Pool<SkinColorSet>();
                foreach (YamlMappingNode entry in (YamlSequenceNode)skinMap["pool"])
                {
                    float weight = GetFloatOr(entry, "weight", 1f);
                    var skin = new SkinColorSet(
                        ParseHexColor(GetString(entry, "base")),
                        ParseHexColor(GetString(entry, "shadow")),
                        ParseHexColor(GetString(entry, "highlight")));
                    app.Skin.Add(skin, weight);
                }
            }
            else if (skinNode is YamlMappingNode fixedSkin)
            {
                // Fixed value shorthand
                var skin = new SkinColorSet(
                    ParseHexColor(GetString(fixedSkin, "base")),
                    ParseHexColor(GetString(fixedSkin, "shadow")),
                    ParseHexColor(GetString(fixedSkin, "highlight")));
                app.Skin = Pool<SkinColorSet>.Fixed(skin);
            }
        }

        // Eyes — pool of EyeColorSet or fixed
        if (node.Children.ContainsKey("eyes"))
        {
            var eyesNode = node["eyes"];
            if (eyesNode is YamlMappingNode eyesMap && eyesMap.Children.ContainsKey("pool"))
            {
                app.Eyes = new Pool<EyeColorSet>();
                foreach (YamlMappingNode entry in (YamlSequenceNode)eyesMap["pool"])
                {
                    float weight = GetFloatOr(entry, "weight", 1f);
                    app.Eyes.Add(new EyeColorSet(ParseHexColor(GetString(entry, "color"))), weight);
                }
            }
            else if (eyesNode is YamlMappingNode fixedEye)
            {
                app.Eyes = Pool<EyeColorSet>.Fixed(new EyeColorSet(ParseHexColor(GetString(fixedEye, "color"))));
            }
        }

        // Outline
        if (node.Children.ContainsKey("outline"))
            app.Outline = ParseHexColor(((YamlScalarNode)node["outline"]).Value);

        // Features — pools per category
        if (node.Children.ContainsKey("features"))
        {
            app.Features = new Dictionary<string, FeaturePoolDef>();
            var featNode = (YamlMappingNode)node["features"];
            foreach (var kv in featNode.Children)
            {
                string category = ((YamlScalarNode)kv.Key).Value;
                var catNode = (YamlMappingNode)kv.Value;

                var fpd = new FeaturePoolDef();
                fpd.Joint = GetStringOr(catNode, "joint", category);
                fpd.ZOrder = GetIntOr(catNode, "z_order", 15);

                if (catNode.Children.ContainsKey("pool"))
                {
                    fpd.ObjectPool = new Pool<string>();
                    foreach (YamlMappingNode entry in (YamlSequenceNode)catNode["pool"])
                    {
                        float weight = GetFloatOr(entry, "weight", 1f);
                        string id = GetStringOr(entry, "id", null);
                        fpd.ObjectPool.Add(id, weight); // null = "no feature"
                    }
                }
                else if (catNode.Children.ContainsKey("id"))
                {
                    // Fixed value
                    fpd.ObjectPool = Pool<string>.Fixed(GetString(catNode, "id"));
                }

                app.Features[category] = fpd;
            }
        }

        // Hair colors
        if (node.Children.ContainsKey("hair_colors"))
        {
            var hcNode = node["hair_colors"];
            if (hcNode is YamlMappingNode hcMap && hcMap.Children.ContainsKey("pool"))
            {
                app.HairColors = new Pool<HairColorSet>();
                foreach (YamlMappingNode entry in (YamlSequenceNode)hcMap["pool"])
                {
                    float weight = GetFloatOr(entry, "weight", 1f);
                    var overrides = new Dictionary<string, Color>();
                    foreach (var colorKv in entry.Children)
                    {
                        string key = ((YamlScalarNode)colorKv.Key).Value;
                        if (key == "weight") continue;
                        overrides[key] = ParseHexColor(((YamlScalarNode)colorKv.Value).Value);
                    }
                    app.HairColors.Add(new HairColorSet(overrides), weight);
                }
            }
        }

        return app;
    }

    private static EquipmentLayer ParseEquipment(YamlMappingNode node)
    {
        var eq = new EquipmentLayer { Slots = new Dictionary<string, Pool<string>>() };

        foreach (var kv in node.Children)
        {
            string slot = ((YamlScalarNode)kv.Key).Value;

            if (kv.Value is YamlMappingNode slotNode && slotNode.Children.ContainsKey("pool"))
            {
                var pool = new Pool<string>();
                foreach (YamlMappingNode entry in (YamlSequenceNode)slotNode["pool"])
                {
                    float weight = GetFloatOr(entry, "weight", 1f);
                    string id = GetStringOr(entry, "id", null);
                    pool.Add(id, weight);
                }
                eq.Slots[slot] = pool;
            }
            else if (kv.Value is YamlMappingNode fixedSlot)
            {
                string id = GetStringOr(fixedSlot, "id", null);
                if (id != null)
                    eq.Slots[slot] = Pool<string>.Fixed(id);
            }
        }

        return eq;
    }

    // --- Helpers ---

    private static string GetString(YamlMappingNode node, string key)
        => ((YamlScalarNode)node[key]).Value;

    private static string GetStringOr(YamlMappingNode node, string key, string def)
    {
        if (!node.Children.ContainsKey(key)) return def;
        var val = ((YamlScalarNode)node[key]).Value;
        // YAML null values should map to C# null
        if (val == null || val == "null" || val == "~") return null;
        return val;
    }

    private static int GetIntOr(YamlMappingNode node, string key, int def)
        => node.Children.ContainsKey(key) ? int.Parse(((YamlScalarNode)node[key]).Value) : def;

    private static float GetFloatOr(YamlMappingNode node, string key, float def)
        => node.Children.ContainsKey(key) ? float.Parse(((YamlScalarNode)node[key]).Value, CultureInfo.InvariantCulture) : def;

    private static Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
        int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
        int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
        return new Color(r, g, b);
    }
}

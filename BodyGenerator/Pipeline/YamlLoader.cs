using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using YamlDotNet.RepresentationModel;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public static class YamlLoader
{
    public static BodyTemplate LoadBodyTemplate(string path)
    {
        var yaml = LoadYamlDocument(path);
        var root = (YamlMappingNode)yaml.RootNode;

        var template = new BodyTemplate
        {
            Name = GetString(root, "name"),
            SpriteSize = GetInt(root, "sprite_size"),
            Parts = new Dictionary<string, BodyPartTemplate>()
        };

        // Parse hierarchy
        var hierarchyNode = (YamlMappingNode)root["hierarchy"];
        template.Hierarchy = ParseHierarchyNode(hierarchyNode);

        // Parse parts
        var partsNode = (YamlMappingNode)root["parts"];
        foreach (var kvp in partsNode.Children)
        {
            string partId = ((YamlScalarNode)kvp.Key).Value;
            var partMap = (YamlMappingNode)kvp.Value;
            template.Parts[partId] = ParseBodyPart(partId, partMap);
        }

        return template;
    }

    public static PoseDefinition LoadPose(string path)
    {
        var yaml = LoadYamlDocument(path);
        var root = (YamlMappingNode)yaml.RootNode;

        var pose = new PoseDefinition
        {
            Name = GetString(root, "name"),
            RootPosition = ParsePoint((YamlSequenceNode)root["root_position"]),
            DrawOrder = new List<string>(),
            JointOffsets = new Dictionary<string, Point>()
        };

        var drawOrder = (YamlSequenceNode)root["draw_order"];
        foreach (YamlScalarNode item in drawOrder)
        {
            pose.DrawOrder.Add(item.Value);
        }

        var offsets = root["joint_offsets"];
        if (offsets is YamlMappingNode offsetMap)
        {
            foreach (var kvp in offsetMap.Children)
            {
                string jointName = ((YamlScalarNode)kvp.Key).Value;
                var point = ParsePoint((YamlSequenceNode)kvp.Value);
                pose.JointOffsets[jointName] = point;
            }
        }

        return pose;
    }

    public static CreatureDefinition LoadCreature(string path)
    {
        var yaml = LoadYamlDocument(path);
        var root = (YamlMappingNode)yaml.RootNode;

        var creature = new CreatureDefinition
        {
            Name = GetString(root, "name"),
            BodyTemplate = GetString(root, "body_template"),
            Pose = GetString(root, "pose"),
            Palette = new Dictionary<ColorRole, Color>()
        };

        if (root.Children.ContainsKey("palette"))
        {
            var paletteNode = (YamlMappingNode)root["palette"];
            foreach (var kvp in paletteNode.Children)
            {
                string roleName = ((YamlScalarNode)kvp.Key).Value;
                string hexColor = ((YamlScalarNode)kvp.Value).Value;
                var role = ParseColorRoleName(roleName);
                var color = ParseHexColor(hexColor);
                creature.Palette[role] = color;
            }
        }

        // Parse appearance attachments
        if (root.Children.ContainsKey("attachments"))
        {
            creature.Attachments = new List<CreatureAttachment>();
            var attachNode = (YamlSequenceNode)root["attachments"];
            foreach (YamlMappingNode attNode in attachNode)
            {
                var att = new CreatureAttachment
                {
                    ObjectPath = GetString(attNode, "object"),
                    Joint = attNode.Children.ContainsKey("joint")
                        ? GetString(attNode, "joint") : null,
                    ZOrder = attNode.Children.ContainsKey("z_order")
                        ? GetInt(attNode, "z_order") : 15
                };
                if (attNode.Children.ContainsKey("material_overrides"))
                {
                    att.MaterialOverrides = new Dictionary<string, Color>();
                    var moNode = (YamlMappingNode)attNode["material_overrides"];
                    foreach (var kv in moNode.Children)
                    {
                        string matName = ((YamlScalarNode)kv.Key).Value;
                        string hex = ((YamlScalarNode)kv.Value).Value;
                        att.MaterialOverrides[matName] = ParseHexColor(hex);
                    }
                }
                creature.Attachments.Add(att);
            }
        }

        return creature;
    }

    private static BodyHierarchyNode ParseHierarchyNode(YamlMappingNode node)
    {
        var hierarchy = new BodyHierarchyNode
        {
            PartId = GetString(node, "part"),
            JointName = node.Children.ContainsKey(new YamlScalarNode("joint"))
                ? GetString(node, "joint")
                : null
        };

        if (node.Children.ContainsKey(new YamlScalarNode("children")))
        {
            var children = (YamlSequenceNode)node["children"];
            foreach (YamlMappingNode child in children)
            {
                hierarchy.Children.Add(ParseHierarchyNode(child));
            }
        }

        return hierarchy;
    }

    private static BodyPartTemplate ParseBodyPart(string id, YamlMappingNode node)
    {
        var part = new BodyPartTemplate
        {
            Id = id,
            Width = GetInt(node, "width"),
            Height = GetInt(node, "height"),
            Anchor = ParsePoint((YamlSequenceNode)node["anchor"]),
            Joints = new Dictionary<string, Point>()
        };

        var jointsNode = node["joints"];
        if (jointsNode is YamlMappingNode jointsMap)
        {
            foreach (var kvp in jointsMap.Children)
            {
                string jointName = ((YamlScalarNode)kvp.Key).Value;
                var point = ParsePoint((YamlSequenceNode)kvp.Value);
                part.Joints[jointName] = point;
            }
        }

        string pixelString = ((YamlScalarNode)node["pixels"]).Value;
        part.Pixels = TemplateParser.Parse(pixelString);

        return part;
    }

    private static YamlDocument LoadYamlDocument(string path)
    {
        string text = File.ReadAllText(path);
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents[0];
    }

    private static string GetString(YamlMappingNode node, string key)
    {
        return ((YamlScalarNode)node[key]).Value;
    }

    private static int GetInt(YamlMappingNode node, string key)
    {
        return int.Parse(((YamlScalarNode)node[key]).Value);
    }

    private static Point ParsePoint(YamlSequenceNode seq)
    {
        int x = int.Parse(((YamlScalarNode)seq[0]).Value);
        int y = int.Parse(((YamlScalarNode)seq[1]).Value);
        return new Point(x, y);
    }

    private static Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
        int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
        int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
        return new Color(r, g, b);
    }

    private static ColorRole ParseColorRoleName(string name)
    {
        return name switch
        {
            "skin_base" => ColorRole.SkinBase,
            "skin_shadow" => ColorRole.SkinShadow,
            "skin_highlight" => ColorRole.SkinHighlight,
            "cloth_base" => ColorRole.ClothBase,
            "cloth_shadow" => ColorRole.ClothShadow,
            "cloth_highlight" => ColorRole.ClothHighlight,
            "leg_base" => ColorRole.LegBase,
            "leg_shadow" => ColorRole.LegShadow,
            "belt" => ColorRole.Belt,
            "boot" => ColorRole.Boot,
            "eye" => ColorRole.Eye,
            "outline" => ColorRole.Outline,
            _ => throw new ArgumentException($"Unknown color role: {name}")
        };
    }
}

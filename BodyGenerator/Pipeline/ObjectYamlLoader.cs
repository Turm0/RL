using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using YamlDotNet.RepresentationModel;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public static class ObjectYamlLoader
{
    public static ObjectDefinition Load(string path)
    {
        string text = File.ReadAllText(path);
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        var def = new ObjectDefinition();
        def.Name = GetString(root, "name");
        def.SpriteSize = GetIntOr(root, "sprite_size", 32);
        def.PixelSize = GetIntOr(root, "pixel_size", 2);

        if (root.Children.ContainsKey("anchor"))
            def.Anchor = ParsePoint((YamlSequenceNode)root["anchor"]);

        def.Animated = GetBoolOr(root, "animated", false);
        def.FrameCount = GetIntOr(root, "frame_count", 1);
        def.FrameRate = GetFloatOr(root, "frame_rate", 6f);

        // Materials
        if (root.Children.ContainsKey("materials"))
        {
            var matsNode = (YamlMappingNode)root["materials"];
            foreach (var kv in matsNode.Children)
            {
                string matName = ((YamlScalarNode)kv.Key).Value;
                var matDef = new MaterialDef();

                if (kv.Value is YamlScalarNode scalar)
                {
                    // Short form: material_name: "#RRGGBB"
                    matDef.Color = ParseHexColor(scalar.Value);
                }
                else
                {
                    var matNode = (YamlMappingNode)kv.Value;
                    matDef.Color = ParseHexColor(GetString(matNode, "color"));
                    matDef.Alpha = (byte)GetIntOr(matNode, "alpha", 255);
                }

                def.Materials[matName] = matDef;
            }
        }

        // Frames (animated) or shapes (static)
        if (root.Children.ContainsKey("frames"))
        {
            var framesNode = (YamlSequenceNode)root["frames"];
            foreach (YamlMappingNode frameNode in framesNode)
            {
                var shapes = ParseShapeList((YamlSequenceNode)frameNode["shapes"]);
                def.Frames.Add(shapes);
            }
            def.FrameCount = def.Frames.Count;
        }
        else if (root.Children.ContainsKey("shapes"))
        {
            var shapes = ParseShapeList((YamlSequenceNode)root["shapes"]);
            def.Frames.Add(shapes);
            def.FrameCount = 1;
            def.Animated = false;
        }

        return def;
    }

    private static List<ShapeDef> ParseShapeList(YamlSequenceNode shapesNode)
    {
        var shapes = new List<ShapeDef>();
        foreach (YamlMappingNode shapeNode in shapesNode)
        {
            var shape = new ShapeDef();
            shape.Type = GetString(shapeNode, "type");
            shape.Material = GetStringOr(shapeNode, "material", null);
            shape.Filled = GetBoolOr(shapeNode, "filled", true);

            switch (shape.Type)
            {
                case "rect":
                    shape.X = GetInt(shapeNode, "x");
                    shape.Y = GetInt(shapeNode, "y");
                    shape.Width = GetInt(shapeNode, "width");
                    shape.Height = GetInt(shapeNode, "height");
                    break;

                case "oval":
                    var center = ParsePoint((YamlSequenceNode)shapeNode["center"]);
                    shape.CenterX = center.X;
                    shape.CenterY = center.Y;
                    var radius = ParsePoint((YamlSequenceNode)shapeNode["radius"]);
                    shape.RadiusX = radius.X;
                    shape.RadiusY = radius.Y;
                    break;

                case "line":
                    var from = ParsePoint((YamlSequenceNode)shapeNode["from"]);
                    var to = ParsePoint((YamlSequenceNode)shapeNode["to"]);
                    shape.FromX = from.X;
                    shape.FromY = from.Y;
                    shape.ToX = to.X;
                    shape.ToY = to.Y;
                    shape.LineWidth = GetIntOr(shapeNode, "width", 1);
                    break;

                case "flame":
                    var fc = ParsePoint((YamlSequenceNode)shapeNode["center"]);
                    shape.CenterX = fc.X;
                    shape.CenterY = fc.Y;
                    shape.Width = GetInt(shapeNode, "width");
                    shape.Height = GetInt(shapeNode, "height");
                    break;

                case "polygon":
                    shape.Points = new List<Point>();
                    var pointsNode = (YamlSequenceNode)shapeNode["points"];
                    foreach (YamlSequenceNode pt in pointsNode)
                        shape.Points.Add(ParsePoint(pt));
                    break;
            }

            shapes.Add(shape);
        }
        return shapes;
    }

    private static string GetString(YamlMappingNode node, string key)
        => ((YamlScalarNode)node[key]).Value;

    private static string GetStringOr(YamlMappingNode node, string key, string def)
        => node.Children.ContainsKey(key) ? ((YamlScalarNode)node[key]).Value : def;

    private static int GetInt(YamlMappingNode node, string key)
        => int.Parse(((YamlScalarNode)node[key]).Value);

    private static int GetIntOr(YamlMappingNode node, string key, int def)
        => node.Children.ContainsKey(key) ? int.Parse(((YamlScalarNode)node[key]).Value) : def;

    private static float GetFloatOr(YamlMappingNode node, string key, float def)
        => node.Children.ContainsKey(key) ? float.Parse(((YamlScalarNode)node[key]).Value, CultureInfo.InvariantCulture) : def;

    private static bool GetBoolOr(YamlMappingNode node, string key, bool def)
        => node.Children.ContainsKey(key) ? bool.Parse(((YamlScalarNode)node[key]).Value) : def;

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
}

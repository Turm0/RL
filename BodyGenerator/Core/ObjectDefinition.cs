using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace BodyGenerator.Core;

public class ObjectDefinition
{
    public string Name;
    public int SpriteSize = 32;
    public Point Anchor;
    public bool Animated;
    public int FrameCount = 1;
    public float FrameRate = 6f;
    public int PixelSize = 2;
    public Dictionary<string, MaterialDef> Materials = new();
    public List<List<ShapeDef>> Frames = new(); // frames[frameIdx] = shapes list
}

public class MaterialDef
{
    public Color Color;
    public byte Alpha = 255;
}

public class ShapeDef
{
    public string Type;      // "rect", "oval", "line", "flame", "polygon"
    public string Material;  // key into Materials
    public bool Filled = true;
    // Shape-specific parameters stored generically
    public int X, Y, Width, Height;
    public int CenterX, CenterY;
    public int RadiusX, RadiusY;
    public int FromX, FromY, ToX, ToY;
    public int LineWidth = 1;
    public List<Point> Points; // for polygon
    // For 'pixels' shape type
    public int OffsetX, OffsetY;
    public Dictionary<char, string> MaterialMap; // char → material name
    public string[] Grid; // rows of characters
}

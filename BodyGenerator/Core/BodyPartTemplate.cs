using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace BodyGenerator.Core;

public class BodyPartTemplate
{
    public string Id;
    public int Width;
    public int Height;
    public Point Anchor;
    public Dictionary<string, Point> Joints;
    public List<TemplatePixel> Pixels;
}

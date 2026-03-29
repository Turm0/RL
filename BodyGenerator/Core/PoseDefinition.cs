using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace BodyGenerator.Core;

public class PoseDefinition
{
    public string Name;
    public Point RootPosition;
    public List<string> DrawOrder;
    public Dictionary<string, Point> JointOffsets;
}

using System.Collections.Generic;

namespace BodyGenerator.Core;

public class BodyTemplate
{
    public string Name;
    public int SpriteSize;
    public Dictionary<string, BodyPartTemplate> Parts;
    public BodyHierarchyNode Hierarchy;
}

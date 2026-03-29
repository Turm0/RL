using System.Collections.Generic;

namespace BodyGenerator.Core;

public class BodyHierarchyNode
{
    public string PartId;
    public string JointName;
    public List<BodyHierarchyNode> Children = new();
}

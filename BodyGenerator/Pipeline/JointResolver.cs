using System.Collections.Generic;
using Microsoft.Xna.Framework;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public static class JointResolver
{
    public static Dictionary<string, Point> Resolve(BodyTemplate body, PoseDefinition pose)
    {
        var positions = new Dictionary<string, Point>();
        ResolveRecursive(body, pose, body.Hierarchy, pose.RootPosition, positions);
        return positions;
    }

    private static void ResolveRecursive(
        BodyTemplate body,
        PoseDefinition pose,
        BodyHierarchyNode node,
        Point parentWorldJoint,
        Dictionary<string, Point> positions)
    {
        var part = body.Parts[node.PartId];

        int worldX = parentWorldJoint.X - part.Anchor.X;
        int worldY = parentWorldJoint.Y - part.Anchor.Y;
        positions[node.PartId] = new Point(worldX, worldY);

        foreach (var child in node.Children)
        {
            var jointLocal = part.Joints[child.JointName];
            int jointWorldX = worldX + jointLocal.X;
            int jointWorldY = worldY + jointLocal.Y;

            if (pose.JointOffsets.TryGetValue(child.JointName, out var offset))
            {
                jointWorldX += offset.X;
                jointWorldY += offset.Y;
            }

            ResolveRecursive(body, pose, child, new Point(jointWorldX, jointWorldY), positions);
        }
    }
}

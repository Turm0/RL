using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.ECS.Components;

public struct AttachmentSlot
{
    public string ObjectPath;   // "features/hair_long.yaml", "objects/torch.yaml"
    public string Joint;        // "head", "hand_right", "torso"
    public Point Offset;        // pixel offset from joint position
    public int ZOrder;          // z-layer (14+ = over body, <0 = behind)
    public Dictionary<string, Color> MaterialOverrides; // optional per-entity color overrides

    public AttachmentSlot(string objectPath, string joint, int zOrder,
        int offsetX = 0, int offsetY = 0, Dictionary<string, Color> materialOverrides = null)
    {
        ObjectPath = objectPath;
        Joint = joint;
        Offset = new Point(offsetX, offsetY);
        ZOrder = zOrder;
        MaterialOverrides = materialOverrides;
    }
}

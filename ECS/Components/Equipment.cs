using System.Collections.Generic;

namespace RoguelikeEngine.ECS.Components;

public struct Equipment
{
    public List<AttachmentSlot> Slots;

    public Equipment()
    {
        Slots = new List<AttachmentSlot>();
    }

    public string GetEquipmentHash()
    {
        int hash = 0;
        if (Slots != null)
            foreach (var s in Slots)
                hash ^= s.ObjectPath?.GetHashCode() ?? 0;
        return hash.ToString("X8");
    }
}

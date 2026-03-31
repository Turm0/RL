using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.ECS.Components;

public struct Appearance
{
    public Color SkinBase;
    public Color SkinShadow;
    public Color SkinHighlight;
    public Color EyeColor;
    public Color OutlineColor;
    public List<AttachmentSlot> Attachments;

    public Appearance(Color skinBase, Color skinShadow, Color skinHighlight,
        Color eyeColor, Color outlineColor = default)
    {
        SkinBase = skinBase;
        SkinShadow = skinShadow;
        SkinHighlight = skinHighlight;
        EyeColor = eyeColor;
        OutlineColor = outlineColor == default ? new Color(26, 26, 42) : outlineColor;
        Attachments = new List<AttachmentSlot>();
    }

    /// <summary>
    /// Generates a hash string for cache key purposes.
    /// </summary>
    public string GetAppearanceHash()
    {
        int hash = SkinBase.GetHashCode() ^ SkinShadow.GetHashCode() ^
                   SkinHighlight.GetHashCode() ^ EyeColor.GetHashCode();
        if (Attachments != null)
            foreach (var a in Attachments)
                hash ^= a.ObjectPath?.GetHashCode() ?? 0;
        return hash.ToString("X8");
    }
}

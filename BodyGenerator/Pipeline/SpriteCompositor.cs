using System.Collections.Generic;
using Microsoft.Xna.Framework;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public static class SpriteCompositor
{
    public static void Composite(
        PixelBuffer buffer,
        BodyTemplate body,
        PoseDefinition pose,
        Dictionary<string, Point> partPositions,
        Dictionary<ColorRole, Color> palette)
    {
        for (int i = 0; i < pose.DrawOrder.Count; i++)
        {
            string partName = pose.DrawOrder[i];
            if (!body.Parts.TryGetValue(partName, out var part)) continue;
            if (!partPositions.TryGetValue(partName, out var pos)) continue;

            int z = i;
            foreach (var pixel in part.Pixels)
            {
                if (palette.TryGetValue(pixel.Role, out var color))
                {
                    buffer.SetPixel(pos.X + pixel.X, pos.Y + pixel.Y, color, z);
                }
            }
        }
    }
}

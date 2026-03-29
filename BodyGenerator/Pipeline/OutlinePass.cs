using System.Collections.Generic;
using Microsoft.Xna.Framework;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public static class OutlinePass
{
    private static readonly (int dx, int dy)[] Neighbors = { (0, -1), (0, 1), (-1, 0), (1, 0) };

    public static void Apply(PixelBuffer buffer, Color outlineColor)
    {
        var outlinePositions = new HashSet<(int x, int y)>();

        for (int y = 0; y < buffer.Height; y++)
        {
            for (int x = 0; x < buffer.Width; x++)
            {
                if (buffer.GetPixel(x, y) == Color.Transparent) continue;

                foreach (var (dx, dy) in Neighbors)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx < 0 || nx >= buffer.Width || ny < 0 || ny >= buffer.Height)
                        continue; // out of bounds neighbor — mark nothing (edge of buffer)

                    if (buffer.GetPixel(nx, ny) == Color.Transparent)
                    {
                        outlinePositions.Add((nx, ny));
                    }
                }
            }
        }

        foreach (var (x, y) in outlinePositions)
        {
            if (buffer.GetPixel(x, y) == Color.Transparent)
            {
                buffer.SetPixel(x, y, outlineColor, 999);
            }
        }
    }
}

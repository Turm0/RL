using System;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Rendering;

public static class PixelUtil
{
    /// <summary>
    /// Pixelizes a pixel array by averaging NxN blocks and filling them back.
    /// blockSize=1 is a no-op, 2 gives chunky 2x2 pixels, etc.
    /// </summary>
    public static void Pixelize(Color[] pixels, int size, int blockSize)
    {
        if (blockSize <= 1) return;

        for (int by = 0; by < size; by += blockSize)
        {
            for (int bx = 0; bx < size; bx += blockSize)
            {
                int r = 0, g = 0, b = 0, a = 0, count = 0;
                int maxY = Math.Min(by + blockSize, size);
                int maxX = Math.Min(bx + blockSize, size);

                for (int py = by; py < maxY; py++)
                    for (int px = bx; px < maxX; px++)
                    {
                        var c = pixels[py * size + px];
                        r += c.R; g += c.G; b += c.B; a += c.A;
                        count++;
                    }

                var avg = new Color(r / count, g / count, b / count, a / count);

                for (int py = by; py < maxY; py++)
                    for (int px = bx; px < maxX; px++)
                        pixels[py * size + px] = avg;
            }
        }
    }
}

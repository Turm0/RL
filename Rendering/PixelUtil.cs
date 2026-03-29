using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RoguelikeEngine.Rendering;

public static class PixelUtil
{
    /// <summary>
    /// Pixelizes an existing Texture2D in-place.
    /// </summary>
    public static void PixelizeTexture(Texture2D texture, int blockSize)
    {
        if (blockSize <= 1) return;

        int w = texture.Width, h = texture.Height;
        var pixels = new Color[w * h];
        texture.GetData(pixels);
        Pixelize(pixels, w, h, blockSize);
        texture.SetData(pixels);
    }
    /// <summary>
    /// Pixelizes a pixel array by averaging NxN blocks and filling them back.
    /// blockSize=1 is a no-op, 2 gives chunky 2x2 pixels, etc.
    /// </summary>
    public static void Pixelize(Color[] pixels, int width, int height, int blockSize)
    {
        if (blockSize <= 1) return;

        for (int by = 0; by < height; by += blockSize)
        {
            for (int bx = 0; bx < width; bx += blockSize)
            {
                int r = 0, g = 0, b = 0, a = 0, count = 0;
                int maxY = Math.Min(by + blockSize, height);
                int maxX = Math.Min(bx + blockSize, width);

                for (int py = by; py < maxY; py++)
                    for (int px = bx; px < maxX; px++)
                    {
                        var c = pixels[py * width + px];
                        r += c.R; g += c.G; b += c.B; a += c.A;
                        count++;
                    }

                var avg = new Color(r / count, g / count, b / count, a / count);

                for (int py = by; py < maxY; py++)
                    for (int px = bx; px < maxX; px++)
                        pixels[py * width + px] = avg;
            }
        }
    }

    /// <summary>Convenience overload for square textures.</summary>
    public static void Pixelize(Color[] pixels, int size, int blockSize)
        => Pixelize(pixels, size, size, blockSize);
}

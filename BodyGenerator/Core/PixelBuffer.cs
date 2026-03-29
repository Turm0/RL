using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BodyGenerator.Core;

public class PixelBuffer
{
    public int Width;
    public int Height;
    public Color[] Pixels;
    public int[] ZBuffer;

    public PixelBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new Color[width * height];
        ZBuffer = new int[width * height];
        Clear();
    }

    public void SetPixel(int x, int y, Color color, int z)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        int idx = y * Width + x;
        if (ZBuffer[idx] <= z)
        {
            Pixels[idx] = color;
            ZBuffer[idx] = z;
        }
    }

    public Color GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return Color.Transparent;
        return Pixels[y * Width + x];
    }

    public void Clear()
    {
        Array.Fill(Pixels, Color.Transparent);
        Array.Fill(ZBuffer, -1);
    }

    public Texture2D ToTexture2D(GraphicsDevice device)
    {
        var tex = new Texture2D(device, Width, Height);
        tex.SetData(Pixels);
        return tex;
    }
}

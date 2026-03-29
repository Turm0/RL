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

    public void CenterContent()
    {
        int minX = Width, minY = Height, maxX = -1, maxY = -1;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (Pixels[y * Width + x] != Color.Transparent)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }

        if (maxX < 0) return; // empty buffer

        int contentW = maxX - minX + 1;
        int contentH = maxY - minY + 1;
        int newX = (Width - contentW) / 2;
        int newY = (Height - contentH) / 2;
        int dx = newX - minX;
        int dy = newY - minY;

        if (dx == 0 && dy == 0) return;

        var newPixels = new Color[Width * Height];
        Array.Fill(newPixels, Color.Transparent);

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var c = Pixels[y * Width + x];
                if (c != Color.Transparent)
                    newPixels[(y + dy) * Width + (x + dx)] = c;
            }

        Pixels = newPixels;
    }

    public Texture2D ToTexture2D(GraphicsDevice device)
    {
        var tex = new Texture2D(device, Width, Height);
        tex.SetData(Pixels);
        return tex;
    }
}

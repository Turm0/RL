using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public static class ObjectShapeRenderer
{
    public static void RenderShape(PixelBuffer buffer, ShapeDef shape, MaterialDef material, int zOrder)
    {
        var color = new Color(material.Color.R, material.Color.G, material.Color.B, material.Alpha);

        switch (shape.Type)
        {
            case "rect":
                RenderRect(buffer, shape.X, shape.Y, shape.Width, shape.Height, color, shape.Filled, zOrder);
                break;
            case "oval":
                RenderOval(buffer, shape.CenterX, shape.CenterY, shape.RadiusX, shape.RadiusY, color, shape.Filled, zOrder);
                break;
            case "line":
                RenderLine(buffer, shape.FromX, shape.FromY, shape.ToX, shape.ToY, shape.LineWidth, color, zOrder);
                break;
            case "flame":
                RenderFlame(buffer, shape.CenterX, shape.CenterY, shape.Width, shape.Height, color, zOrder);
                break;
            case "polygon":
                if (shape.Points != null && shape.Points.Count >= 3)
                    RenderPolygon(buffer, shape.Points, color, zOrder);
                break;
        }
    }

    private static void RenderRect(PixelBuffer buffer, int x, int y, int w, int h,
        Color color, bool filled, int z)
    {
        if (filled)
        {
            for (int py = y; py < y + h; py++)
                for (int px = x; px < x + w; px++)
                    buffer.SetPixel(px, py, color, z);
        }
        else
        {
            for (int px = x; px < x + w; px++)
            {
                buffer.SetPixel(px, y, color, z);
                buffer.SetPixel(px, y + h - 1, color, z);
            }
            for (int py = y; py < y + h; py++)
            {
                buffer.SetPixel(x, py, color, z);
                buffer.SetPixel(x + w - 1, py, color, z);
            }
        }
    }

    private static void RenderOval(PixelBuffer buffer, int cx, int cy, int rx, int ry,
        Color color, bool filled, int z)
    {
        if (rx <= 0 || ry <= 0) return;

        // Midpoint ellipse algorithm
        int rxSq = rx * rx, rySq = ry * ry;
        int x = 0, y = ry;
        int px = 0, py = 2 * rxSq * y;

        if (filled)
            HSpan(buffer, cx - rx, cx + rx, cy, color, z);
        else
            Plot4(buffer, cx, cy, 0, ry, color, z);

        int d1 = rySq - rxSq * ry + rxSq / 4;
        while (px < py)
        {
            x++; px += 2 * rySq;
            if (d1 < 0) d1 += rySq * (2 * x + 1);
            else { y--; py -= 2 * rxSq; d1 += rySq * (2 * x + 1) - 2 * rxSq * y; }

            if (filled)
            {
                HSpan(buffer, cx - x, cx + x, cy + y, color, z);
                HSpan(buffer, cx - x, cx + x, cy - y, color, z);
            }
            else
                Plot4(buffer, cx, cy, x, y, color, z);
        }

        int d2 = rySq * (x * x + x) + rxSq * (y * y - 2 * y + 1) - rxSq * rySq;
        while (y > 0)
        {
            y--; py -= 2 * rxSq;
            if (d2 > 0) d2 += rxSq * (1 - 2 * y);
            else { x++; px += 2 * rySq; d2 += 2 * rySq * x + rxSq * (1 - 2 * y); }

            if (filled)
            {
                HSpan(buffer, cx - x, cx + x, cy + y, color, z);
                HSpan(buffer, cx - x, cx + x, cy - y, color, z);
            }
            else
                Plot4(buffer, cx, cy, x, y, color, z);
        }
    }

    private static void Plot4(PixelBuffer buffer, int cx, int cy, int x, int y, Color color, int z)
    {
        buffer.SetPixel(cx + x, cy + y, color, z);
        buffer.SetPixel(cx - x, cy + y, color, z);
        buffer.SetPixel(cx + x, cy - y, color, z);
        buffer.SetPixel(cx - x, cy - y, color, z);
    }

    private static void HSpan(PixelBuffer buffer, int x0, int x1, int y, Color color, int z)
    {
        for (int x = x0; x <= x1; x++)
            buffer.SetPixel(x, y, color, z);
    }

    private static void RenderLine(PixelBuffer buffer, int x0, int y0, int x1, int y1,
        int width, Color color, int z)
    {
        // Bresenham's with width
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        int hw = width / 2;

        while (true)
        {
            // Draw a block around the current point for line width
            for (int wy = -hw; wy <= hw; wy++)
                for (int wx = -hw; wx <= hw; wx++)
                    buffer.SetPixel(x0 + wx, y0 + wy, color, z);

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private static void RenderFlame(PixelBuffer buffer, int cx, int cy, int width, int height,
        Color color, int z)
    {
        // Pointed flame: narrow top, wide middle, narrowing bottom
        int halfW = width / 2;
        int top = cy - height / 2;
        int bottom = cy + height / 2;

        for (int py = top; py <= bottom; py++)
        {
            float t = (float)(py - top) / (bottom - top); // 0=top, 1=bottom

            // Width profile: 0 at top, max at 60%, narrows to 40% at bottom
            float widthT;
            if (t < 0.6f)
                widthT = t / 0.6f; // 0 → 1
            else
                widthT = 1f - (t - 0.6f) / 0.4f * 0.6f; // 1 → 0.4

            int rowHalfW = (int)(halfW * widthT);
            for (int px = cx - rowHalfW; px <= cx + rowHalfW; px++)
                buffer.SetPixel(px, py, color, z);
        }
    }

    private static void RenderPolygon(PixelBuffer buffer, List<Point> points, Color color, int z)
    {
        // Scanline fill
        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (var p in points)
        {
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        for (int y = minY; y <= maxY; y++)
        {
            var intersections = new List<int>();
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % n];
                if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
                {
                    int x = a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                    intersections.Add(x);
                }
            }
            intersections.Sort();

            for (int i = 0; i + 1 < intersections.Count; i += 2)
                for (int x = intersections[i]; x <= intersections[i + 1]; x++)
                    buffer.SetPixel(x, y, color, z);
        }
    }
}

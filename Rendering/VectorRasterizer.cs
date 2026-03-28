using System;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Renders vector creature shapes onto ImageSharp images and converts them to MonoGame Texture2D.
/// </summary>
public class VectorRasterizer
{
    /// <summary>
    /// Rasterizes a creature type into a Texture2D.
    /// </summary>
    public Texture2D RasterizeCreature(string creatureType, float size, int tileSize, GraphicsDevice device)
    {
        int texSize = (int)(tileSize * size * 3);
        if (texSize < 4) texSize = 4;

        using var image = new Image<Rgba32>(texSize, texSize, new Rgba32(0, 0, 0, 0));
        float scale = (tileSize / 24f) * size;
        float cx = texSize / 2f;
        float cy = texSize / 2f;

        switch (creatureType)
        {
            case "player":
                DrawPlayer(image, cx, cy, scale);
                break;
            case "goblin":
                DrawGoblin(image, cx, cy, scale);
                break;
            case "rat":
                DrawRat(image, cx, cy, scale);
                break;
            case "skeleton":
                DrawSkeleton(image, cx, cy, scale);
                break;
            case "dragon":
                DrawDragon(image, cx, cy, scale);
                break;
            case "ghost":
                DrawGhost(image, cx, cy, scale);
                break;
            case "fire_elemental":
                DrawFireElemental(image, cx, cy, scale);
                break;
            case "torch":
                DrawTorch(image, cx, cy, scale);
                break;
        }

        return ImageToTexture(image, device);
    }

    private static void DrawPlayer(Image<Rgba32> img, float cx, float cy, float s)
    {
        img.Mutate(ctx =>
        {
            // Cloak: trapezoid from shoulders down
            var cloak = new Polygon(new PointF[]
            {
                new(cx - 6 * s, cy - 2 * s),
                new(cx + 6 * s, cy - 2 * s),
                new(cx + 8 * s, cy + 10 * s),
                new(cx - 8 * s, cy + 10 * s)
            });
            ctx.Fill(Color.FromRgba(46, 125, 50, 255), cloak);

            // Head: circle above body
            var head = new EllipsePolygon(cx, cy - 6 * s, 4.5f * s);
            ctx.Fill(Color.FromRgba(165, 214, 167, 255), head);

            // Hood: upper half of head (semicircle approximation)
            var hood = new Polygon(new PointF[]
            {
                new(cx - 4.5f * s, cy - 6 * s),
                new(cx - 4.2f * s, cy - 8.5f * s),
                new(cx - 3f * s, cy - 9.8f * s),
                new(cx - 1f * s, cy - 10.3f * s),
                new(cx + 1f * s, cy - 10.3f * s),
                new(cx + 3f * s, cy - 9.8f * s),
                new(cx + 4.2f * s, cy - 8.5f * s),
                new(cx + 4.5f * s, cy - 6 * s)
            });
            ctx.Fill(Color.FromRgba(27, 94, 32, 255), hood);

            // Eyes
            ctx.Fill(Color.FromRgba(232, 245, 233, 255), new EllipsePolygon(cx - 2 * s, cy - 5.5f * s, 1f * s));
            ctx.Fill(Color.FromRgba(232, 245, 233, 255), new EllipsePolygon(cx + 2 * s, cy - 5.5f * s, 1f * s));

            // Sword: line from right side going up-right
            ctx.DrawLine(Color.FromRgba(176, 190, 197, 255), 2f * s,
                new PointF[] { new(cx + 7 * s, cy - 1 * s), new(cx + 11 * s, cy - 8 * s) });

            // Sword pommel
            ctx.Fill(Color.FromRgba(255, 213, 79, 255), new EllipsePolygon(cx + 7 * s, cy - 1 * s, 1.2f * s));
        });
    }

    private static void DrawGoblin(Image<Rgba32> img, float cx, float cy, float s)
    {
        img.Mutate(ctx =>
        {
            var green = Color.FromRgba(124, 179, 66, 255);
            var darkGreen = Color.FromRgba(85, 139, 47, 255);

            // Body: ellipse centered slightly below middle
            ctx.Fill(green, new EllipsePolygon(cx, cy + 2 * s, 6 * s, 8 * s));

            // Head
            ctx.Fill(green, new EllipsePolygon(cx, cy - 7 * s, 5 * s));

            // Ears: pointy triangles
            var leftEar = new Polygon(new PointF[]
            {
                new(cx - 4 * s, cy - 9 * s),
                new(cx - 8 * s, cy - 14 * s),
                new(cx - 2 * s, cy - 7 * s)
            });
            var rightEar = new Polygon(new PointF[]
            {
                new(cx + 4 * s, cy - 9 * s),
                new(cx + 8 * s, cy - 14 * s),
                new(cx + 2 * s, cy - 7 * s)
            });
            ctx.Fill(darkGreen, leftEar);
            ctx.Fill(darkGreen, rightEar);

            // Eyes: yellow
            ctx.Fill(Color.FromRgba(255, 235, 59, 255), new EllipsePolygon(cx - 2 * s, cy - 7.5f * s, 1.2f * s));
            ctx.Fill(Color.FromRgba(255, 235, 59, 255), new EllipsePolygon(cx + 2 * s, cy - 7.5f * s, 1.2f * s));

            // Arms: lines extending outward and down
            ctx.DrawLine(green, 2f * s,
                new PointF[] { new(cx - 6 * s, cy), new(cx - 10 * s, cy + 6 * s) });
            ctx.DrawLine(green, 2f * s,
                new PointF[] { new(cx + 6 * s, cy), new(cx + 10 * s, cy + 6 * s) });
        });
    }

    private static void DrawRat(Image<Rgba32> img, float cx, float cy, float s)
    {
        img.Mutate(ctx =>
        {
            var brown = Color.FromRgba(121, 85, 72, 255);
            var lightBrown = Color.FromRgba(161, 136, 127, 255);

            // Tail: curving backward (draw first, behind body)
            ctx.DrawLine(lightBrown, MathF.Max(2f * s, 1.5f),
                new PointF[]
                {
                    new(cx - 14 * s, cy),
                    new(cx - 18 * s, cy - 4 * s),
                    new(cx - 20 * s, cy - 10 * s)
                });

            // Body: horizontal ellipse (larger base coords)
            ctx.Fill(brown, new EllipsePolygon(cx, cy, 14 * s, 8 * s));

            // Head: at front
            ctx.Fill(brown, new EllipsePolygon(cx + 12 * s, cy - 2 * s, 7 * s, 6 * s));

            // Ears
            ctx.Fill(lightBrown, new EllipsePolygon(cx + 14 * s, cy - 7 * s, 3 * s));
            ctx.Fill(lightBrown, new EllipsePolygon(cx + 10 * s, cy - 7 * s, 3 * s));

            // Nose
            ctx.Fill(Color.FromRgba(90, 60, 50, 255), new EllipsePolygon(cx + 17 * s, cy - 2 * s, 2 * s, 1.5f * s));

            // Eye
            ctx.Fill(Color.FromRgba(255, 87, 34, 255), new EllipsePolygon(cx + 14 * s, cy - 3 * s, MathF.Max(1.8f * s, 1.2f)));
        });
    }

    private static void DrawSkeleton(Image<Rgba32> img, float cx, float cy, float s)
    {
        img.Mutate(ctx =>
        {
            var bone = Color.FromRgba(224, 224, 224, 255);
            float lw = MathF.Max(2.5f * s, 1.5f); // minimum line width so it's visible

            // Legs (draw first, behind body)
            ctx.DrawLine(bone, lw,
                new PointF[] { new(cx - 1 * s, cy + 10 * s), new(cx - 5 * s, cy + 16 * s) });
            ctx.DrawLine(bone, lw,
                new PointF[] { new(cx + 1 * s, cy + 10 * s), new(cx + 5 * s, cy + 16 * s) });

            // Pelvis
            ctx.Fill(bone, new EllipsePolygon(cx, cy + 10 * s, 3 * s, 2 * s));

            // Spine: vertical line
            ctx.DrawLine(bone, lw,
                new PointF[] { new(cx, cy - 3 * s), new(cx, cy + 10 * s) });

            // Ribcage: filled shape for visibility
            var ribcage = new Polygon(new PointF[]
            {
                new(cx - 5 * s, cy + 1 * s),
                new(cx - 6 * s, cy - 2 * s),
                new(cx - 4 * s, cy - 4 * s),
                new(cx, cy - 3 * s),
                new(cx + 4 * s, cy - 4 * s),
                new(cx + 6 * s, cy - 2 * s),
                new(cx + 5 * s, cy + 1 * s),
                new(cx, cy + 3 * s)
            });
            ctx.Fill(Color.FromRgba(200, 200, 200, 180), ribcage);

            // Rib lines across chest
            for (int i = 0; i < 3; i++)
            {
                float ry = cy - 2 * s + i * 2.5f * s;
                float ribW = (5.5f - i * 0.8f) * s;
                ctx.DrawLine(bone, lw,
                    new PointF[]
                    {
                        new(cx - ribW, ry + 1 * s),
                        new(cx, ry),
                        new(cx + ribW, ry + 1 * s)
                    });
            }

            // Arms
            ctx.DrawLine(bone, lw,
                new PointF[] { new(cx, cy - 2 * s), new(cx - 8 * s, cy + 4 * s) });
            ctx.DrawLine(bone, lw,
                new PointF[] { new(cx, cy - 2 * s), new(cx + 8 * s, cy + 4 * s) });

            // Skull: larger
            ctx.Fill(bone, new EllipsePolygon(cx, cy - 7.5f * s, 5 * s, 5.5f * s));

            // Jaw
            ctx.Fill(Color.FromRgba(200, 200, 200, 255), new EllipsePolygon(cx, cy - 4.5f * s, 3.5f * s, 2 * s));

            // Eye sockets
            ctx.Fill(Color.FromRgba(33, 33, 33, 255), new EllipsePolygon(cx - 2 * s, cy - 8 * s, 1.8f * s, 2 * s));
            ctx.Fill(Color.FromRgba(33, 33, 33, 255), new EllipsePolygon(cx + 2 * s, cy - 8 * s, 1.8f * s, 2 * s));

            // Eye glow
            ctx.Fill(Color.FromRgba(244, 67, 54, 255), new EllipsePolygon(cx - 2 * s, cy - 8 * s, 0.9f * s));
            ctx.Fill(Color.FromRgba(244, 67, 54, 255), new EllipsePolygon(cx + 2 * s, cy - 8 * s, 0.9f * s));
        });
    }

    private static void DrawDragon(Image<Rgba32> img, float cx, float cy, float s)
    {
        img.Mutate(ctx =>
        {
            var red = Color.FromRgba(211, 47, 47, 255);
            var darkRed = Color.FromRgba(183, 28, 28, 255);

            // Wings: bat-like with multiple points
            var leftWing = new Polygon(new PointF[]
            {
                new(cx - 4 * s, cy - 4 * s),
                new(cx - 10 * s, cy - 14 * s),
                new(cx - 14 * s, cy - 8 * s),
                new(cx - 17 * s, cy - 12 * s),
                new(cx - 16 * s, cy - 2 * s),
                new(cx - 12 * s, cy + 3 * s),
                new(cx - 5 * s, cy + 1 * s)
            });
            var rightWing = new Polygon(new PointF[]
            {
                new(cx + 4 * s, cy - 4 * s),
                new(cx + 10 * s, cy - 14 * s),
                new(cx + 14 * s, cy - 8 * s),
                new(cx + 17 * s, cy - 12 * s),
                new(cx + 16 * s, cy - 2 * s),
                new(cx + 12 * s, cy + 3 * s),
                new(cx + 5 * s, cy + 1 * s)
            });
            ctx.Fill(Color.FromRgba(140, 20, 20, 160), leftWing);
            ctx.Fill(Color.FromRgba(140, 20, 20, 160), rightWing);

            // Tail: thick curving line (behind body)
            ctx.DrawLine(darkRed, 3f * s,
                new PointF[]
                {
                    new(cx + 2 * s, cy + 8 * s),
                    new(cx + 7 * s, cy + 14 * s),
                    new(cx + 13 * s, cy + 12 * s),
                    new(cx + 15 * s, cy + 9 * s)
                });

            // Body: large oval
            ctx.Fill(red, new EllipsePolygon(cx, cy + 1 * s, 7 * s, 9 * s));

            // Belly scales: slightly lighter
            ctx.Fill(Color.FromRgba(229, 80, 80, 255), new EllipsePolygon(cx, cy + 2 * s, 4 * s, 6 * s));

            // Neck
            ctx.Fill(red, new EllipsePolygon(cx, cy - 7 * s, 3.5f * s, 4 * s));

            // Head: wider than tall
            ctx.Fill(red, new EllipsePolygon(cx, cy - 10 * s, 5.5f * s, 4.5f * s));

            // Horns
            var leftHorn = new Polygon(new PointF[]
            {
                new(cx - 3 * s, cy - 13 * s),
                new(cx - 5 * s, cy - 17 * s),
                new(cx - 1.5f * s, cy - 13 * s)
            });
            var rightHorn = new Polygon(new PointF[]
            {
                new(cx + 3 * s, cy - 13 * s),
                new(cx + 5 * s, cy - 17 * s),
                new(cx + 1.5f * s, cy - 13 * s)
            });
            ctx.Fill(darkRed, leftHorn);
            ctx.Fill(darkRed, rightHorn);

            // Snout
            ctx.Fill(red, new EllipsePolygon(cx, cy - 12.5f * s, 3 * s, 2 * s));

            // Nostrils
            ctx.Fill(darkRed, new EllipsePolygon(cx - 1.2f * s, cy - 13 * s, 0.7f * s));
            ctx.Fill(darkRed, new EllipsePolygon(cx + 1.2f * s, cy - 13 * s, 0.7f * s));

            // Eyes: orange with slit pupils
            ctx.Fill(Color.FromRgba(255, 152, 0, 255), new EllipsePolygon(cx - 2.5f * s, cy - 10 * s, 1.8f * s, 1.5f * s));
            ctx.Fill(Color.FromRgba(255, 152, 0, 255), new EllipsePolygon(cx + 2.5f * s, cy - 10 * s, 1.8f * s, 1.5f * s));
            ctx.Fill(Color.FromRgba(33, 33, 33, 255), new EllipsePolygon(cx - 2.5f * s, cy - 10 * s, 0.6f * s, 1.3f * s));
            ctx.Fill(Color.FromRgba(33, 33, 33, 255), new EllipsePolygon(cx + 2.5f * s, cy - 10 * s, 0.6f * s, 1.3f * s));
        });
    }

    private static void DrawGhost(Image<Rgba32> img, float cx, float cy, float s)
    {
        img.Mutate(ctx =>
        {
            // Body: rounded top with wavy bottom edge
            float top = cy - 10 * s;
            float bottom = cy + 10 * s;
            float left = cx - 7 * s;
            float right = cx + 7 * s;
            float waveAmp = 3 * s;

            var bodyPoints = new PointF[]
            {
                // Top dome (smoother arc)
                new(left, cy),
                new(left + 0.5f * s, cy - 4 * s),
                new(left + 2 * s, cy - 7 * s),
                new(left + 4 * s, cy - 9 * s),
                new(cx, top),
                new(right - 4 * s, cy - 9 * s),
                new(right - 2 * s, cy - 7 * s),
                new(right - 0.5f * s, cy - 4 * s),
                new(right, cy),
                // Right side straight down
                new(right, bottom - waveAmp),
                // Wavy bottom: 4 "tentacles"
                new(right - 1.5f * s, bottom),
                new(right - 3.5f * s, bottom - waveAmp),
                new(cx + 1.5f * s, bottom),
                new(cx, bottom - waveAmp * 0.7f),
                new(cx - 1.5f * s, bottom),
                new(left + 3.5f * s, bottom - waveAmp),
                new(left + 1.5f * s, bottom),
                // Left side up
                new(left, bottom - waveAmp)
            };

            // Outer glow/body - more visible
            ctx.Fill(Color.FromRgba(200, 220, 255, 160), new Polygon(bodyPoints));

            // Inner brighter core
            var innerPoints = new PointF[]
            {
                new(left + 2 * s, cy),
                new(left + 3 * s, cy - 4 * s),
                new(cx, top + 2 * s),
                new(right - 3 * s, cy - 4 * s),
                new(right - 2 * s, cy),
                new(right - 2 * s, bottom - waveAmp - 1 * s),
                new(cx, bottom - waveAmp),
                new(left + 2 * s, bottom - waveAmp - 1 * s)
            };
            ctx.Fill(Color.FromRgba(220, 235, 255, 100), new Polygon(innerPoints));

            // Eyes: large light blue ellipses
            ctx.Fill(Color.FromRgba(179, 229, 252, 255), new EllipsePolygon(cx - 3 * s, cy - 3 * s, 2.5f * s, 3 * s));
            ctx.Fill(Color.FromRgba(179, 229, 252, 255), new EllipsePolygon(cx + 3 * s, cy - 3 * s, 2.5f * s, 3 * s));

            // Pupils: dark navy
            ctx.Fill(Color.FromRgba(26, 35, 126, 255), new EllipsePolygon(cx - 3 * s, cy - 2.5f * s, 1.2f * s));
            ctx.Fill(Color.FromRgba(26, 35, 126, 255), new EllipsePolygon(cx + 3 * s, cy - 2.5f * s, 1.2f * s));

            // Mouth: small dark oval
            ctx.Fill(Color.FromRgba(100, 120, 160, 200), new EllipsePolygon(cx, cy + 2 * s, 2 * s, 1.2f * s));
        });
    }

    private static void DrawFireElemental(Image<Rgba32> img, float cx, float cy, float s)
    {
        img.Mutate(ctx =>
        {
            // Layered flame shapes: 4 overlapping pointed ovals, each smaller and lighter
            DrawFlameLayer(ctx, cx, cy, s, 10, 14, Color.FromRgba(230, 81, 0, 180));
            DrawFlameLayer(ctx, cx, cy, s, 8, 11, Color.FromRgba(255, 152, 0, 200));
            DrawFlameLayer(ctx, cx, cy, s, 6, 8, Color.FromRgba(255, 213, 79, 210));
            DrawFlameLayer(ctx, cx, cy, s, 4, 5, Color.FromRgba(255, 249, 196, 220));

            // Eyes: pale yellow
            ctx.Fill(Color.FromRgba(255, 249, 196, 255), new EllipsePolygon(cx - 2 * s, cy - 1 * s, 1.2f * s));
            ctx.Fill(Color.FromRgba(255, 249, 196, 255), new EllipsePolygon(cx + 2 * s, cy - 1 * s, 1.2f * s));
        });
    }

    private static void DrawFlameLayer(IImageProcessingContext ctx, float cx, float cy, float s,
        float radiusX, float radiusY, Color color)
    {
        // Pointed flame shape: diamond-like with wide middle and pointed top
        var flame = new Polygon(new PointF[]
        {
            new(cx, cy - radiusY * s),       // top point
            new(cx + radiusX * 0.6f * s, cy - radiusY * 0.3f * s),
            new(cx + radiusX * s, cy + 2 * s),  // widest right
            new(cx + radiusX * 0.4f * s, cy + radiusY * 0.7f * s),
            new(cx, cy + radiusY * 0.5f * s),   // bottom center
            new(cx - radiusX * 0.4f * s, cy + radiusY * 0.7f * s),
            new(cx - radiusX * s, cy + 2 * s),  // widest left
            new(cx - radiusX * 0.6f * s, cy - radiusY * 0.3f * s)
        });
        ctx.Fill(color, flame);
    }

    private static void DrawTorch(Image<Rgba32> img, float cx, float cy, float s)
    {
        img.Mutate(ctx =>
        {
            // Stick
            ctx.DrawLine(Color.FromRgba(100, 70, 40, 255), MathF.Max(2.5f * s, 1.5f),
                new PointF[] { new(cx, cy + 2 * s), new(cx, cy + 10 * s) });

            // Mount bracket
            ctx.Fill(Color.FromRgba(80, 80, 80, 255), new EllipsePolygon(cx, cy + 2 * s, 2.5f * s, 1.5f * s));

            // Flame outer: orange
            var flameOuter = new Polygon(new PointF[]
            {
                new(cx, cy - 8 * s),
                new(cx + 3 * s, cy - 3 * s),
                new(cx + 2.5f * s, cy + 1 * s),
                new(cx, cy + 2 * s),
                new(cx - 2.5f * s, cy + 1 * s),
                new(cx - 3 * s, cy - 3 * s)
            });
            ctx.Fill(Color.FromRgba(255, 140, 0, 220), flameOuter);

            // Flame inner: yellow
            var flameInner = new Polygon(new PointF[]
            {
                new(cx, cy - 5 * s),
                new(cx + 1.5f * s, cy - 2 * s),
                new(cx + 1.2f * s, cy + 0.5f * s),
                new(cx, cy + 1.5f * s),
                new(cx - 1.2f * s, cy + 0.5f * s),
                new(cx - 1.5f * s, cy - 2 * s)
            });
            ctx.Fill(Color.FromRgba(255, 220, 80, 240), flameInner);

            // Core: bright white-yellow
            ctx.Fill(Color.FromRgba(255, 255, 200, 255), new EllipsePolygon(cx, cy - 1 * s, 1 * s, 1.5f * s));
        });
    }

    private static Texture2D ImageToTexture(Image<Rgba32> image, GraphicsDevice device)
    {
        int w = image.Width;
        int h = image.Height;
        var pixels = new Microsoft.Xna.Framework.Color[w * h];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    var p = row[x];
                    pixels[y * w + x] = new Microsoft.Xna.Framework.Color(p.R, p.G, p.B, p.A);
                }
            }
        });

        var tex = new Texture2D(device, w, h);
        tex.SetData(pixels);
        return tex;
    }
}

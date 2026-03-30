using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public class ObjectSpriteGenerator
{
    private readonly string _contentRoot;
    private readonly Dictionary<string, ObjectDefinition> _definitionCache = new();

    public ObjectSpriteGenerator(string contentRoot)
    {
        _contentRoot = contentRoot;
    }

    public ObjectDefinition GetDefinition(string objectPath)
    {
        if (_definitionCache.TryGetValue(objectPath, out var cached))
            return cached;

        string fullPath = Path.Combine(_contentRoot, objectPath);
        var def = ObjectYamlLoader.Load(fullPath);
        _definitionCache[objectPath] = def;
        return def;
    }

    public Texture2D Generate(GraphicsDevice device, string objectPath)
    {
        return GenerateFrame(device, objectPath, 0);
    }

    public Texture2D GenerateFrame(GraphicsDevice device, string objectPath, int frame)
    {
        var def = GetDefinition(objectPath);
        int frameIdx = frame % def.Frames.Count;
        var shapes = def.Frames[frameIdx];

        int size = def.SpriteSize;
        var buffer = new PixelBuffer(size, size);

        // Render shapes in order (z increases with each shape)
        for (int i = 0; i < shapes.Count; i++)
        {
            var shape = shapes[i];
            if (shape.Material != null && def.Materials.TryGetValue(shape.Material, out var material))
            {
                ObjectShapeRenderer.RenderShape(buffer, shape, material, i);
            }
        }

        // Apply outline
        var outlineColor = new Color(20, 20, 30);
        if (def.Materials.TryGetValue("outline", out var outlineMat))
            outlineColor = outlineMat.Color;
        OutlinePass.Apply(buffer, outlineColor);

        // Pixelize (inline to avoid cross-project dependency)
        if (def.PixelSize > 1)
            Pixelize(buffer.Pixels, size, size, def.PixelSize);

        // Don't center — anchor point handles positioning
        return buffer.ToTexture2D(device);
    }

    private static void Pixelize(Color[] pixels, int width, int height, int blockSize)
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
}

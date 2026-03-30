using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Draws soft fog edges:
/// 1. Between explored and unexplored tiles (dark fog)
/// 2. Between visible and memory tiles (subtle visibility boundary)
/// Pre-renders gradient textures once at init for each edge direction.
/// </summary>
public class FogEdgeRenderer
{
    // Fog of war edges (explored → unexplored)
    private Texture2D _fogN, _fogS, _fogE, _fogW;
    private Texture2D _fogNW, _fogNE, _fogSW, _fogSE;


    private static readonly Color FogColor = new(0, 0, 0);


    public void Initialize(GraphicsDevice device)
    {
        int size = GameConfig.TileSize;

        // Fog edges (strong, matches void color)
        _fogN = CreateEdge(device, size, 0, -1, FogColor, 1.0f);
        _fogS = CreateEdge(device, size, 0, 1, FogColor, 1.0f);
        _fogW = CreateEdge(device, size, -1, 0, FogColor, 1.0f);
        _fogE = CreateEdge(device, size, 1, 0, FogColor, 1.0f);
        _fogNW = CreateCorner(device, size, -1, -1, FogColor, 1.0f);
        _fogNE = CreateCorner(device, size, 1, -1, FogColor, 1.0f);
        _fogSW = CreateCorner(device, size, -1, 1, FogColor, 1.0f);
        _fogSE = CreateCorner(device, size, 1, 1, FogColor, 1.0f);

    }

    private static Texture2D CreateEdge(GraphicsDevice device, int size, int dx, int dy,
        Color color, float strength)
    {
        var pixels = new Color[size * size];
        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float t;
                if (dy == -1) t = 1f - (float)py / size;
                else if (dy == 1) t = (float)py / size;
                else if (dx == -1) t = 1f - (float)px / size;
                else t = (float)px / size;

                t = t * t * strength;
                pixels[py * size + px] = new Color(color.R, color.G, color.B, (byte)(t * 255));
            }
        }

        var tex = new Texture2D(device, size, size);
        tex.SetData(pixels);
        return tex;
    }

    private static Texture2D CreateCorner(GraphicsDevice device, int size, int dx, int dy,
        Color color, float strength)
    {
        var pixels = new Color[size * size];
        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float fx = dx < 0 ? 1f - (float)px / size : (float)px / size;
                float fy = dy < 0 ? 1f - (float)py / size : (float)py / size;
                float t = fx * fy * strength;
                t = t * t;
                pixels[py * size + px] = new Color(color.R, color.G, color.B, (byte)(t * 255));
            }
        }

        var tex = new Texture2D(device, size, size);
        tex.SetData(pixels);
        return tex;
    }

    public void Draw(SpriteBatch spriteBatch, TileMap map, Camera camera, FogOfWar fow)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = camera.GetVisibleTileRect(tileSize);

        int startX = visibleRect.X;
        int startY = visibleRect.Y;
        int endX = startX + visibleRect.Width;
        int endY = startY + visibleRect.Height;

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if (!map.IsInBounds(x, y)) continue;

                bool visible = fow.IsVisible(x, y);
                bool explored = fow.IsExplored(x, y);
                if (!visible && !explored) continue;

                var destRect = camera.TileToScreenRect(x, y, tileSize);

                // Fog edges: explored tile next to unexplored void
                bool fogN = !IsExplored(map, fow, x, y - 1);
                bool fogS = !IsExplored(map, fow, x, y + 1);
                bool fogW = !IsExplored(map, fow, x - 1, y);
                bool fogE = !IsExplored(map, fow, x + 1, y);

                if (fogN) spriteBatch.Draw(_fogN, destRect, Color.White);
                if (fogS) spriteBatch.Draw(_fogS, destRect, Color.White);
                if (fogW) spriteBatch.Draw(_fogW, destRect, Color.White);
                if (fogE) spriteBatch.Draw(_fogE, destRect, Color.White);

                if (!fogN && !fogW && !IsExplored(map, fow, x - 1, y - 1))
                    spriteBatch.Draw(_fogNW, destRect, Color.White);
                if (!fogN && !fogE && !IsExplored(map, fow, x + 1, y - 1))
                    spriteBatch.Draw(_fogNE, destRect, Color.White);
                if (!fogS && !fogW && !IsExplored(map, fow, x - 1, y + 1))
                    spriteBatch.Draw(_fogSW, destRect, Color.White);
                if (!fogS && !fogE && !IsExplored(map, fow, x + 1, y + 1))
                    spriteBatch.Draw(_fogSE, destRect, Color.White);

            }
        }
    }

    private static bool IsExplored(TileMap map, FogOfWar fow, int x, int y)
    {
        if (!map.IsInBounds(x, y)) return false;
        return fow.IsExplored(x, y) || fow.IsVisible(x, y);
    }

}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.Data;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Draws the tile grid from a TileMap using colored rectangles.
/// </summary>
public class TerrainRenderer
{
    private static readonly Color FloorColor = new(55, 50, 44);
    private static readonly Color WallColor = new(75, 70, 62);
    private static readonly Color WaterColor = new(30, 50, 70);
    private static readonly Color LavaColor = new(120, 40, 20);
    private static readonly Color WallHighlightColor = new(95, 88, 78);

    private const int HighlightHeight = 4;

    /// <summary>
    /// Draws all visible tiles from the map.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D whitePixel, TileMap map, Camera camera)
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

                var tile = map.GetTile(x, y);
                var color = GetTileColor(tile);

                var worldPos = new Vector2(x * tileSize, y * tileSize);
                var screenPos = camera.WorldToScreen(worldPos);
                int sx = (int)Math.Floor(screenPos.X);
                int sy = (int)Math.Floor(screenPos.Y);
                // Compute next tile edge to avoid sub-pixel gaps
                int sx2 = (int)Math.Floor(screenPos.X + tileSize);
                int sy2 = (int)Math.Floor(screenPos.Y + tileSize);
                var destRect = new Rectangle(sx, sy, sx2 - sx, sy2 - sy);

                spriteBatch.Draw(whitePixel, destRect, color);

                // Wall top highlight: if this wall has floor below it
                if (tile == TileType.Wall && map.IsInBounds(x, y + 1) && map.GetTile(x, y + 1) == TileType.Floor)
                {
                    var highlightRect = new Rectangle(
                        sx,
                        sy2 - HighlightHeight,
                        sx2 - sx,
                        HighlightHeight);
                    spriteBatch.Draw(whitePixel, highlightRect, WallHighlightColor);
                }
            }
        }
    }

    private static Color GetTileColor(TileType type) => type switch
    {
        TileType.Floor => FloorColor,
        TileType.Wall => WallColor,
        TileType.Water => WaterColor,
        TileType.Lava => LavaColor,
        _ => Color.Magenta
    };
}

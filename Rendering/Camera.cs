using System;
using Microsoft.Xna.Framework;
using RoguelikeEngine.Core;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Smooth-follow camera with viewport culling.
/// Position represents the top-left corner of the viewport in world pixel space.
/// </summary>
public class Camera
{
    /// <summary>Top-left corner of the viewport in world pixel space.</summary>
    public Vector2 Position;

    /// <summary>Viewport width in pixels.</summary>
    public int ViewportWidth;

    /// <summary>Viewport height in pixels.</summary>
    public int ViewportHeight;

    /// <summary>The tile the camera should follow (typically the player position).</summary>
    public Point TargetTile;

    /// <summary>
    /// Smoothly lerps the camera position toward the target tile each frame.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        int tileSize = GameConfig.TileSize;

        var targetPixel = new Vector2(
            TargetTile.X * tileSize + tileSize / 2f,
            TargetTile.Y * tileSize + tileSize / 2f);
        var viewportCenter = new Vector2(ViewportWidth / 2f, ViewportHeight / 2f);
        var desired = targetPixel - viewportCenter;

        Position = Vector2.Lerp(Position, desired, 8f * dt);
    }

    /// <summary>Converts a world pixel position to screen position.</summary>
    public Vector2 WorldToScreen(Vector2 worldPos) => worldPos - Position;

    /// <summary>
    /// Gets the rectangle of tile coordinates currently visible, plus a 1-tile margin.
    /// </summary>
    public Rectangle GetVisibleTileRect(int tileSize)
    {
        int startX = (int)Math.Floor(Position.X / tileSize) - 1;
        int startY = (int)Math.Floor(Position.Y / tileSize) - 1;
        int endX = (int)Math.Ceiling((Position.X + ViewportWidth) / tileSize) + 1;
        int endY = (int)Math.Ceiling((Position.Y + ViewportHeight) / tileSize) + 1;

        return new Rectangle(startX, startY, endX - startX + 1, endY - startY + 1);
    }

    /// <summary>Returns true if the given tile is within the visible area.</summary>
    public bool IsInView(int tileX, int tileY, int tileSize)
    {
        var rect = GetVisibleTileRect(tileSize);
        return tileX >= rect.X && tileX < rect.X + rect.Width &&
               tileY >= rect.Y && tileY < rect.Y + rect.Height;
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.Data;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

public class RoofRenderer
{
    private readonly TextureCache _cache = new();
    private readonly TextureCache _memoryCache = new();
    private readonly FastNoiseLite _noise;
    private readonly Dictionary<ushort, float> _roofAlpha = new();
    private const float FadeSpeed = 4f;

    public RoofRenderer()
    {
        _noise = new FastNoiseLite(5813);
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    }

    public void Update(GameTime gameTime, TileMap map, ushort playerZoneId)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        foreach (var zone in map.GetAllZones())
        {
            if (!zone.HasRoof) continue;

            _roofAlpha.TryGetValue(zone.Id, out float alpha);

            float target = (zone.Id == playerZoneId) ? 0f : 1f;

            if (alpha < target)
                alpha = Math.Min(alpha + FadeSpeed * dt, target);
            else if (alpha > target)
                alpha = Math.Max(alpha - FadeSpeed * dt, target);

            _roofAlpha[zone.Id] = alpha;
        }
    }

    public void Draw(SpriteBatch spriteBatch, TileMap map, Camera camera, FogOfWar fow)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = camera.GetVisibleTileRect(tileSize);

        foreach (var zone in map.GetAllZones())
        {
            if (!zone.HasRoof) continue;
            _roofAlpha.TryGetValue(zone.Id, out float alpha);
            if (alpha <= 0.01f) continue;

            int alphaByte = (int)(alpha * 255);
            var tint = new Color(255, 255, 255, alphaByte);

            // A roof looks "normal" (not memory) if the player can see any wall of the building.
            // Since FOV can't penetrate walls, roof tiles are technically never visible,
            // but the player IS looking at the building from outside.
            bool roofSeen = IsAnyWallVisible(map, fow, zone);

            int x1 = Math.Max(zone.Bounds.X - 1, visibleRect.X);
            int y1 = Math.Max(zone.Bounds.Y - 1, visibleRect.Y);
            int x2 = Math.Min(zone.Bounds.Right + 1, visibleRect.Right);
            int y2 = Math.Min(zone.Bounds.Bottom + 1, visibleRect.Bottom);

            for (int x = x1; x < x2; x++)
            {
                for (int y = y1; y < y2; y++)
                {
                    if (!map.IsInBounds(x, y)) continue;

                    bool inZone = map.GetZoneId(x, y) == zone.Id;
                    bool isOverhang = !inZone && IsAdjacentToZone(map, x, y, zone.Id);
                    if (!inZone && !isOverhang) continue;

                    bool useNormalColor = roofSeen || fow.IsVisible(x, y);
                    DoorEdge doorEdge = inZone ? DetectDoorEdge(map, x, y, zone.Id) : DoorEdge.None;

                    var cacheKey = $"roof_{zone.RoofStyle}_{zone.RoofColor.PackedValue}_{TileMap.ComputeVariantSeed(x, y)}_{(int)doorEdge}";

                    Texture2D texture;
                    if (useNormalColor)
                    {
                        texture = _cache.GetOrCreate(cacheKey, () =>
                            GenerateRoofTile(spriteBatch.GraphicsDevice, zone, x, y, doorEdge));
                    }
                    else
                    {
                        string memKey = "mem_" + cacheKey;
                        texture = _memoryCache.GetOrCreate(memKey, () =>
                        {
                            var pixels = GenerateRoofPixels(zone, x, y, doorEdge);
                            ToMemoryColors(pixels);
                            var tex = new Texture2D(spriteBatch.GraphicsDevice, GameConfig.TileSize, GameConfig.TileSize);
                            tex.SetData(pixels);
                            return tex;
                        });
                    }

                    var destRect = camera.TileToScreenRect(x, y, tileSize);

                    spriteBatch.Draw(texture, destRect, tint);
                }
            }
        }
    }

    public bool IsHiddenByRoof(TileMap map, int tileX, int tileY, ushort playerZoneId)
    {
        ushort zoneId = map.GetZoneId(tileX, tileY);
        if (zoneId == 0) return false;
        var zone = map.GetZone(zoneId);
        if (zone == null || !zone.HasRoof) return false;
        if (zoneId == playerZoneId) return false;

        _roofAlpha.TryGetValue(zoneId, out float alpha);
        return alpha > 0.5f;
    }

    // --- Door detection ---

    [Flags]
    private enum DoorEdge
    {
        None = 0,
        North = 1,
        South = 2,
        West = 4,
        East = 8
    }

    private static DoorEdge DetectDoorEdge(TileMap map, int x, int y, ushort zoneId)
    {
        var tile = map.GetTile(x, y);
        if (tile.HasWall) return DoorEdge.None; // walls aren't doors

        DoorEdge result = DoorEdge.None;

        // A door edge exists where this zone tile has a cardinal neighbor
        // that is outside the zone and walkable (no wall) — that's the opening
        if (IsOutsideAndOpen(map, x, y - 1, zoneId)) result |= DoorEdge.North;
        if (IsOutsideAndOpen(map, x, y + 1, zoneId)) result |= DoorEdge.South;
        if (IsOutsideAndOpen(map, x - 1, y, zoneId)) result |= DoorEdge.West;
        if (IsOutsideAndOpen(map, x + 1, y, zoneId)) result |= DoorEdge.East;

        return result;
    }

    private static bool IsOutsideAndOpen(TileMap map, int x, int y, ushort zoneId)
    {
        if (!map.IsInBounds(x, y)) return false;
        var tile = map.GetTile(x, y);
        return tile.ZoneId != zoneId && !tile.HasWall;
    }

    /// <summary>
    /// Returns true if the player can currently see any wall tile adjacent to this zone.
    /// This means the building is in view and its roof should render in normal color.
    /// </summary>
    private static bool IsAnyWallVisible(TileMap map, FogOfWar fow, ZoneDefinition zone)
    {
        // Scan the 1-tile border around the zone bounds (where walls typically are)
        int x1 = zone.Bounds.X - 1;
        int y1 = zone.Bounds.Y - 1;
        int x2 = zone.Bounds.Right;
        int y2 = zone.Bounds.Bottom;

        // Top and bottom edges
        for (int x = x1; x <= x2; x++)
        {
            if (map.IsInBounds(x, y1) && map.HasWall(x, y1) && fow.IsVisible(x, y1)) return true;
            if (map.IsInBounds(x, y2) && map.HasWall(x, y2) && fow.IsVisible(x, y2)) return true;
        }
        // Left and right edges
        for (int y = y1 + 1; y < y2; y++)
        {
            if (map.IsInBounds(x1, y) && map.HasWall(x1, y) && fow.IsVisible(x1, y)) return true;
            if (map.IsInBounds(x2, y) && map.HasWall(x2, y) && fow.IsVisible(x2, y)) return true;
        }
        return false;
    }

    private static bool IsAdjacentToZone(TileMap map, int x, int y, ushort zoneId)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (map.IsInBounds(x + dx, y + dy) && map.GetZoneId(x + dx, y + dy) == zoneId)
                    return true;
            }
        return false;
    }

    // --- Texture generation ---

    private Texture2D GenerateRoofTile(GraphicsDevice device, ZoneDefinition zone,
        int worldX, int worldY, DoorEdge doorEdge)
    {
        var pixels = GenerateRoofPixels(zone, worldX, worldY, doorEdge);
        var tex = new Texture2D(device, GameConfig.TileSize, GameConfig.TileSize);
        tex.SetData(pixels);
        return tex;
    }

    private Color[] GenerateRoofPixels(ZoneDefinition zone, int worldX, int worldY, DoorEdge doorEdge)
    {
        int size = GameConfig.TileSize;
        var pixels = new Color[size * size];
        var baseColor = zone.RoofColor;

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float wpx = worldX * size + px;
                float wpy = worldY * size + py;

                float n = _noise.GetNoise(wpx * 0.3f, wpy * 0.3f) * 0.12f;

                Color c = zone.RoofStyle switch
                {
                    "thatch" => ShiftBrightness(baseColor,
                        n + MathF.Sin(wpy * 0.8f + _noise.GetNoise(wpx * 0.5f, wpy * 0.2f) * 3f) * 0.08f),
                    "stone_tiles" => (((int)wpx % 8 == 0) || ((int)wpy % 6 == 0))
                        ? ShiftBrightness(baseColor, -0.1f + n)
                        : ShiftBrightness(baseColor, n),
                    "wood_shingle" => ShiftBrightness(baseColor,
                        n + (((int)wpy % 6) < 1 ? -0.08f : (((int)wpy % 6) > 4 ? 0.04f : 0f))),
                    _ => ShiftBrightness(baseColor, n)
                };

                if (doorEdge != DoorEdge.None)
                    c = ApplyDoorIndicator(c, px, py, size, doorEdge);

                pixels[py * size + px] = c;
            }
        }

        return pixels;
    }

    private static void ToMemoryColors(Color[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            float gray = c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
            gray *= 0.3f;
            pixels[i] = new Color((int)gray, (int)gray, (int)gray, c.A);
        }
    }

    private static readonly Color DoorFrameColor = new(45, 32, 20);
    private static readonly Color DoorDarkColor = new(20, 15, 10);

    private static Color ApplyDoorIndicator(Color c, int px, int py, int size, DoorEdge edge)
    {
        // Dark rectangular opening with a visible frame on the door edge
        const int openHalf = 9;  // half-width of opening
        const int openDepth = 12; // how far the opening extends into the tile
        const int frameThick = 2; // frame border thickness
        int cx = size / 2;
        int cy = size / 2;

        if ((edge & DoorEdge.South) != 0 && CheckDoorPixel(px, py, cx, size - 1, 0, -1, openHalf, openDepth, frameThick, out var col))
            return col;
        if ((edge & DoorEdge.North) != 0 && CheckDoorPixel(px, py, cx, 0, 0, 1, openHalf, openDepth, frameThick, out col))
            return col;
        if ((edge & DoorEdge.West) != 0 && CheckDoorPixel(py, px, cy, 0, 0, 1, openHalf, openDepth, frameThick, out col))
            return col;
        if ((edge & DoorEdge.East) != 0 && CheckDoorPixel(py, px, cy, size - 1, 0, -1, openHalf, openDepth, frameThick, out col))
            return col;

        return c;
    }

    /// <summary>
    /// Checks if a pixel falls inside a door opening or its frame.
    /// Works in a rotated coordinate system: 'along' is the axis parallel to the wall,
    /// 'into' is the axis going into the tile from the edge.
    /// </summary>
    private static bool CheckDoorPixel(int px, int py, int center, int edgePos,
        int alongAxis, int intoDir, int halfWidth, int depth, int frame, out Color result)
    {
        // For south/north: along = px relative to center, into = distance from edge
        // The caller swaps px/py for east/west edges
        int along, into;

        if (intoDir > 0)
        {
            // Edge is at edgePos, going positive into the tile
            along = px - center;
            into = py - edgePos;
        }
        else
        {
            along = px - center;
            into = edgePos - py;
        }

        if (into < 0 || into >= depth) { result = default; return false; }
        if (Math.Abs(along) > halfWidth) { result = default; return false; }

        // Frame: the border pixels around the opening
        bool isFrame = into < frame ||
                       Math.Abs(along) > halfWidth - frame;

        if (isFrame)
        {
            result = DoorFrameColor;
            return true;
        }

        // Interior: dark void
        result = DoorDarkColor;
        return true;
    }

    private static Color ShiftBrightness(Color c, float shift)
    {
        return new Color(
            (byte)Math.Clamp(c.R + (int)(shift * 255), 0, 255),
            (byte)Math.Clamp(c.G + (int)(shift * 255), 0, 255),
            (byte)Math.Clamp(c.B + (int)(shift * 255), 0, 255));
    }
}

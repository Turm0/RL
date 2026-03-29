using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.Data;
using RoguelikeEngine.World;
using static RoguelikeEngine.World.TileMap;

namespace RoguelikeEngine.Rendering;

public class RoofRenderer
{
    private readonly TextureCache _cache = new();
    private readonly TextureCache _memoryCache = new();
    private readonly Dictionary<ushort, float> _roofAlpha = new();
    private const float FadeSpeed = 4f;

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

                    var cacheKey = $"roof_{(byte)zone.RoofMaterial}_{ComputeVariantSeed(x, y)}_{(int)doorEdge}";

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
        var mat = RoofMaterialRegistry.Get(zone.RoofMaterial);
        int seed = ComputeVariantSeed(worldX, worldY);

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                var c = SampleRoofPixel(mat, seed, px, py);

                if (doorEdge != DoorEdge.None)
                    c = ApplyDoorIndicator(c, px, py, size, doorEdge);

                pixels[py * size + px] = c;
            }
        }

        PixelUtil.Pixelize(pixels, size, mat.PixelSize);

        return pixels;
    }

    private static Color SampleRoofPixel(RoofMaterial mat, int seed, int px, int py)
    {
        int rowH = mat.RowHeight;
        int pieceW = mat.PieceWidth;

        int row = py / rowH;
        int localY = py % rowH;

        int staggerOffset = (mat.Staggered && (row & 1) != 0) ? pieceW / 2 : 0;
        int seedShift = (seed & 0xF) * 3;
        int shiftedX = px + staggerOffset + seedShift;

        int localX = shiftedX % pieceW;
        int pieceCol = shiftedX / pieceW;

        int pieceSeed = HashPixel(pieceCol, row, seed + 555);

        // --- Curved bottom edge with per-pixel noise for irregular/pixelated look ---
        float centerT = Math.Abs(localX - pieceW / 2f) / (pieceW / 2f);
        int edgeNoise = (HashPixel(localX, row, pieceSeed + 123) & 3) - 1; // -1 to 2
        int curveHeight = rowH - 1 - (int)(centerT * centerT * 3) + edgeNoise;
        curveHeight = Math.Clamp(curveHeight, 2, rowH - 1);

        bool belowCurve = localY > curveHeight;

        // --- Vertical gap: irregular width (0-1px jitter per row) ---
        if (pieceW < 32)
        {
            int gapJitter = (HashPixel(pieceCol, row, seed + 999) & 1); // 0 or 1
            if (localX <= gapJitter)
                return mat.MortarColor;
        }

        if (belowCurve)
        {
            int nextRowPieceSeed = HashPixel(pieceCol, row + 1, seed + 555);
            float nt = ((nextRowPieceSeed & 0xFFFF) % 1000) / 999f;
            var behindColor = LerpColor(mat.VariantColorMin, mat.VariantColorMax, nt);
            return ShiftBrightness(behindColor, -0.10f);
        }

        // --- Overlap shadow at top: irregular (1-2px, varies per shingle) ---
        int shadowDepth = 1 + ((pieceSeed >> 12) & 1); // 1 or 2
        if (localY < shadowDepth && mat.Staggered)
            return ShiftBrightness(mat.MortarColor, 0.05f);

        // --- Shingle face color ---
        float t = ((pieceSeed & 0xFFFF) % 1000) / 999f;
        var color = LerpColor(mat.VariantColorMin, mat.VariantColorMax, t);

        // Subtle vertical grain
        int grainH = HashPixel(localX, py / 3, pieceSeed);
        float grain = ((grainH & 0xFF) / 255f - 0.5f) * 0.04f;
        color = ShiftBrightness(color, grain);

        // Slight darkening toward bottom of each shingle (depth)
        float yFade = (float)localY / curveHeight * 0.05f;
        color = ShiftBrightness(color, -yFade);

        // Per-pixel roughness
        if (mat.Roughness > 0f)
        {
            int h = HashPixel(px, py, seed + 777);
            float rough = ((h & 0xFF) / 255f - 0.5f) * mat.Roughness;
            color = ShiftBrightness(color, rough);
        }

        return color;
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

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color ShiftBrightness(Color c, float shift)
    {
        return new Color(
            (byte)Math.Clamp(c.R + (int)(shift * 255), 0, 255),
            (byte)Math.Clamp(c.G + (int)(shift * 255), 0, 255),
            (byte)Math.Clamp(c.B + (int)(shift * 255), 0, 255));
    }

    private static int HashPixel(int x, int y, int seed)
    {
        int h = x * 374761393 + y * 668265263 + seed * 1274126177;
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }
}

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

    public void Draw(SpriteBatch spriteBatch, TileMap map, Camera camera, FogOfWar fow,
        Vector3 ambientColor)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = camera.GetVisibleTileRect(tileSize);

        foreach (var zone in map.GetAllZones())
        {
            if (!zone.HasRoof) continue;
            _roofAlpha.TryGetValue(zone.Id, out float alpha);
            if (alpha <= 0.01f) continue;

            int alphaByte = (int)(alpha * 255);
            // Tint roof by ambient light — bright in daylight, dark at night
            int ar = (int)(ambientColor.X * 255);
            int ag = (int)(ambientColor.Y * 255);
            int ab = (int)(ambientColor.Z * 255);
            var tint = new Color(ar, ag, ab, alphaByte);
            // Memory tint: teal-shifted, matching terrain memory style
            var memTint = new Color(
                (int)(ambientColor.X * 0.88f * 255),
                (int)(ambientColor.Y * 0.93f * 255),
                (int)(ambientColor.Z * 0.97f * 255),
                alphaByte);

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

                    // Position relative to zone center (for ridge shading)
                    float zoneRelX = (zone.Bounds.Width > 1)
                        ? (x - zone.Bounds.X) / (float)(zone.Bounds.Width - 1) : 0.5f;
                    float zoneRelY = (zone.Bounds.Height > 1)
                        ? (y - zone.Bounds.Y) / (float)(zone.Bounds.Height - 1) : 0.5f;

                    var cacheKey = $"roof_{(byte)zone.RoofMaterial}_{ComputeVariantSeed(x, y)}_{(int)doorEdge}_{(int)(zoneRelX*10)}_{(int)(zoneRelY*10)}";

                    Texture2D texture;
                    if (useNormalColor)
                    {
                        texture = _cache.GetOrCreate(cacheKey, () =>
                            GenerateRoofTile(spriteBatch.GraphicsDevice, zone, x, y, doorEdge, zoneRelX, zoneRelY));
                    }
                    else
                    {
                        string memKey = "mem_" + cacheKey;
                        texture = _memoryCache.GetOrCreate(memKey, () =>
                        {
                            var pixels = GenerateRoofPixels(zone, x, y, doorEdge, zoneRelX, zoneRelY);
                            ToMemoryColors(pixels);
                            var tex = new Texture2D(spriteBatch.GraphicsDevice, GameConfig.TileSize, GameConfig.TileSize);
                            tex.SetData(pixels);
                            return tex;
                        });
                    }

                    var destRect = camera.TileToScreenRect(x, y, tileSize);

                    spriteBatch.Draw(texture, destRect, useNormalColor ? tint : memTint);
                }
            }
        }
    }

    public bool IsHiddenByRoof(TileMap map, int tileX, int tileY, ushort playerZoneId)
    {
        if (!map.HasElevatedCover(tileX, tileY)) return false;
        ushort zoneId = map.GetZoneId(tileX, tileY);
        if (zoneId == 0 || zoneId == playerZoneId) return false;

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
        int worldX, int worldY, DoorEdge doorEdge, float zoneRelX = 0.5f, float zoneRelY = 0.5f)
    {
        var pixels = GenerateRoofPixels(zone, worldX, worldY, doorEdge, zoneRelX, zoneRelY);
        var tex = new Texture2D(device, GameConfig.TileSize, GameConfig.TileSize);
        tex.SetData(pixels);
        return tex;
    }

    private Color[] GenerateRoofPixels(ZoneDefinition zone, int worldX, int worldY,
        DoorEdge doorEdge, float zoneRelX = 0.5f, float zoneRelY = 0.5f)
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

        // Ridge shading: center of roof is the peak (lighter), edges are eaves (darker)
        // Also north side slightly lighter (facing light), south side slightly darker
        float distFromCenterX = Math.Abs(zoneRelX - 0.5f) * 2f; // 0 at center, 1 at edge
        float distFromCenterY = Math.Abs(zoneRelY - 0.5f) * 2f;
        float edgeDist = Math.Max(distFromCenterX, distFromCenterY);
        float ridgeShade = -edgeDist * 0.12f; // darken up to 12% at edges
        // North-south bias: top of roof slightly brighter
        float nsBias = (zoneRelY - 0.5f) * 0.08f; // north=+4%, south=-4%

        if (ridgeShade != 0f || nsBias != 0f)
        {
            float totalShade = ridgeShade - nsBias;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = ShiftBrightness(pixels[i], totalShade);
        }

        PixelUtil.Pixelize(pixels, size, mat.PixelSize);

        return pixels;
    }

    private static Color SampleRoofPixel(RoofMaterial mat, int seed, int px, int py)
    {
        // Cave stone: rough rocky texture, no shingle pattern
        if (mat.Type == RoofMaterialType.CaveStone)
            return SampleCaveStone(mat, seed, px, py);

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
        int edgeNoise = (HashPixel(localX, row, pieceSeed + 123) & 1);
        int curveHeight = rowH - 1 - (int)(centerT * centerT * 4) + edgeNoise;
        curveHeight = Math.Clamp(curveHeight, 2, rowH - 1);

        bool belowCurve = localY > curveHeight;

        // --- Vertical gap between shingles: subtle darkening, not a hard line ---
        if (pieceW < 32 && localX == 0)
        {
            // Sample the shingle color but darken slightly instead of using mortar color
            float gt = 0.3f + ((pieceSeed & 0xFF) / 255f) * 0.4f;
            var gapColor = LerpColor(mat.VariantColorMin, mat.VariantColorMax, gt);
            return ShiftBrightness(gapColor, -0.06f);
        }

        if (belowCurve)
        {
            // Below the curve: the next row's shingle peeks through, visibly darker
            int nextRowPieceSeed = HashPixel(pieceCol, row + 1, seed + 555);
            float nt = 0.3f + ((nextRowPieceSeed & 0xFF) / 255f) * 0.4f;
            var behindColor = LerpColor(mat.VariantColorMin, mat.VariantColorMax, nt);
            return ShiftBrightness(behindColor, -0.10f);
        }

        // --- Overlap shadow at top of row: soft, 1px ---
        if (localY == 0 && mat.Staggered)
        {
            float st = 0.3f + ((pieceSeed & 0xFF) / 255f) * 0.4f;
            var shadowColor = LerpColor(mat.VariantColorMin, mat.VariantColorMax, st);
            return ShiftBrightness(shadowColor, -0.04f);
        }

        // --- Shingle face color ---
        // Narrow the range so adjacent shingles don't contrast too much
        float t = 0.3f + ((pieceSeed & 0xFF) / 255f) * 0.4f; // 0.3..0.7 range
        var color = LerpColor(mat.VariantColorMin, mat.VariantColorMax, t);

        // Very subtle darkening toward bottom
        float yFade = (float)localY / curveHeight * 0.03f;
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
        const float saturation = 0.35f;
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            float gray = c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
            int r = (int)(gray + (c.R - gray) * saturation);
            int g = (int)(gray + (c.G - gray) * saturation);
            int b = (int)(gray + (c.B - gray) * saturation);
            pixels[i] = new Color(r, g, b, c.A);
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

    private static Color SampleCaveStone(RoofMaterial mat, int seed, int px, int py)
    {
        // Multi-scale rocky noise using hash
        int h1 = HashPixel(px / 6, py / 6, seed + 111); // large rock patches
        int h2 = HashPixel(px / 3, py / 3, seed + 222); // medium detail
        int h3 = HashPixel(px, py, seed + 333);          // fine grain

        float large = ((h1 & 0xFF) / 255f - 0.5f) * 0.14f;
        float medium = ((h2 & 0xFF) / 255f - 0.5f) * 0.08f;
        float fine = ((h3 & 0xFF) / 255f - 0.5f) * 0.04f;

        // Base color with per-tile variation
        float t = 0.3f + ((HashPixel(0, 0, seed) & 0xFF) / 255f) * 0.4f;
        var color = LerpColor(mat.VariantColorMin, mat.VariantColorMax, t);

        color = ShiftBrightness(color, large + medium + fine);

        // Occasional dark crevice pixels
        if ((h3 & 0x3F) == 0)
            color = ShiftBrightness(color, -0.08f);

        return color;
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

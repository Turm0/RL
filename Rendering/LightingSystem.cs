using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Computes per-sub-tile lighting via recursive shadowcasting,
/// renders to a light texture, and composites over the scene with multiply blend.
/// </summary>
public class LightingSystem
{
    private const int SubTileRes = 6;

    private RenderTarget2D _lightRT;
    private float[] _lightBuffer;
    private int _bufferWidth;
    private int _bufferHeight;
    private int _lastVisibleW;
    private int _lastVisibleH;
    private Rectangle _visibleRect;

    // Per-light visited set to prevent double-counting at octant boundaries
    private readonly HashSet<long> _visited = new();

    private readonly Vector3 _ambientColor = new(0.22f, 0.20f, 0.24f);

    private readonly BlendState _multiplyBlend = new()
    {
        ColorBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.DestinationAlpha,
        AlphaDestinationBlend = Blend.Zero
    };

    // Octant multiplier tables for recursive shadowcasting
    private static readonly int[] _mulXX = { 1,  0,  0, -1, -1,  0,  0,  1 };
    private static readonly int[] _mulXY = { 0,  1, -1,  0,  0, -1,  1,  0 };
    private static readonly int[] _mulYX = { 0,  1,  1,  0,  0, -1, -1,  0 };
    private static readonly int[] _mulYY = { 1,  0,  0,  1, -1,  0,  0, -1 };

    /// <summary>
    /// Resizes internal buffers if the visible tile area has changed.
    /// </summary>
    public void Resize(int visibleTilesWide, int visibleTilesHigh, GraphicsDevice device)
    {
        if (visibleTilesWide == _lastVisibleW && visibleTilesHigh == _lastVisibleH && _lightRT != null)
            return;

        _lastVisibleW = visibleTilesWide;
        _lastVisibleH = visibleTilesHigh;
        _bufferWidth = visibleTilesWide * SubTileRes;
        _bufferHeight = visibleTilesHigh * SubTileRes;
        _lightBuffer = new float[_bufferWidth * _bufferHeight * 3];

        _lightRT?.Dispose();
        _lightRT = new RenderTarget2D(device, _bufferWidth, _bufferHeight);
    }

    /// <summary>
    /// Clears the light buffer to ambient color.
    /// </summary>
    public void BeginFrame(Rectangle visibleRect)
    {
        _visibleRect = visibleRect;
        for (int i = 0; i < _lightBuffer.Length; i += 3)
        {
            _lightBuffer[i + 0] = _ambientColor.X;
            _lightBuffer[i + 1] = _ambientColor.Y;
            _lightBuffer[i + 2] = _ambientColor.Z;
        }
    }

    /// <summary>
    /// Adds a light source using recursive shadowcasting.
    /// </summary>
    public void AddLight(int tileX, int tileY, float radius, float intensity,
        Vector3 color, TileMap map, float time, bool flicker, float flickerIntensity, int flickerSeed)
    {
        float flickerMod = 1f;
        if (flicker)
        {
            float phase1 = flickerSeed * 1.7f;
            float phase2 = flickerSeed * 2.3f;
            flickerMod = 1f - flickerIntensity * MathF.Sin(time * 4f + phase1) * MathF.Sin(time * 7f + phase2) * 0.5f;
        }

        float effectiveIntensity = intensity * flickerMod;
        float lightCenterX = tileX + 0.5f;
        float lightCenterY = tileY + 0.5f;

        // Clear visited set for this light
        _visited.Clear();

        // Light the source tile
        LightTile(tileX, tileY, lightCenterX, lightCenterY, radius, effectiveIntensity, color, map);

        // Cast light through 8 octants
        for (int octant = 0; octant < 8; octant++)
            CastOctant(map, tileX, tileY, lightCenterX, lightCenterY, radius, effectiveIntensity, color, octant, 1, 1f, 0f);
    }

    /// <summary>
    /// Uploads the light buffer to the render target texture.
    /// Non-visible tiles (per FOV) are set to white (1.0) so the multiply blend
    /// passes through the memory/terrain color unchanged — lighting only affects visible tiles.
    /// </summary>
    public void BuildTexture(GraphicsDevice device, FogOfWar fow)
    {
        var pixels = new Color[_bufferWidth * _bufferHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            // Map buffer index back to tile coordinates
            int subX = i % _bufferWidth;
            int subY = i / _bufferWidth;
            int tileX = _visibleRect.X + subX / SubTileRes;
            int tileY = _visibleRect.Y + subY / SubTileRes;

            // Only apply dynamic lighting to tiles currently in player FOV
            if (fow.IsVisible(tileX, tileY))
            {
                int bi = i * 3;
                float r = Math.Clamp(_lightBuffer[bi + 0], 0f, 1f);
                float g = Math.Clamp(_lightBuffer[bi + 1], 0f, 1f);
                float b = Math.Clamp(_lightBuffer[bi + 2], 0f, 1f);
                pixels[i] = new Color(
                    (byte)(r * 255f),
                    (byte)(g * 255f),
                    (byte)(b * 255f),
                    (byte)255);
            }
            else
            {
                // White = multiply by 1.0 = pass through terrain color unchanged
                pixels[i] = Color.White;
            }
        }
        _lightRT.SetData(pixels);
    }

    /// <summary>
    /// Composites the light texture over the scene using multiply blend.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera camera, int tileSize)
    {
        if (_lightRT == null) return;

        float worldX = _visibleRect.X * tileSize;
        float worldY = _visibleRect.Y * tileSize;
        var screenPos = camera.WorldToScreen(new Vector2(worldX, worldY));

        float pixelWidth = _visibleRect.Width * tileSize;
        float pixelHeight = _visibleRect.Height * tileSize;

        spriteBatch.Begin(SpriteSortMode.Deferred, _multiplyBlend, SamplerState.LinearClamp);
        spriteBatch.Draw(_lightRT,
            new Rectangle((int)screenPos.X, (int)screenPos.Y, (int)pixelWidth, (int)pixelHeight),
            Color.White);
        spriteBatch.End();
    }

    private static long TileKey(int x, int y) => ((long)x << 32) | (uint)y;

    /// <summary>
    /// Lights a tile if not already visited. Walls get reduced brightness. Out-of-bounds tiles are skipped.
    /// </summary>
    private void LightTile(int tileX, int tileY, float lightCX, float lightCY,
        float radius, float intensity, Vector3 color, TileMap map)
    {
        if (!_visited.Add(TileKey(tileX, tileY)))
            return; // already lit by this light

        if (!map.IsInBounds(tileX, tileY))
            return;

        bool isWall = map.GetTile(tileX, tileY) == Data.TileType.Wall;

        // Only light wall tiles that face an open space (have a non-wall neighbor)
        if (isWall && !HasNonWallNeighbor(map, tileX, tileY))
            return;

        float wallMult = isWall ? 0.65f : 1f;

        int relX = tileX - _visibleRect.X;
        int relY = tileY - _visibleRect.Y;

        if (relX < 0 || relX >= _visibleRect.Width || relY < 0 || relY >= _visibleRect.Height)
            return;

        int subX0 = relX * SubTileRes;
        int subY0 = relY * SubTileRes;

        for (int sy = 0; sy < SubTileRes; sy++)
        {
            float subWorldY = tileY + (sy + 0.5f) / SubTileRes;
            int bufY = subY0 + sy;

            for (int sx = 0; sx < SubTileRes; sx++)
            {
                float subWorldX = tileX + (sx + 0.5f) / SubTileRes;

                float ddx = subWorldX - lightCX;
                float ddy = subWorldY - lightCY;
                float dist = MathF.Sqrt(ddx * ddx + ddy * ddy);

                float t = dist / radius;
                if (t >= 1f) continue;
                float falloff = 1f - t * t;

                int idx = (bufY * _bufferWidth + subX0 + sx) * 3;
                _lightBuffer[idx + 0] += intensity * falloff * color.X * wallMult;
                _lightBuffer[idx + 1] += intensity * falloff * color.Y * wallMult;
                _lightBuffer[idx + 2] += intensity * falloff * color.Z * wallMult;
            }
        }
    }

    /// <summary>
    /// Recursive shadowcasting for one octant.
    /// </summary>
    private void CastOctant(TileMap map, int ox, int oy, float lightCX, float lightCY,
        float radius, float intensity, Vector3 color, int octant,
        int row, float startSlope, float endSlope)
    {
        if (startSlope < endSlope) return;

        int radiusCeil = (int)MathF.Ceiling(radius);
        float newStartSlope = 0f;
        bool blocked = false;

        // Scan to radiusCeil+1 to ensure diagonal tiles within Euclidean radius are reached.
        // The tileDist check rejects any tile actually beyond the radius.
        int scanLimit = radiusCeil + 1;

        for (int distance = row; distance <= scanLimit && !blocked; distance++)
        {
            int dy = -distance;

            for (int dx = -distance; dx <= 0; dx++)
            {
                int mapX = ox + dx * _mulXX[octant] + dy * _mulXY[octant];
                int mapY = oy + dx * _mulYX[octant] + dy * _mulYY[octant];

                float lSlope = (dx - 0.5f) / (dy + 0.5f);
                float rSlope = (dx + 0.5f) / (dy - 0.5f);

                if (startSlope < rSlope) continue;
                if (endSlope > lSlope) break;

                int ddx = mapX - ox;
                int ddy = mapY - oy;
                float tileDist = MathF.Sqrt(ddx * ddx + ddy * ddy);

                // Light the tile if within Euclidean radius
                if (tileDist <= radius)
                    LightTile(mapX, mapY, lightCX, lightCY, radius, intensity, color, map);

                bool isOpaque = !map.IsInBounds(mapX, mapY) ||
                                map.GetTile(mapX, mapY) == Data.TileType.Wall;

                if (blocked)
                {
                    if (isOpaque)
                    {
                        newStartSlope = rSlope;
                    }
                    else
                    {
                        blocked = false;
                        startSlope = newStartSlope;
                    }
                }
                else if (isOpaque)
                {
                    blocked = true;
                    CastOctant(map, ox, oy, lightCX, lightCY, radius, intensity, color,
                        octant, distance + 1, startSlope, lSlope);
                    newStartSlope = rSlope;
                }
            }
        }
    }

    /// <summary>
    /// Returns true if the tile has at least one cardinal neighbor that is not a wall.
    /// Used to identify wall surfaces facing open space.
    /// </summary>
    private static bool HasNonWallNeighbor(TileMap map, int x, int y)
    {
        return IsNonWall(map, x - 1, y) || IsNonWall(map, x + 1, y) ||
               IsNonWall(map, x, y - 1) || IsNonWall(map, x, y + 1) ||
               IsNonWall(map, x - 1, y - 1) || IsNonWall(map, x + 1, y - 1) ||
               IsNonWall(map, x - 1, y + 1) || IsNonWall(map, x + 1, y + 1);
    }

    private static bool IsNonWall(TileMap map, int x, int y)
    {
        return map.IsInBounds(x, y) && map.GetTile(x, y) != Data.TileType.Wall;
    }
}

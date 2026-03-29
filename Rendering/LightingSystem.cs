using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

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

    private readonly HashSet<long> _visited = new();

    private Vector3 _ambientColor = new(0.22f, 0.20f, 0.24f);
    private int _ambientMode = 0;

    private static readonly (string Name, Vector3 Color)[] AmbientModes =
    {
        ("Night",         new Vector3(0.08f, 0.07f, 0.12f)),
        ("Moonlit",       new Vector3(0.15f, 0.15f, 0.22f)),
        ("Dawn",          new Vector3(0.35f, 0.25f, 0.22f)),
        ("Morning",       new Vector3(0.55f, 0.50f, 0.42f)),
        ("Day",           new Vector3(0.98f, 0.95f, 0.90f)),
        ("Overcast",      new Vector3(0.50f, 0.50f, 0.52f)),
        ("Sunset",        new Vector3(0.50f, 0.32f, 0.20f)),
        ("Dusk",          new Vector3(0.25f, 0.20f, 0.25f)),
        ("Torchlight",    new Vector3(0.12f, 0.10f, 0.08f)),
    };

    public string CurrentAmbientName => AmbientModes[_ambientMode].Name;
    public Vector3 AmbientColor => _ambientColor;

    public void CycleAmbient()
    {
        _ambientMode = (_ambientMode + 1) % AmbientModes.Length;
        _ambientColor = AmbientModes[_ambientMode].Color;
    }

    private readonly BlendState _multiplyBlend = new()
    {
        ColorBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.DestinationAlpha,
        AlphaDestinationBlend = Blend.Zero
    };

    private static readonly int[] _mulXX = { 1,  0,  0, -1, -1,  0,  0,  1 };
    private static readonly int[] _mulXY = { 0,  1, -1,  0,  0, -1,  1,  0 };
    private static readonly int[] _mulYX = { 0,  1,  1,  0,  0, -1, -1,  0 };
    private static readonly int[] _mulYY = { 1,  0,  0,  1, -1,  0,  0, -1 };

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

    // Indoor ambient: dim baseline that doesn't change with time of day
    private static readonly Vector3 IndoorAmbient = new(0.10f, 0.09f, 0.11f);

    public void BeginFrame(Rectangle visibleRect, TileMap map)
    {
        _visibleRect = visibleRect;

        for (int ty = 0; ty < visibleRect.Height; ty++)
        {
            for (int tx = 0; tx < visibleRect.Width; tx++)
            {
                int mapX = visibleRect.X + tx;
                int mapY = visibleRect.Y + ty;

                // Check if tile is under a roof — use indoor ambient instead
                bool isIndoor = false;
                if (map.IsInBounds(mapX, mapY))
                {
                    ushort zoneId = map.GetZoneId(mapX, mapY);
                    if (zoneId != 0)
                    {
                        var zone = map.GetZone(zoneId);
                        if (zone != null && zone.HasRoof)
                            isIndoor = true;
                    }
                }

                var ambient = isIndoor ? IndoorAmbient : _ambientColor;

                // Fill all sub-tile pixels for this tile
                int subX0 = tx * SubTileRes;
                int subY0 = ty * SubTileRes;
                for (int sy = 0; sy < SubTileRes; sy++)
                    for (int sx = 0; sx < SubTileRes; sx++)
                    {
                        int idx = ((subY0 + sy) * _bufferWidth + subX0 + sx) * 3;
                        _lightBuffer[idx + 0] = ambient.X;
                        _lightBuffer[idx + 1] = ambient.Y;
                        _lightBuffer[idx + 2] = ambient.Z;
                    }
            }
        }
    }

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

        _visited.Clear();

        LightTile(tileX, tileY, lightCenterX, lightCenterY, radius, effectiveIntensity, color, map);

        for (int octant = 0; octant < 8; octant++)
            CastOctant(map, tileX, tileY, lightCenterX, lightCenterY, radius, effectiveIntensity, color, octant, 1, 1f, 0f);
    }

    /// <summary>
    /// Applies a 3x3 box blur to the light buffer to soften shadow edges.
    /// </summary>
    public void BlurBuffer(TileMap map)
    {
        var temp = new float[_lightBuffer.Length];
        Array.Copy(_lightBuffer, temp, _lightBuffer.Length);

        for (int y = 0; y < _bufferHeight; y++)
        {
            for (int x = 0; x < _bufferWidth; x++)
            {
                // Map this sub-pixel back to tile coordinates
                int tileX = _visibleRect.X + x / SubTileRes;
                int tileY = _visibleRect.Y + y / SubTileRes;

                // Skip blur for pixels on or adjacent to walls
                if (map.BlocksLight(tileX, tileY))
                    continue;

                float r = 0, g = 0, b = 0;
                int count = 0;

                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= _bufferHeight) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= _bufferWidth) continue;

                        // Only average with non-wall pixels
                        int nTileX = _visibleRect.X + nx / SubTileRes;
                        int nTileY = _visibleRect.Y + ny / SubTileRes;
                        if (map.BlocksLight(nTileX, nTileY)) continue;

                        int idx = (ny * _bufferWidth + nx) * 3;
                        r += _lightBuffer[idx];
                        g += _lightBuffer[idx + 1];
                        b += _lightBuffer[idx + 2];
                        count++;
                    }
                }

                if (count > 0)
                {
                    int outIdx = (y * _bufferWidth + x) * 3;
                    temp[outIdx] = r / count;
                    temp[outIdx + 1] = g / count;
                    temp[outIdx + 2] = b / count;
                }
            }
        }

        Array.Copy(temp, _lightBuffer, _lightBuffer.Length);
    }

    public void BuildTexture(GraphicsDevice device, FogOfWar fow)
    {
        var pixels = new Color[_bufferWidth * _bufferHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            int subX = i % _bufferWidth;
            int subY = i / _bufferWidth;
            int tileX = _visibleRect.X + subX / SubTileRes;
            int tileY = _visibleRect.Y + subY / SubTileRes;

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
                pixels[i] = Color.White;
            }
        }
        _lightRT.SetData(pixels);
    }

    public void Draw(SpriteBatch spriteBatch, Camera camera, int tileSize)
    {
        if (_lightRT == null) return;

        float worldX = _visibleRect.X * tileSize;
        float worldY = _visibleRect.Y * tileSize;
        var screenPos = camera.WorldToScreen(new Vector2(worldX, worldY));

        float pixelWidth = _visibleRect.Width * tileSize;
        float pixelHeight = _visibleRect.Height * tileSize;

        spriteBatch.Begin(SpriteSortMode.Deferred, _multiplyBlend, SamplerState.PointClamp);
        spriteBatch.Draw(_lightRT,
            new Rectangle((int)screenPos.X, (int)screenPos.Y, (int)pixelWidth, (int)pixelHeight),
            Color.White);
        spriteBatch.End();
    }

    private static long TileKey(int x, int y) => ((long)x << 32) | (uint)y;

    private void LightTile(int tileX, int tileY, float lightCX, float lightCY,
        float radius, float intensity, Vector3 color, TileMap map)
    {
        if (!_visited.Add(TileKey(tileX, tileY)))
            return;

        if (!map.IsInBounds(tileX, tileY))
            return;

        bool isWall = map.BlocksLight(tileX, tileY);

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
                float f = 1f - t * t;
                float falloff = f * f;

                int idx = (bufY * _bufferWidth + subX0 + sx) * 3;
                _lightBuffer[idx + 0] += intensity * falloff * color.X * wallMult;
                _lightBuffer[idx + 1] += intensity * falloff * color.Y * wallMult;
                _lightBuffer[idx + 2] += intensity * falloff * color.Z * wallMult;
            }
        }
    }

    private void CastOctant(TileMap map, int ox, int oy, float lightCX, float lightCY,
        float radius, float intensity, Vector3 color, int octant,
        int row, float startSlope, float endSlope)
    {
        if (startSlope < endSlope) return;

        int radiusCeil = (int)MathF.Ceiling(radius);
        float newStartSlope = 0f;
        bool blocked = false;

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

                int ddx2 = mapX - ox;
                int ddy2 = mapY - oy;
                float tileDist = MathF.Sqrt(ddx2 * ddx2 + ddy2 * ddy2);

                if (tileDist <= radius)
                    LightTile(mapX, mapY, lightCX, lightCY, radius, intensity, color, map);

                bool isOpaque = !map.IsInBounds(mapX, mapY) || map.BlocksLight(mapX, mapY);

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

    private bool HasNonWallNeighbor(TileMap map, int x, int y)
    {
        return IsNonWall(map, x - 1, y) || IsNonWall(map, x + 1, y) ||
               IsNonWall(map, x, y - 1) || IsNonWall(map, x, y + 1) ||
               IsNonWall(map, x - 1, y - 1) || IsNonWall(map, x + 1, y - 1) ||
               IsNonWall(map, x - 1, y + 1) || IsNonWall(map, x + 1, y + 1);
    }

    private bool IsNonWall(TileMap map, int x, int y)
    {
        return map.IsInBounds(x, y) && !map.BlocksLight(x, y);
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.World;

public class FogOfWar
{
    private readonly bool[,] _explored;
    private readonly bool[,] _visible;
    private readonly bool[,] _visited;
    private readonly float[,] _distance; // distance from player, set during Compute
    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private ushort _viewerZoneId;
    private int _fovRadius;
    private int _playerX, _playerY;

    // Pooled list for corner fills (avoids allocation per frame)
    private readonly List<(int x, int y)> _cornerFills = new(64);

    public FogOfWar(int mapWidth, int mapHeight)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _explored = new bool[mapWidth, mapHeight];
        _visible = new bool[mapWidth, mapHeight];
        _visited = new bool[mapWidth, mapHeight];
        _distance = new float[mapWidth, mapHeight];
    }

    public bool IsVisible(int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return false;
        return _visible[x, y];
    }

    public bool IsExplored(int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return false;
        return _explored[x, y];
    }

    /// <summary>
    /// Returns 1.0 for tiles close to the player, fading toward 0.0 at the FOV edge.
    /// Used for gradual desaturation at vision boundary.
    /// </summary>
    public float GetVisibilityFactor(int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return 0f;
        if (!_visible[x, y]) return 0f;
        if (_fovRadius <= 0) return 1f;
        float dist = _distance[x, y];
        // Full color within 5 tiles, then gradual fade to FOV edge
        float fadeStart = 5f;
        if (dist <= fadeStart) return 1f;
        float fadeEnd = _fovRadius;
        if (dist >= fadeEnd) return 0f;
        return 1f - (dist - fadeStart) / (fadeEnd - fadeStart);
    }

    public void Compute(int playerX, int playerY, int radius, TileMap map, ushort viewerZoneId = 0)
    {
        Array.Clear(_visible, 0, _visible.Length);
        Array.Clear(_visited, 0, _visited.Length);
        Array.Clear(_distance, 0, _distance.Length);
        _viewerZoneId = viewerZoneId;
        _fovRadius = radius;
        _playerX = playerX;
        _playerY = playerY;

        MarkVisible(playerX, playerY);

        for (int octant = 0; octant < 8; octant++)
            CastOctant(map, playerX, playerY, radius, octant, 1, 1f, 0f);

        MarkVisibleWalls(map);
    }

    private void MarkVisible(int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return;
        if (_visited[x, y]) return;
        _visited[x, y] = true;
        _visible[x, y] = true;
        _explored[x, y] = true;
        int dx = x - _playerX, dy = y - _playerY;
        _distance[x, y] = MathF.Sqrt(dx * dx + dy * dy);
    }

    private void MarkReached(int x, int y, TileMap map)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return;
        if (_visited[x, y]) return;
        _visited[x, y] = true;

        int dx = x - _playerX, dy = y - _playerY;
        _distance[x, y] = MathF.Sqrt(dx * dx + dy * dy);

        if (!map.HasWall(x, y))
        {
            _visible[x, y] = true;
            _explored[x, y] = true;
        }
    }

    private void MarkVisibleWalls(TileMap map)
    {
        // First pass: make walls with visible floor neighbors visible
        for (int x = 0; x < _mapWidth; x++)
        {
            for (int y = 0; y < _mapHeight; y++)
            {
                if (!_visited[x, y]) continue;
                if (!map.HasWall(x, y)) continue;
                if (_visible[x, y]) continue;

                if (HasVisibleFloorNeighbor(map, x, y))
                {
                    _visible[x, y] = true;
                    _explored[x, y] = true;
                }
            }
        }

        // Second pass: corner fills
        _cornerFills.Clear();
        for (int x = 0; x < _mapWidth; x++)
        {
            for (int y = 0; y < _mapHeight; y++)
            {
                if (!_visible[x, y]) continue;
                if (map.HasWall(x, y)) continue;

                TryFillCorner(map, x, y, -1, -1);
                TryFillCorner(map, x, y,  1, -1);
                TryFillCorner(map, x, y, -1,  1);
                TryFillCorner(map, x, y,  1,  1);
            }
        }

        foreach (var (cx, cy) in _cornerFills)
        {
            if (!_visible[cx, cy])
            {
                _visible[cx, cy] = true;
                _explored[cx, cy] = true;
            }
        }
    }

    private void TryFillCorner(TileMap map, int floorX, int floorY, int dx, int dy)
    {
        int cornerX = floorX + dx;
        int cornerY = floorY + dy;
        int wallAX = floorX + dx;
        int wallAY = floorY;
        int wallBX = floorX;
        int wallBY = floorY + dy;

        if (!map.IsInBounds(cornerX, cornerY)) return;
        if (!map.HasWall(cornerX, cornerY)) return;

        if (wallAX >= 0 && wallAX < _mapWidth && wallAY >= 0 && wallAY < _mapHeight &&
            _visible[wallAX, wallAY] && map.HasWall(wallAX, wallAY) &&
            wallBX >= 0 && wallBX < _mapWidth && wallBY >= 0 && wallBY < _mapHeight &&
            _visible[wallBX, wallBY] && map.HasWall(wallBX, wallBY))
        {
            _cornerFills.Add((cornerX, cornerY));
        }
    }

    private bool HasVisibleFloorNeighbor(TileMap map, int x, int y)
    {
        return IsVisibleNonWall(map, x - 1, y) || IsVisibleNonWall(map, x + 1, y) ||
               IsVisibleNonWall(map, x, y - 1) || IsVisibleNonWall(map, x, y + 1);
    }

    private bool IsVisibleNonWall(TileMap map, int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return false;
        if (map.HasWall(x, y)) return false;
        return _visible[x, y];
    }

    private static readonly int[] _xx = { 1,  0,  0, -1, -1,  0,  0,  1 };
    private static readonly int[] _xy = { 0,  1, -1,  0,  0, -1,  1,  0 };
    private static readonly int[] _yx = { 0,  1,  1,  0,  0, -1, -1,  0 };
    private static readonly int[] _yy = { 1,  0,  0,  1, -1,  0,  0, -1 };

    private void CastOctant(TileMap map, int ox, int oy, int radius, int octant,
        int row, float startSlope, float endSlope)
    {
        if (startSlope < endSlope) return;

        float newStartSlope = 0f;
        bool blocked = false;

        int scanLimit = radius + 1;
        for (int distance = row; distance <= scanLimit && !blocked; distance++)
        {
            int dy = -distance;

            for (int dx = -distance; dx <= 0; dx++)
            {
                int mapX = ox + dx * _xx[octant] + dy * _xy[octant];
                int mapY = oy + dx * _yx[octant] + dy * _yy[octant];

                float lSlope = (dx - 0.5f) / (dy + 0.5f);
                float rSlope = (dx + 0.5f) / (dy - 0.5f);

                if (startSlope < rSlope) continue;
                if (endSlope > lSlope) break;

                int ddx = mapX - ox;
                int ddy = mapY - oy;
                float tileDist = MathF.Sqrt(ddx * ddx + ddy * ddy);

                if (tileDist <= radius)
                    MarkReached(mapX, mapY, map);

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
                    CastOctant(map, ox, oy, radius, octant, distance + 1, startSlope, lSlope);
                    newStartSlope = rSlope;
                }
            }
        }
    }
}

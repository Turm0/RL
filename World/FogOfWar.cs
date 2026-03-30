using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.World;

public class FogOfWar
{
    private readonly bool[,] _explored;
    private readonly HashSet<long> _visible;
    private readonly HashSet<long> _visited;
    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private ushort _viewerZoneId;

    public FogOfWar(int mapWidth, int mapHeight)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _explored = new bool[mapWidth, mapHeight];
        _visible = new HashSet<long>();
        _visited = new HashSet<long>();
    }

    public bool IsVisible(int x, int y) => _visible.Contains(Key(x, y));

    public bool IsExplored(int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return false;
        return _explored[x, y];
    }

    public void Compute(int playerX, int playerY, int radius, TileMap map, ushort viewerZoneId = 0)
    {
        _visible.Clear();
        _visited.Clear();
        _viewerZoneId = viewerZoneId;

        MarkVisible(playerX, playerY);

        for (int octant = 0; octant < 8; octant++)
            CastOctant(map, playerX, playerY, radius, octant, 1, 1f, 0f);

        MarkVisibleWalls(map);
    }

    private void MarkVisible(int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return;
        if (!_visited.Add(Key(x, y))) return;
        _visible.Add(Key(x, y));
        _explored[x, y] = true;
    }

    private void MarkReached(int x, int y, TileMap map)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return;
        if (!_visited.Add(Key(x, y))) return;

        if (!map.HasWall(x, y))
        {
            _visible.Add(Key(x, y));
            _explored[x, y] = true;
        }
    }

    private void MarkVisibleWalls(TileMap map)
    {
        foreach (long key in _visited)
        {
            int x = (int)(key >> 32);
            int y = (int)(key & 0xFFFFFFFF);

            if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) continue;
            if (!map.HasWall(x, y)) continue;
            if (_visible.Contains(key)) continue;

            if (HasVisibleFloorNeighbor(map, x, y))
            {
                _visible.Add(key);
                _explored[x, y] = true;
            }
        }

        var cornerFills = new List<(int x, int y)>();
        foreach (long key in _visible)
        {
            int x = (int)(key >> 32);
            int y = (int)(key & 0xFFFFFFFF);

            if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) continue;
            if (map.HasWall(x, y)) continue;

            TryFillCorner(map, x, y, -1, -1, cornerFills);
            TryFillCorner(map, x, y,  1, -1, cornerFills);
            TryFillCorner(map, x, y, -1,  1, cornerFills);
            TryFillCorner(map, x, y,  1,  1, cornerFills);
        }

        foreach (var (cx, cy) in cornerFills)
        {
            var k = Key(cx, cy);
            if (!_visible.Contains(k))
            {
                _visible.Add(k);
                _explored[cx, cy] = true;
            }
        }
    }

    private void TryFillCorner(TileMap map, int floorX, int floorY, int dx, int dy,
        List<(int, int)> fills)
    {
        int cornerX = floorX + dx;
        int cornerY = floorY + dy;
        int wallAX = floorX + dx;
        int wallAY = floorY;
        int wallBX = floorX;
        int wallBY = floorY + dy;

        if (!map.IsInBounds(cornerX, cornerY)) return;
        if (!map.HasWall(cornerX, cornerY)) return;

        if (_visible.Contains(Key(wallAX, wallAY)) && map.HasWall(wallAX, wallAY) &&
            _visible.Contains(Key(wallBX, wallBY)) && map.HasWall(wallBX, wallBY))
        {
            fills.Add((cornerX, cornerY));
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
        return _visible.Contains(Key(x, y));
    }

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;

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

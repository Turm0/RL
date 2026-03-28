using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.World;

/// <summary>
/// Tracks tile visibility states: unexplored, explored (previously seen), and visible (in current FOV).
/// Uses recursive shadowcasting to compute player field-of-view.
/// </summary>
public class FogOfWar
{
    private readonly bool[,] _explored; // true if tile has ever been seen
    private readonly HashSet<long> _visible; // tiles currently in FOV
    private readonly HashSet<long> _visited; // per-computation dedup set
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    public FogOfWar(int mapWidth, int mapHeight)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _explored = new bool[mapWidth, mapHeight];
        _visible = new HashSet<long>();
        _visited = new HashSet<long>();
    }

    /// <summary>Returns true if the tile is currently visible to the player.</summary>
    public bool IsVisible(int x, int y) => _visible.Contains(Key(x, y));

    /// <summary>Returns true if the tile has been seen before (explored).</summary>
    public bool IsExplored(int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return false;
        return _explored[x, y];
    }

    /// <summary>
    /// Recomputes the player's field-of-view from the given position using shadowcasting.
    /// </summary>
    public void Compute(int playerX, int playerY, int radius, TileMap map)
    {
        _visible.Clear();
        _visited.Clear();

        // Mark origin visible
        MarkVisible(playerX, playerY);

        // Cast through 8 octants
        for (int octant = 0; octant < 8; octant++)
            CastOctant(map, playerX, playerY, radius, octant, 1, 1f, 0f);

        // Post-pass: mark wall tiles as visible/explored only if they neighbor a visible non-wall tile.
        // This prevents deep walls behind rooms from leaking into the explored map.
        MarkVisibleWalls(map);
    }

    private void MarkVisible(int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return;
        if (!_visited.Add(Key(x, y))) return;
        _visible.Add(Key(x, y));
        _explored[x, y] = true;
    }

    /// <summary>
    /// Marks a tile as reached by the shadowcast but does NOT add to visible/explored yet.
    /// Wall tiles are deferred — only added in the post-pass if they neighbor a visible floor.
    /// </summary>
    private void MarkReached(int x, int y, TileMap map)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return;
        if (!_visited.Add(Key(x, y))) return;

        // Non-wall tiles: immediately visible
        if (map.GetTile(x, y) != Data.TileType.Wall)
        {
            _visible.Add(Key(x, y));
            _explored[x, y] = true;
        }
        // Wall tiles: deferred to post-pass (stored in _visited but not _visible yet)
    }

    /// <summary>
    /// Post-pass: for every wall tile reached by the shadowcast, mark it visible
    /// only if it has a cardinal neighbor that is a visible non-wall tile.
    /// Then fill diagonal corners: if a visible floor tile has two orthogonal visible walls,
    /// the diagonal wall between them is also marked visible.
    /// </summary>
    private void MarkVisibleWalls(TileMap map)
    {
        // Pass 1: walls with a cardinal visible floor neighbor
        foreach (long key in _visited)
        {
            int x = (int)(key >> 32);
            int y = (int)(key & 0xFFFFFFFF);

            if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) continue;
            if (map.GetTile(x, y) != Data.TileType.Wall) continue;
            if (_visible.Contains(key)) continue;

            if (HasVisibleFloorNeighbor(map, x, y))
            {
                _visible.Add(key);
                _explored[x, y] = true;
            }
        }

        // Pass 2: diagonal corner fill
        // For each visible floor tile, check all 4 diagonal corners.
        // If both orthogonal walls are visible, make the diagonal wall visible too.
        var cornerFills = new List<(int x, int y)>();
        foreach (long key in _visible)
        {
            int x = (int)(key >> 32);
            int y = (int)(key & 0xFFFFFFFF);

            if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) continue;
            if (map.GetTile(x, y) == Data.TileType.Wall) continue; // only from floor tiles

            // Check 4 diagonal corners: (-1,-1), (1,-1), (-1,1), (1,1)
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
        if (map.GetTile(cornerX, cornerY) != Data.TileType.Wall) return;

        // Both orthogonal neighbors must be visible walls
        if (_visible.Contains(Key(wallAX, wallAY)) && IsWall(map, wallAX, wallAY) &&
            _visible.Contains(Key(wallBX, wallBY)) && IsWall(map, wallBX, wallBY))
        {
            fills.Add((cornerX, cornerY));
        }
    }

    private static bool IsWall(TileMap map, int x, int y)
    {
        return map.IsInBounds(x, y) && map.GetTile(x, y) == Data.TileType.Wall;
    }

    private bool HasVisibleFloorNeighbor(TileMap map, int x, int y)
    {
        return IsVisibleNonWall(map, x - 1, y) || IsVisibleNonWall(map, x + 1, y) ||
               IsVisibleNonWall(map, x, y - 1) || IsVisibleNonWall(map, x, y + 1);
    }

    private bool IsVisibleNonWall(TileMap map, int x, int y)
    {
        if (x < 0 || x >= _mapWidth || y < 0 || y >= _mapHeight) return false;
        if (map.GetTile(x, y) == Data.TileType.Wall) return false;
        return _visible.Contains(Key(x, y));
    }

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;

    // Octant multiplier tables
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
                    CastOctant(map, ox, oy, radius, octant, distance + 1, startSlope, lSlope);
                    newStartSlope = rSlope;
                }
            }
        }
    }
}

using RoguelikeEngine.Data;

namespace RoguelikeEngine.World;

/// <summary>
/// 2D grid of tiles representing the game world.
/// </summary>
public class TileMap
{
    private readonly TileType[,] _tiles;

    /// <summary>Width of the map in tiles.</summary>
    public int Width { get; }

    /// <summary>Height of the map in tiles.</summary>
    public int Height { get; }

    public TileMap(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new TileType[width, height];
    }

    /// <summary>Gets the tile type at the given coordinates.</summary>
    public TileType GetTile(int x, int y)
    {
        return IsInBounds(x, y) ? _tiles[x, y] : TileType.Wall;
    }

    /// <summary>Sets the tile type at the given coordinates.</summary>
    public void SetTile(int x, int y, TileType type)
    {
        if (IsInBounds(x, y))
            _tiles[x, y] = type;
    }

    /// <summary>Returns true if the coordinates are within map bounds.</summary>
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    /// <summary>Returns true if the tile at the given position can be walked on (Floor or Water).</summary>
    public bool IsWalkable(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        var tile = _tiles[x, y];
        return tile == TileType.Floor || tile == TileType.Water;
    }

    /// <summary>Creates the hardcoded test dungeon for Phase 1.</summary>
    public static TileMap CreateTestDungeon()
    {
        var map = new TileMap(50, 40);

        // Fill everything with walls
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                map.SetTile(x, y, TileType.Wall);

        // Helper to carve a rectangular room of floor tiles
        static void CarveRoom(TileMap m, int x1, int y1, int x2, int y2, TileType type = TileType.Floor)
        {
            for (int x = x1; x <= x2; x++)
                for (int y = y1; y <= y2; y++)
                    m.SetTile(x, y, type);
        }

        // Room 1: (3,3) to (12,9)
        CarveRoom(map, 3, 3, 12, 9);

        // Room 2: (16,3) to (26,11)
        CarveRoom(map, 16, 3, 26, 11);

        // Room 3: (3,14) to (13,22)
        CarveRoom(map, 3, 14, 13, 22);

        // Room 4: (18,15) to (28,23)
        CarveRoom(map, 18, 15, 28, 23);

        // Corridor: room 1 to room 2 — horizontal at y=6 from x=13 to x=15
        CarveRoom(map, 13, 6, 15, 6);

        // Corridor: room 1 to room 3 — vertical at x=7 from y=10 to y=13
        CarveRoom(map, 7, 10, 7, 13);

        // Corridor: room 2 to room 4 — vertical at x=22 from y=12 to y=14
        CarveRoom(map, 22, 12, 22, 14);

        // Corridor: room 3 to room 4 — horizontal at y=18 from x=14 to x=17
        CarveRoom(map, 14, 18, 17, 18);

        // Water tiles in room 3: (5,17) to (8,20)
        CarveRoom(map, 5, 17, 8, 20, TileType.Water);

        // Wall pillars in room 2
        map.SetTile(19, 6, TileType.Wall);
        map.SetTile(23, 8, TileType.Wall);

        return map;
    }
}

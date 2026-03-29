using RoguelikeEngine.Data;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

public struct NeighborContext
{
    public TerrainId N, NE, E, SE, S, SW, W, NW;
    public bool WallN, WallNE, WallE, WallSE, WallS, WallSW, WallW, WallNW;

    public static NeighborContext FromMap(TileMap map, int x, int y)
    {
        var ctx = new NeighborContext();
        SetNeighbor(map, x, y - 1, out ctx.N, out ctx.WallN);
        SetNeighbor(map, x + 1, y - 1, out ctx.NE, out ctx.WallNE);
        SetNeighbor(map, x + 1, y, out ctx.E, out ctx.WallE);
        SetNeighbor(map, x + 1, y + 1, out ctx.SE, out ctx.WallSE);
        SetNeighbor(map, x, y + 1, out ctx.S, out ctx.WallS);
        SetNeighbor(map, x - 1, y + 1, out ctx.SW, out ctx.WallSW);
        SetNeighbor(map, x - 1, y, out ctx.W, out ctx.WallW);
        SetNeighbor(map, x - 1, y - 1, out ctx.NW, out ctx.WallNW);
        return ctx;
    }

    private static void SetNeighbor(TileMap map, int x, int y, out TerrainId terrain, out bool wall)
    {
        var tile = map.GetTile(x, y);
        terrain = tile.Terrain;
        wall = tile.HasWall;
    }

    public long GetHash()
    {
        // Pack 8 terrain IDs (4 bits each = 32 bits) + 8 wall flags (8 bits) = 40 bits
        long h = (long)N | ((long)NE << 4) | ((long)E << 8) | ((long)SE << 12)
               | ((long)S << 16) | ((long)SW << 20) | ((long)W << 24) | ((long)NW << 28);
        long wallBits = (WallN ? 1L : 0) | (WallNE ? 2L : 0) | (WallE ? 4L : 0) | (WallSE ? 8L : 0)
                      | (WallS ? 16L : 0) | (WallSW ? 32L : 0) | (WallW ? 64L : 0) | (WallNW ? 128L : 0);
        return h | (wallBits << 32);
    }
}

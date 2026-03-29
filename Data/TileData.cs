namespace RoguelikeEngine.Data;

public struct TileData
{
    public TerrainId Terrain;
    public WallType Wall;
    public ushort VariantSeed;
    public ushort ZoneId; // 0 = no zone (outdoors)

    public TileData(TerrainId terrain, WallType wall = WallType.None, ushort variantSeed = 0, ushort zoneId = 0)
    {
        Terrain = terrain;
        Wall = wall;
        VariantSeed = variantSeed;
        ZoneId = zoneId;
    }

    public bool HasWall => Wall != WallType.None;
}

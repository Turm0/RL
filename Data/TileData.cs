namespace RoguelikeEngine.Data;

public struct TileData
{
    public TerrainId Terrain;
    public WallType Wall;
    public ushort VariantSeed;
    public ushort ZoneId; // 0 = no zone (outdoors)
    public WaterStyleId WaterStyle;
    public float WaterDepth; // 0..1, auto-computed from distance to shore

    public TileData(TerrainId terrain, WallType wall = WallType.None, ushort variantSeed = 0, ushort zoneId = 0)
    {
        Terrain = terrain;
        Wall = wall;
        VariantSeed = variantSeed;
        ZoneId = zoneId;
        WaterStyle = WaterStyleId.Clean;
        WaterDepth = 0f;
    }

    public bool HasWall => Wall != WallType.None;
}

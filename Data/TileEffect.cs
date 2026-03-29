namespace RoguelikeEngine.Data;

public struct TileEffect
{
    public TerrainEffectType Type;
    public float Intensity; // 0.0 (barely visible) to 1.0 (fully covered)

    public TileEffect(TerrainEffectType type, float intensity)
    {
        Type = type;
        Intensity = intensity;
    }
}

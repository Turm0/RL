using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public class WallDefinition
{
    public WallType Id;
    public string Name;
    public Color BaseColor;
    public Color HighlightColor;
    public Color VariantColorMin;
    public Color VariantColorMax;
    public float NoiseFrequency;
    public float NoiseAmplitude;
    public TerrainPattern Pattern;
}

using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public class TerrainDefinition
{
    public TerrainId Id;
    public string Name;
    public bool Walkable;
    public Color BaseColor;
    public Color VariantColorMin;
    public Color VariantColorMax;
    public float NoiseFrequency;
    public float NoiseAmplitude;
    public TerrainPattern Pattern;
    public byte TransitionPriority; // higher encroaches into lower
    public int PixelSize;          // pixelization block size (1 = none, 2 = chunky)
}

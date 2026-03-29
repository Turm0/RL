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
    public float NoiseAmplitude;
    public int BlockWidth;
    public int BlockHeight;
    public int MortarWidth;
    public bool Staggered;
    public float Roughness; // extra per-pixel noise (0 = smooth, higher = rougher)
    public int PixelSize;  // pixelization block size (1 = none, 2 = chunky)
}

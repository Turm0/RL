using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public enum RoofMaterialType : byte
{
    Thatch,
    ClayTile,
    Slate,
    WoodShingle
}

public class RoofMaterial
{
    public RoofMaterialType Type;
    public string Name;
    public Color BaseColor;
    public Color VariantColorMin;
    public Color VariantColorMax;
    public Color MortarColor;    // gap between tiles/shingles
    public int RowHeight;        // height of each row in pixels
    public int PieceWidth;       // width of each tile/shingle
    public bool Staggered;       // offset every other row
    public float Roughness;      // per-pixel noise amount
    public int PixelSize;         // pixelization block size (1 = no pixelization, 2 = 2x2 chunky)
}

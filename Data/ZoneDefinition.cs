using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public class ZoneDefinition
{
    public ushort Id;
    public bool HasRoof;
    public RoofMaterialType RoofMaterial;
    public Rectangle Bounds;
}

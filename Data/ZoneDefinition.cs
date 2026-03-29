using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public class ZoneDefinition
{
    public ushort Id;
    public bool HasRoof;
    public string RoofStyle; // "thatch", "stone_tiles", "wood_shingle"
    public Color RoofColor;
    public Rectangle Bounds;
}

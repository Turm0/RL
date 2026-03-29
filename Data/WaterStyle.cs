using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public enum WaterStyleId : byte
{
    Clean,
    Murky,
    Swamp,
    Crystal
}

public class WaterStyle
{
    public WaterStyleId Id;
    public string Name;
    public Color ShallowColor;
    public Color DeepColor;
    public Color HighlightColor;
}

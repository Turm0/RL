using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public static class WaterStyleRegistry
{
    private static readonly Dictionary<WaterStyleId, WaterStyle> _defs = new();

    public static WaterStyle Get(WaterStyleId id) => _defs[id];

    static WaterStyleRegistry()
    {
        Register(new WaterStyle
        {
            Id = WaterStyleId.Clean,
            Name = "Clean",
            ShallowColor = new Color(55, 85, 115),
            DeepColor = new Color(18, 35, 60),
            HighlightColor = new Color(140, 195, 235)
        });

        Register(new WaterStyle
        {
            Id = WaterStyleId.Murky,
            Name = "Murky",
            ShallowColor = new Color(50, 65, 55),
            DeepColor = new Color(22, 30, 25),
            HighlightColor = new Color(90, 110, 85)
        });

        Register(new WaterStyle
        {
            Id = WaterStyleId.Swamp,
            Name = "Swamp",
            ShallowColor = new Color(45, 62, 35),
            DeepColor = new Color(20, 32, 15),
            HighlightColor = new Color(75, 95, 55)
        });

        Register(new WaterStyle
        {
            Id = WaterStyleId.Crystal,
            Name = "Crystal",
            ShallowColor = new Color(70, 120, 140),
            DeepColor = new Color(25, 55, 80),
            HighlightColor = new Color(180, 220, 245)
        });
    }

    private static void Register(WaterStyle def) => _defs[def.Id] = def;
}

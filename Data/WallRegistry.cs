using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public static class WallRegistry
{
    private static readonly Dictionary<WallType, WallDefinition> _defs = new();

    public static WallDefinition Get(WallType id) => _defs[id];

    static WallRegistry()
    {
        Register(new WallDefinition
        {
            Id = WallType.StoneWall,
            Name = "Stone Wall",
            BaseColor = new Color(82, 78, 70),
            HighlightColor = new Color(100, 94, 84),
            VariantColorMin = new Color(75, 70, 62),
            VariantColorMax = new Color(90, 85, 76),
            NoiseAmplitude = 0.10f,
            BlockWidth = 14,
            BlockHeight = 8,
            MortarWidth = 1,
            Staggered = true,
            Roughness = 0f,
            PixelSize = 2
        });

        Register(new WallDefinition
        {
            Id = WallType.BrickWall,
            Name = "Brick Wall",
            BaseColor = new Color(100, 60, 48),
            HighlightColor = new Color(115, 75, 60),
            VariantColorMin = new Color(90, 52, 42),
            VariantColorMax = new Color(110, 68, 55),
            NoiseAmplitude = 0.12f,
            BlockWidth = 16,
            BlockHeight = 8,
            MortarWidth = 1,
            Staggered = true,
            Roughness = 0f,
            PixelSize = 2
        });

        Register(new WallDefinition
        {
            Id = WallType.CaveWall,
            Name = "Cave Wall",
            BaseColor = new Color(62, 56, 50),
            HighlightColor = new Color(76, 70, 62),
            VariantColorMin = new Color(52, 46, 40),
            VariantColorMax = new Color(70, 64, 56),
            NoiseAmplitude = 0.15f,
            BlockWidth = 9,
            BlockHeight = 8,
            MortarWidth = 1,
            Staggered = false,
            Roughness = 0.10f,
            PixelSize = 2
        });

        Register(new WallDefinition
        {
            Id = WallType.WoodWall,
            Name = "Wood Wall",
            BaseColor = new Color(80, 60, 38),
            HighlightColor = new Color(95, 72, 48),
            VariantColorMin = new Color(72, 54, 34),
            VariantColorMax = new Color(88, 66, 42),
            NoiseAmplitude = 0.10f,
            BlockWidth = 10,
            BlockHeight = 32,
            MortarWidth = 1,
            Staggered = false,
            Roughness = 0f,
            PixelSize = 2
        });
    }

    private static void Register(WallDefinition def) => _defs[def.Id] = def;
}

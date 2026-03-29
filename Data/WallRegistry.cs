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
            BaseColor = new Color(75, 70, 62),
            HighlightColor = new Color(95, 88, 78),
            VariantColorMin = new Color(68, 63, 55),
            VariantColorMax = new Color(82, 77, 68),
            NoiseFrequency = 0.08f,
            NoiseAmplitude = 0.12f,
            Pattern = TerrainPattern.Flat
        });

        Register(new WallDefinition
        {
            Id = WallType.BrickWall,
            Name = "Brick Wall",
            BaseColor = new Color(95, 55, 45),
            HighlightColor = new Color(110, 70, 58),
            VariantColorMin = new Color(85, 48, 38),
            VariantColorMax = new Color(105, 62, 50),
            NoiseFrequency = 0.15f,
            NoiseAmplitude = 0.15f,
            Pattern = TerrainPattern.Flat
        });

        Register(new WallDefinition
        {
            Id = WallType.CaveWall,
            Name = "Cave Wall",
            BaseColor = new Color(58, 52, 46),
            HighlightColor = new Color(72, 65, 58),
            VariantColorMin = new Color(48, 42, 36),
            VariantColorMax = new Color(65, 58, 52),
            NoiseFrequency = 0.12f,
            NoiseAmplitude = 0.25f,
            Pattern = TerrainPattern.Organic
        });

        Register(new WallDefinition
        {
            Id = WallType.WoodWall,
            Name = "Wood Wall",
            BaseColor = new Color(75, 55, 35),
            HighlightColor = new Color(90, 68, 45),
            VariantColorMin = new Color(65, 48, 30),
            VariantColorMax = new Color(85, 62, 40),
            NoiseFrequency = 0.18f,
            NoiseAmplitude = 0.12f,
            Pattern = TerrainPattern.Plank
        });
    }

    private static void Register(WallDefinition def) => _defs[def.Id] = def;
}

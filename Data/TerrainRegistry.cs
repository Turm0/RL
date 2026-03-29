using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public static class TerrainRegistry
{
    private static readonly Dictionary<TerrainId, TerrainDefinition> _defs = new();

    public static TerrainDefinition Get(TerrainId id) => _defs[id];

    static TerrainRegistry()
    {
        Register(new TerrainDefinition
        {
            Id = TerrainId.Stone,
            Name = "Stone",
            Walkable = true,
            BaseColor = new Color(78, 74, 68),
            VariantColorMin = new Color(72, 68, 62),
            VariantColorMax = new Color(85, 80, 74),
            NoiseFrequency = 0.06f,
            NoiseAmplitude = 0.12f,
            Pattern = TerrainPattern.Flat,
            TransitionPriority = 10,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.Dirt,
            Name = "Dirt",
            Walkable = true,
            BaseColor = new Color(88, 72, 52),
            VariantColorMin = new Color(82, 66, 46),
            VariantColorMax = new Color(95, 78, 58),
            NoiseFrequency = 0.08f,
            NoiseAmplitude = 0.12f,
            Pattern = TerrainPattern.Speckled,
            TransitionPriority = 45,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.Grass,
            Name = "Grass",
            Walkable = true,
            BaseColor = new Color(58, 78, 45),
            VariantColorMin = new Color(52, 72, 40),
            VariantColorMax = new Color(65, 85, 50),
            NoiseFrequency = 0.12f,
            NoiseAmplitude = 0.15f,
            Pattern = TerrainPattern.Organic,
            TransitionPriority = 40,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.Sand,
            Name = "Sand",
            Walkable = true,
            BaseColor = new Color(140, 125, 90),
            VariantColorMin = new Color(130, 115, 80),
            VariantColorMax = new Color(155, 140, 100),
            NoiseFrequency = 0.08f,
            NoiseAmplitude = 0.15f,
            Pattern = TerrainPattern.Speckled,
            TransitionPriority = 20,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.Water,
            Name = "Water",
            Walkable = true,
            BaseColor = new Color(30, 50, 70),
            VariantColorMin = new Color(25, 42, 62),
            VariantColorMax = new Color(38, 58, 80),
            NoiseFrequency = 0.12f,
            NoiseAmplitude = 0.25f,
            Pattern = TerrainPattern.Ripple,
            TransitionPriority = 60,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.DeepWater,
            Name = "Deep Water",
            Walkable = false,
            BaseColor = new Color(18, 32, 55),
            VariantColorMin = new Color(14, 26, 48),
            VariantColorMax = new Color(24, 40, 64),
            NoiseFrequency = 0.10f,
            NoiseAmplitude = 0.20f,
            Pattern = TerrainPattern.Ripple,
            TransitionPriority = 65,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.Lava,
            Name = "Lava",
            Walkable = false,
            BaseColor = new Color(120, 40, 20),
            VariantColorMin = new Color(100, 30, 12),
            VariantColorMax = new Color(150, 55, 25),
            NoiseFrequency = 0.10f,
            NoiseAmplitude = 0.30f,
            Pattern = TerrainPattern.Ripple,
            TransitionPriority = 70,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.Ice,
            Name = "Ice",
            Walkable = true,
            BaseColor = new Color(160, 190, 210),
            VariantColorMin = new Color(145, 178, 200),
            VariantColorMax = new Color(180, 205, 225),
            NoiseFrequency = 0.05f,
            NoiseAmplitude = 0.10f,
            Pattern = TerrainPattern.Flat,
            TransitionPriority = 15,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.Wood,
            Name = "Wood",
            Walkable = true,
            BaseColor = new Color(85, 60, 38),
            VariantColorMin = new Color(75, 52, 32),
            VariantColorMax = new Color(95, 68, 44),
            NoiseFrequency = 0.20f,
            NoiseAmplitude = 0.15f,
            Pattern = TerrainPattern.Plank,
            TransitionPriority = 5,
            PixelSize = 2
        });

        Register(new TerrainDefinition
        {
            Id = TerrainId.CaveFloor,
            Name = "Cave Floor",
            Walkable = true,
            BaseColor = new Color(50, 45, 40),
            VariantColorMin = new Color(42, 38, 34),
            VariantColorMax = new Color(58, 52, 46),
            NoiseFrequency = 0.12f,
            NoiseAmplitude = 0.20f,
            Pattern = TerrainPattern.Speckled,
            TransitionPriority = 12,
            PixelSize = 2
        });
    }

    private static void Register(TerrainDefinition def) => _defs[def.Id] = def;
}

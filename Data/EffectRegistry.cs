using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public static class EffectRegistry
{
    private static readonly Dictionary<TerrainEffectType, EffectDefinition> _defs = new();

    public static EffectDefinition Get(TerrainEffectType type) => _defs[type];

    static EffectRegistry()
    {
        Register(new EffectDefinition
        {
            Type = TerrainEffectType.Wet,
            Name = "Wet",
            TintColor = new Color(40, 60, 90),
            OverlayPattern = TerrainPattern.Speckled
        });

        Register(new EffectDefinition
        {
            Type = TerrainEffectType.Snow,
            Name = "Snow",
            TintColor = new Color(220, 225, 235),
            OverlayPattern = TerrainPattern.Organic
        });

        Register(new EffectDefinition
        {
            Type = TerrainEffectType.Dust,
            Name = "Dust",
            TintColor = new Color(140, 120, 80),
            OverlayPattern = TerrainPattern.Speckled
        });

        Register(new EffectDefinition
        {
            Type = TerrainEffectType.Moss,
            Name = "Moss",
            TintColor = new Color(50, 80, 35),
            OverlayPattern = TerrainPattern.Organic
        });

        Register(new EffectDefinition
        {
            Type = TerrainEffectType.Blood,
            Name = "Blood",
            TintColor = new Color(100, 15, 10),
            OverlayPattern = TerrainPattern.Speckled
        });

        Register(new EffectDefinition
        {
            Type = TerrainEffectType.Scorched,
            Name = "Scorched",
            TintColor = new Color(20, 18, 15),
            OverlayPattern = TerrainPattern.Organic
        });
    }

    private static void Register(EffectDefinition def) => _defs[def.Type] = def;
}

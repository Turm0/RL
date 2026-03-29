using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.Data;

public static class RoofMaterialRegistry
{
    private static readonly Dictionary<RoofMaterialType, RoofMaterial> _defs = new();

    public static RoofMaterial Get(RoofMaterialType type) => _defs[type];

    static RoofMaterialRegistry()
    {
        // Thatch — bundled straw rows
        Register(new RoofMaterial
        {
            Type = RoofMaterialType.Thatch,
            Name = "Thatch",
            BaseColor = new Color(178, 152, 88),
            VariantColorMin = new Color(158, 132, 72),
            VariantColorMax = new Color(195, 168, 100),
            MortarColor = new Color(128, 105, 62),
            RowHeight = 7,
            PieceWidth = 32,
            Staggered = false,
            Roughness = 0.06f,
            PixelSize = 2
        });

        // Clay tile — curved terracotta
        Register(new RoofMaterial
        {
            Type = RoofMaterialType.ClayTile,
            Name = "Clay Tile",
            BaseColor = new Color(150, 78, 45),
            VariantColorMin = new Color(120, 55, 30),
            VariantColorMax = new Color(175, 95, 58),
            MortarColor = new Color(70, 38, 22),
            RowHeight = 7,
            PieceWidth = 5,
            Staggered = true,
            Roughness = 0.02f,
            PixelSize = 2
        });

        // Slate — dark gray-blue stone
        Register(new RoofMaterial
        {
            Type = RoofMaterialType.Slate,
            Name = "Slate",
            BaseColor = new Color(72, 72, 80),
            VariantColorMin = new Color(55, 55, 65),
            VariantColorMax = new Color(92, 92, 100),
            MortarColor = new Color(30, 30, 35),
            RowHeight = 10,
            PieceWidth = 8,
            Staggered = true,
            Roughness = 0.03f,
            PixelSize = 2
        });

        // Wood shingle — cedar shakes, warm browns
        Register(new RoofMaterial
        {
            Type = RoofMaterialType.WoodShingle,
            Name = "Wood Shingle",
            BaseColor = new Color(145, 115, 78),
            VariantColorMin = new Color(128, 100, 68),
            VariantColorMax = new Color(165, 132, 90),
            MortarColor = new Color(100, 78, 52),
            RowHeight = 14,
            PieceWidth = 11,
            Staggered = true,
            Roughness = 0.01f,
            PixelSize = 2
        });

        // Cave stone — natural rough rock ceiling
        Register(new RoofMaterial
        {
            Type = RoofMaterialType.CaveStone,
            Name = "Cave Stone",
            BaseColor = new Color(82, 76, 68),
            VariantColorMin = new Color(65, 60, 52),
            VariantColorMax = new Color(98, 92, 82),
            MortarColor = new Color(50, 45, 38),
            RowHeight = 32,  // no visible rows — full tile
            PieceWidth = 32, // no visible pieces — full tile
            Staggered = false,
            Roughness = 0.08f,
            PixelSize = 2
        });
    }

    private static void Register(RoofMaterial def) => _defs[def.Type] = def;
}

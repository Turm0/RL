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
            BaseColor = new Color(145, 120, 65),
            VariantColorMin = new Color(115, 90, 42),
            VariantColorMax = new Color(165, 140, 80),
            MortarColor = new Color(85, 65, 32),
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
            BaseColor = new Color(110, 82, 52),
            VariantColorMin = new Color(85, 62, 38),
            VariantColorMax = new Color(135, 105, 68),
            MortarColor = new Color(45, 32, 18),
            RowHeight = 12,
            PieceWidth = 9,
            Staggered = true,
            Roughness = 0.03f,
            PixelSize = 2
        });
    }

    private static void Register(RoofMaterial def) => _defs[def.Type] = def;
}

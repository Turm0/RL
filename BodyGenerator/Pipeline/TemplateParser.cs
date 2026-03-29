using System;
using System.Collections.Generic;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public static class TemplateParser
{
    // CRITICAL: case-sensitive mapping
    // lowercase s = SkinShadow, UPPERCASE S = ClothShadow
    // lowercase l = LegShadow, UPPERCASE L = LegBase
    public static List<TemplatePixel> Parse(string pixelString)
    {
        var pixels = new List<TemplatePixel>();
        int x = 0;
        int y = 0;

        foreach (char c in pixelString)
        {
            if (c == '\n' || c == '\r')
            {
                if (c == '\n')
                {
                    y++;
                    x = 0;
                }
                continue;
            }

            if (c != '.')
            {
                var role = CharToRole(c);
                if (role.HasValue)
                {
                    pixels.Add(new TemplatePixel { X = x, Y = y, Role = role.Value });
                }
            }

            x++;
        }

        return pixels;
    }

    private static ColorRole? CharToRole(char c)
    {
        return c switch
        {
            'h' => ColorRole.SkinBase,
            's' => ColorRole.SkinShadow,       // lowercase s = skin shadow
            'H' => ColorRole.SkinHighlight,
            'E' => ColorRole.Eye,
            'C' => ColorRole.ClothBase,
            'S' => ColorRole.ClothShadow,       // UPPERCASE S = cloth shadow
            'G' => ColorRole.ClothHighlight,
            'L' => ColorRole.LegBase,           // UPPERCASE L = leg base
            'l' => ColorRole.LegShadow,         // lowercase l = leg shadow
            'B' => ColorRole.Boot,
            'b' => ColorRole.Belt,
            'O' => ColorRole.Outline,
            _ => null
        };
    }
}

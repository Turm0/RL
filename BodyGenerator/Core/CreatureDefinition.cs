using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace BodyGenerator.Core;

public class CreatureDefinition
{
    public string Name;
    public string BodyTemplate;
    public string Pose;
    public Dictionary<ColorRole, Color> Palette;
}

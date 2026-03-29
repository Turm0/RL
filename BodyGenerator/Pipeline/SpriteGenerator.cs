using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public class SpriteGenerator
{
    private readonly string _contentRoot;

    public SpriteGenerator(string contentRoot)
    {
        _contentRoot = contentRoot;
    }

    public Texture2D Generate(GraphicsDevice device, string creaturePath, string poseOverride = null, bool outline = true)
    {
        var creature = YamlLoader.LoadCreature(Path.Combine(_contentRoot, creaturePath));
        return Generate(device, creature, poseOverride, outline);
    }

    public Texture2D Generate(GraphicsDevice device, CreatureDefinition creature, string poseOverride = null, bool outline = true)
    {
        string bodyPath = Path.Combine(_contentRoot, "bodies", creature.BodyTemplate + ".yaml");
        string poseName = poseOverride ?? creature.Pose;
        string posePath = Path.Combine(_contentRoot, "poses", poseName + ".yaml");

        var body = YamlLoader.LoadBodyTemplate(bodyPath);
        var pose = YamlLoader.LoadPose(posePath);

        var buffer = new PixelBuffer(body.SpriteSize, body.SpriteSize);

        var partPositions = JointResolver.Resolve(body, pose);
        SpriteCompositor.Composite(buffer, body, pose, partPositions, creature.Palette);

        if (outline && creature.Palette.TryGetValue(ColorRole.Outline, out var outlineColor))
        {
            OutlinePass.Apply(buffer, outlineColor);
        }

        return buffer.ToTexture2D(device);
    }
}

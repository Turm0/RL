using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using BodyGenerator.Core;

namespace BodyGenerator.Pipeline;

public class SpriteGenerator
{
    private readonly string _contentRoot;
    private readonly ObjectSpriteGenerator _objectGen;

    public SpriteGenerator(string contentRoot)
    {
        _contentRoot = contentRoot;
        _objectGen = new ObjectSpriteGenerator(contentRoot);
    }

    public Texture2D Generate(GraphicsDevice device, string creaturePath, string poseOverride = null, bool outline = true)
    {
        var creature = YamlLoader.LoadCreature(Path.Combine(_contentRoot, creaturePath));
        return Generate(device, creature, poseOverride, outline);
    }

    public Texture2D Generate(GraphicsDevice device, CreatureDefinition creature, string poseOverride = null, bool outline = true)
    {
        // Auto-convert creature YAML attachments to AttachmentData
        List<AttachmentData> attachments = null;
        if (creature.Attachments != null && creature.Attachments.Count > 0)
        {
            attachments = new List<AttachmentData>();
            foreach (var ca in creature.Attachments)
                attachments.Add(new AttachmentData
                {
                    ObjectPath = ca.ObjectPath,
                    Joint = ca.Joint,
                    ZOrder = ca.ZOrder,
                    MaterialOverrides = ca.MaterialOverrides
                });
        }
        return GenerateWithAttachments(device, creature, null, attachments, poseOverride, outline);
    }

    /// <summary>
    /// Generates a creature sprite with optional appearance attachments and palette overrides.
    /// </summary>
    public Texture2D GenerateWithAttachments(GraphicsDevice device, CreatureDefinition creature,
        Dictionary<ColorRole, Color> paletteOverrides, List<AttachmentData> attachments,
        string poseOverride = null, bool outline = true)
    {
        string bodyPath = Path.Combine(_contentRoot, "bodies", creature.BodyTemplate + ".yaml");
        string poseName = poseOverride ?? creature.Pose;
        string posePath = Path.Combine(_contentRoot, "poses", poseName + ".yaml");

        var body = YamlLoader.LoadBodyTemplate(bodyPath);
        var pose = YamlLoader.LoadPose(posePath);

        var buffer = new PixelBuffer(body.SpriteSize, body.SpriteSize);

        // Build effective palette (base + overrides)
        var palette = new Dictionary<ColorRole, Color>(creature.Palette);
        if (paletteOverrides != null)
            foreach (var kv in paletteOverrides)
                palette[kv.Key] = kv.Value;

        // Resolve skeleton
        var partPositions = JointResolver.Resolve(body, pose);

        // Composite base body
        SpriteCompositor.Composite(buffer, body, pose, partPositions, palette);

        // Render attachments
        if (attachments != null)
        {
            foreach (var attachment in attachments)
            {
                // Load the object definition
                var objDef = _objectGen.GetDefinition(attachment.ObjectPath);

                // Find joint world position
                Point jointPos = attachment.Joint != null
                    ? FindJointPosition(body, partPositions, attachment.Joint)
                    : Point.Zero;

                // Position attachment using its anchor: joint position - anchor = top-left
                int anchorX = objDef.Anchor.X;
                int anchorY = objDef.Anchor.Y;
                int attachX = jointPos.X - anchorX + attachment.OffsetX;
                int attachY = jointPos.Y - anchorY + attachment.OffsetY;
                var shapes = objDef.Frames.Count > 0 ? objDef.Frames[0] : new List<ShapeDef>();

                // Apply material overrides
                var materials = new Dictionary<string, MaterialDef>(objDef.Materials);
                if (attachment.MaterialOverrides != null)
                {
                    foreach (var kv in attachment.MaterialOverrides)
                    {
                        if (materials.ContainsKey(kv.Key))
                            materials[kv.Key] = new MaterialDef { Color = kv.Value, Alpha = materials[kv.Key].Alpha };
                        else
                            materials[kv.Key] = new MaterialDef { Color = kv.Value, Alpha = 255 };
                    }
                }

                // Render shapes into the buffer
                for (int i = 0; i < shapes.Count; i++)
                {
                    var shape = shapes[i];
                    MaterialDef material = null;
                    if (shape.Material != null)
                        materials.TryGetValue(shape.Material, out material);
                    ObjectShapeRenderer.RenderShape(buffer, shape, material, materials,
                        attachment.ZOrder + i, attachX, attachY);
                }
            }
        }

        // Outline runs after everything
        Color outlineCol = palette.TryGetValue(ColorRole.Outline, out var oc) ? oc : new Color(26, 26, 42);
        if (outline)
            OutlinePass.Apply(buffer, outlineCol);

        buffer.CenterContent();
        return buffer.ToTexture2D(device);
    }

    private static Point FindJointPosition(BodyTemplate body, Dictionary<string, Point> partPositions,
        string jointName)
    {
        // Search all parts for a joint with this name
        foreach (var kv in body.Parts)
        {
            if (kv.Value.Joints.TryGetValue(jointName, out var localJoint))
            {
                if (partPositions.TryGetValue(kv.Key, out var partPos))
                    return new Point(partPos.X + localJoint.X, partPos.Y + localJoint.Y);
            }
        }
        return Point.Zero;
    }
}

/// <summary>
/// Attachment data passed to the sprite generator (decoupled from ECS).
/// </summary>
public class AttachmentData
{
    public string ObjectPath;
    public string Joint;
    public int OffsetX, OffsetY;
    public int ZOrder;
    public Dictionary<string, Color> MaterialOverrides;
}

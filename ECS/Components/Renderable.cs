using Microsoft.Xna.Framework;

namespace RoguelikeEngine.ECS.Components;

/// <summary>
/// Placeholder renderable component storing a color for the entity.
/// Will be replaced in Phase 2 with sprite-based rendering.
/// </summary>
public struct Renderable
{
    public Color Color;

    public Renderable(Color color)
    {
        Color = color;
    }
}

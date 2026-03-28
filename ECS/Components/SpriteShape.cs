namespace RoguelikeEngine.ECS.Components;

/// <summary>
/// Defines the visual shape of an entity, used by VectorRasterizer to generate textures.
/// </summary>
public struct SpriteShape
{
    /// <summary>Creature type key, e.g. "player", "goblin", "rat".</summary>
    public string CreatureType;

    /// <summary>Size multiplier. 0.45 for rat, 1.0 for player, 1.4 for dragon.</summary>
    public float Size;

    public SpriteShape(string creatureType, float size)
    {
        CreatureType = creatureType;
        Size = size;
    }
}

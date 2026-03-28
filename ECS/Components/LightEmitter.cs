using Microsoft.Xna.Framework;

namespace RoguelikeEngine.ECS.Components;

/// <summary>
/// Makes an entity emit light. Used by LightingSystem for shadowcasting.
/// </summary>
public struct LightEmitter
{
    /// <summary>Light radius in tiles.</summary>
    public float Radius;

    /// <summary>Base brightness, 0–1.</summary>
    public float Intensity;

    /// <summary>RGB multipliers, e.g. (1.3, 1.0, 0.65) for warm torch.</summary>
    public Vector3 Color;

    /// <summary>Whether to apply flicker modulation.</summary>
    public bool Flicker;

    /// <summary>How much intensity varies when flickering, 0–1.</summary>
    public float FlickerIntensity;

    public LightEmitter(float radius, float intensity, Vector3 color, bool flicker = false, float flickerIntensity = 0f)
    {
        Radius = radius;
        Intensity = intensity;
        Color = color;
        Flicker = flicker;
        FlickerIntensity = flickerIntensity;
    }
}

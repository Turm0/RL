using System;

namespace RoguelikeEngine.World;

public enum WeatherType : byte
{
    Clear,
    Rain,
    Thunderstorm,
    Snow
}

/// <summary>
/// Current weather conditions. Read by renderers and effect systems.
/// Managed by WeatherSystem which handles transitions.
/// </summary>
public class WeatherState
{
    /// <summary>Active weather type.</summary>
    public WeatherType Type { get; private set; } = WeatherType.Clear;

    /// <summary>Precipitation intensity (0-1). Drives particle density and terrain effects.</summary>
    public float Intensity { get; private set; }

    /// <summary>Wind direction in radians (0 = east, PI/2 = south). Affects particle drift.</summary>
    public float WindAngle { get; private set; }

    /// <summary>Wind strength (0-1). Affects particle speed and angle.</summary>
    public float WindStrength { get; private set; }

    /// <summary>Lightning flash brightness (0-1), decays quickly after a strike.</summary>
    public float LightningFlash { get; private set; }

    /// <summary>True while actively transitioning between weather types.</summary>
    public bool Transitioning { get; private set; }

    // Transition state
    private WeatherType _targetType;
    private float _targetIntensity;
    private float _transitionProgress;
    private float _transitionDuration;
    private float _startIntensity;

    // Lightning timing
    private float _nextLightningIn;
    private readonly Random _rng = new();

    /// <summary>
    /// Immediately sets weather without transition.
    /// </summary>
    public void Set(WeatherType type, float intensity)
    {
        Type = type;
        Intensity = Math.Clamp(intensity, 0f, 1f);
        _targetType = type;
        _targetIntensity = Intensity;
        Transitioning = false;
        _transitionProgress = 0f;
    }

    /// <summary>
    /// Begins a smooth transition to a new weather state.
    /// </summary>
    public void TransitionTo(WeatherType type, float intensity, float durationSeconds = 10f)
    {
        _targetType = type;
        _targetIntensity = Math.Clamp(intensity, 0f, 1f);
        _transitionDuration = Math.Max(durationSeconds, 0.01f);
        _transitionProgress = 0f;
        _startIntensity = Intensity;
        Transitioning = true;

        // If changing type, we fade out → swap type → fade in
        // If same type, just lerp intensity
        if (type == Type)
            Transitioning = _targetIntensity != Intensity;
    }

    public void Update(float deltaSeconds)
    {
        // Transition
        if (Transitioning)
        {
            _transitionProgress += deltaSeconds / _transitionDuration;
            if (_transitionProgress >= 1f)
            {
                _transitionProgress = 1f;
                Transitioning = false;
                Type = _targetType;
                Intensity = _targetIntensity;
            }
            else if (_targetType != Type)
            {
                // Fade out old, then fade in new at halfway point
                if (_transitionProgress < 0.5f)
                {
                    Intensity = _startIntensity * (1f - _transitionProgress * 2f);
                }
                else
                {
                    if (Type != _targetType)
                        Type = _targetType;
                    Intensity = _targetIntensity * ((_transitionProgress - 0.5f) * 2f);
                }
            }
            else
            {
                // Same type, just lerp intensity
                float t = _transitionProgress;
                t = t * t * (3f - 2f * t); // smoothstep
                Intensity = _startIntensity + (_targetIntensity - _startIntensity) * t;
            }
        }

        // Lightning for thunderstorms
        if (Type == WeatherType.Thunderstorm && Intensity > 0.2f)
        {
            _nextLightningIn -= deltaSeconds;
            if (_nextLightningIn <= 0f)
            {
                LightningFlash = 0.8f + (float)_rng.NextDouble() * 0.2f;
                // Next strike: 2-8 seconds, shorter intervals at higher intensity
                float maxInterval = 10f - Intensity * 6f;
                _nextLightningIn = 2f + (float)_rng.NextDouble() * maxInterval;
            }
        }

        // Decay lightning flash
        if (LightningFlash > 0f)
        {
            LightningFlash -= deltaSeconds * 4f; // ~0.25 second flash
            if (LightningFlash < 0f) LightningFlash = 0f;
        }

        // Gentle wind drift
        WindAngle += (float)(_rng.NextDouble() - 0.5) * 0.1f * deltaSeconds;
        float targetWind = Type == WeatherType.Thunderstorm ? 0.6f + Intensity * 0.4f
            : Type == WeatherType.Clear ? 0.05f
            : 0.1f + Intensity * 0.3f;
        WindStrength += (targetWind - WindStrength) * deltaSeconds * 0.5f;
    }
}

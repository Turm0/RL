using System;
using Microsoft.Xna.Framework;

namespace RoguelikeEngine.World;

/// <summary>
/// Drives weather transitions based on the world clock and season.
/// Supports manual override cycling for testing.
/// </summary>
public class WeatherSystem
{
    private readonly WorldClock _clock;
    private readonly WeatherState _state;
    private readonly Random _rng = new();

    // Auto-progression
    private float _nextChangeIn;
    private bool _manualOverride;

    // Season-based weather weights: [Clear, Rain, Thunderstorm, Snow]
    private static readonly float[][] SeasonWeights =
    {
        new[] { 0.40f, 0.35f, 0.10f, 0.15f }, // Spring
        new[] { 0.55f, 0.20f, 0.20f, 0.05f }, // Summer
        new[] { 0.30f, 0.40f, 0.15f, 0.15f }, // Autumn
        new[] { 0.25f, 0.10f, 0.05f, 0.60f }, // Winter
    };

    private static readonly (WeatherType Type, float Intensity)[] ManualModes =
    {
        (WeatherType.Clear,        0.0f),
        (WeatherType.Rain,         0.3f),
        (WeatherType.Rain,         0.7f),
        (WeatherType.Rain,         1.0f),
        (WeatherType.Thunderstorm, 0.6f),
        (WeatherType.Thunderstorm, 1.0f),
        (WeatherType.Snow,         0.3f),
        (WeatherType.Snow,         0.7f),
        (WeatherType.Snow,         1.0f),
    };
    private int _manualModeIndex;

    public WeatherState State => _state;

    public WeatherSystem(WorldClock clock)
    {
        _clock = clock;
        _state = new WeatherState();
        _state.Set(WeatherType.Clear, 0f);
        _nextChangeIn = 60f + (float)_rng.NextDouble() * 120f;
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _state.Update(dt);

        if (_manualOverride)
            return;

        float gameMinutes = dt / _clock.TimeScale;
        _nextChangeIn -= gameMinutes;

        if (_nextChangeIn <= 0f)
        {
            RollNewWeather();
            _nextChangeIn = 30f + (float)_rng.NextDouble() * 150f;
        }
    }

    public void CycleManual()
    {
        _manualOverride = true;
        _manualModeIndex = (_manualModeIndex + 1) % ManualModes.Length;
        var mode = ManualModes[_manualModeIndex];
        _state.TransitionTo(mode.Type, mode.Intensity, 2f);
    }

    public void EnableAuto()
    {
        _manualOverride = false;
        _nextChangeIn = 10f + (float)_rng.NextDouble() * 30f;
    }

    private void RollNewWeather()
    {
        var weights = SeasonWeights[(int)_clock.Season];
        float roll = (float)_rng.NextDouble();
        float cumulative = 0f;
        WeatherType picked = WeatherType.Clear;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                picked = (WeatherType)i;
                break;
            }
        }

        float intensity = picked == WeatherType.Clear
            ? 0f
            : 0.2f + (float)_rng.NextDouble() * 0.8f;

        float transitionTime = 8f + (float)_rng.NextDouble() * 12f;
        _state.TransitionTo(picked, intensity, transitionTime);
    }
}

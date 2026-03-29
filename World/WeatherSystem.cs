using System;
using Microsoft.Xna.Framework;
using RoguelikeEngine.Data;
using RoguelikeEngine.Rendering;

namespace RoguelikeEngine.World;

/// <summary>
/// Dynamically changes terrain effect intensities over time.
/// Currently drives a snow cycle in a specified region.
/// </summary>
public class WeatherSystem
{
    private readonly TileMap _map;
    private readonly EffectOverlayRenderer _overlayRenderer;

    // Snow cycle region
    private readonly int _snowX1, _snowY1, _snowX2, _snowY2;
    private float _snowPhase;
    private float _lastSnowIntensity = -1f;

    // Cycle period in seconds (full increase + decrease)
    private const float CyclePeriod = 30f;

    public WeatherSystem(TileMap map, EffectOverlayRenderer overlayRenderer,
        int snowX1, int snowY1, int snowX2, int snowY2)
    {
        _map = map;
        _overlayRenderer = overlayRenderer;
        _snowX1 = snowX1;
        _snowY1 = snowY1;
        _snowX2 = snowX2;
        _snowY2 = snowY2;
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _snowPhase += dt / CyclePeriod;
        if (_snowPhase > 1f) _snowPhase -= 1f;

        // Triangle wave: 0→1→0 over one cycle
        float snowIntensity = _snowPhase < 0.5f
            ? _snowPhase * 2f
            : 2f - _snowPhase * 2f;

        // Quantize to 0.05 steps to avoid constant cache invalidation
        float quantized = MathF.Round(snowIntensity * 20f) / 20f;
        if (Math.Abs(quantized - _lastSnowIntensity) < 0.01f) return;

        _lastSnowIntensity = quantized;

        // Update snow intensity across the region
        for (int x = _snowX1; x <= _snowX2; x++)
        {
            for (int y = _snowY1; y <= _snowY2; y++)
            {
                if (!_map.IsInBounds(x, y)) continue;
                var tile = _map.GetTile(x, y);
                if (tile.HasWall) continue;

                // Slight spatial variation so it doesn't look uniform
                float noiseOffset = ((x * 13 + y * 7) & 0xF) / 32f; // 0..0.5
                float localIntensity = Math.Clamp(quantized + noiseOffset - 0.15f, 0f, 0.95f);

                _map.SetEffect(x, y, TerrainEffectType.Snow, localIntensity);
            }
        }

        // Invalidate overlay cache since intensities changed
        _overlayRenderer.InvalidateAll();
    }
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Renders weather overlays: rain ripple puddles + diagonal streaks,
/// snow flakes, atmosphere tint, and lightning flash.
/// All composited into a single texture per frame.
/// </summary>
public class WeatherRenderer
{
    private const int MaxParticles = 1500;

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _activeCount;
    private Texture2D _pixel;
    private Texture2D _particleTexture;
    private Color[] _particlePixels;
    private int _texWidth, _texHeight;
    private readonly Random _rng = new();

    private float _atmosphereFade;
    private int _frameCount;

    // Rain puddle pool — simple rotating buffer of random positions
    private const int MaxRippleSlots = 32;
    private readonly RippleSlot[] _rippleSlots = new RippleSlot[MaxRippleSlots];
    private int _rippleSlotCount;

    private struct RippleSlot
    {
        public float WorldX, WorldY;
        public int BirthFrame;
        public int Interval;  // frames per ripple cycle (10-16)
        public int RadiusX;   // 3-6
        public int RadiusY;
    }

    private const byte TypeStreak = 0;
    private const byte TypeFlake = 1;
    private const byte TypeSettled = 2;

    private struct Particle
    {
        public float WorldX, WorldY;
        public int ScreenX, ScreenY;
        public float Life, MaxLife;
        public float DriftX, DriftY;
        public byte Size, Type;
    }

    public void Initialize(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    private void EnsureTexture(GraphicsDevice device, int width, int height)
    {
        if (_particleTexture != null && _texWidth == width && _texHeight == height)
            return;
        _particleTexture?.Dispose();
        _texWidth = width;
        _texHeight = height;
        _particleTexture = new Texture2D(device, width, height);
        _particlePixels = new Color[width * height];
    }

    public void Update(float deltaSeconds, WeatherState weather, Camera camera,
        int viewportWidth, int viewportHeight, TileMap map)
    {
        bool weatherActive = weather.Intensity > 0.01f && weather.Type != WeatherType.Clear;
        float fadeTarget = weatherActive ? weather.Intensity : 0f;
        float fadeSpeed = 1f;
        _atmosphereFade = _atmosphereFade < fadeTarget
            ? Math.Min(_atmosphereFade + fadeSpeed * deltaSeconds, fadeTarget)
            : Math.Max(_atmosphereFade - fadeSpeed * deltaSeconds, fadeTarget);

        _frameCount++;

        float intensity = weather.Intensity;
        bool isRain = weather.Type == WeatherType.Rain || weather.Type == WeatherType.Thunderstorm;
        bool isSnow = weather.Type == WeatherType.Snow;

        int tileSize = GameConfig.TileSize;
        float camX = camera.Position.X;
        float camY = camera.Position.Y;
        float worldLeft = camX;
        float worldTop = camY;
        float worldRight = camX + viewportWidth;
        float worldBottom = camY + viewportHeight;

        // --- Manage rain puddle pool ---
        if (isRain && intensity > 0.01f)
        {
            int targetCount = Math.Min(MaxRippleSlots, 4 + (int)(intensity * 28));

            // Fill up to target
            while (_rippleSlotCount < targetCount)
            {
                SpawnRippleSlot(worldLeft, worldTop, worldRight, worldBottom, map, tileSize);
            }

            // Relocate slots whose ripple cycle has finished
            for (int i = 0; i < _rippleSlotCount; i++)
            {
                ref var slot = ref _rippleSlots[i];
                if (_frameCount - slot.BirthFrame >= slot.Interval)
                    RelocateSlot(ref slot, worldLeft, worldTop, worldRight, worldBottom, map, tileSize);
            }
        }
        else
        {
            // Rain stopped — drain pool
            if (_rippleSlotCount > 0)
                _rippleSlotCount = Math.Max(0, _rippleSlotCount - 1);
        }

        if (intensity < 0.01f && _activeCount == 0 && _rippleSlotCount == 0)
            return;

        float windDx = MathF.Cos(weather.WindAngle) * weather.WindStrength;
        float windDy = MathF.Sin(weather.WindAngle) * weather.WindStrength;

        // --- Rain streaks ---
        if (isRain && intensity > 0.01f)
        {
            int tilesWide = viewportWidth / tileSize + 2;
            int tilesHigh = viewportHeight / tileSize + 2;
            int visibleTileCount = tilesWide * tilesHigh;
            int streakCount = (int)(intensity * visibleTileCount * 0.02f);
            for (int i = 0; i < streakCount && _activeCount < MaxParticles; i++)
            {
                ref var p = ref _particles[_activeCount++];
                p.WorldX = worldLeft + (float)_rng.NextDouble() * (worldRight - worldLeft);
                p.WorldY = worldTop + (float)_rng.NextDouble() * (worldBottom - worldTop);
                p.ScreenX = (int)(p.WorldX - camX);
                p.ScreenY = (int)(p.WorldY - camY);
                p.Type = TypeStreak;
                p.MaxLife = p.Life = 0.033f + (float)_rng.NextDouble() * 0.017f;
                p.Size = (byte)(3 + _rng.Next(3));
                p.DriftX = windDx;
                p.DriftY = windDy;
            }
        }

        // --- Snow ---
        if (isSnow && intensity > 0.01f)
        {
            float snowSpawnRate = intensity * 200f;
            float toSpawn = snowSpawnRate * deltaSeconds;
            int spawnCount = (int)toSpawn;
            if (_rng.NextDouble() < toSpawn - spawnCount) spawnCount++;

            for (int i = 0; i < spawnCount && _activeCount < MaxParticles; i++)
            {
                ref var p = ref _particles[_activeCount++];
                p.WorldX = worldLeft + (float)_rng.NextDouble() * (worldRight - worldLeft);
                p.WorldY = worldTop + (float)_rng.NextDouble() * (worldBottom - worldTop);
                p.ScreenX = (int)(p.WorldX - camX);
                p.ScreenY = (int)(p.WorldY - camY);
                bool settled = _rng.NextDouble() < 0.15;
                p.Type = settled ? TypeSettled : TypeFlake;
                p.MaxLife = p.Life = settled
                    ? 1.5f + (float)_rng.NextDouble() * 2f
                    : 0.8f + (float)_rng.NextDouble() * 1.2f;
                p.Size = (byte)(1 + _rng.Next(3));
                p.DriftX = windDx * 20f + ((float)_rng.NextDouble() - 0.5f) * 8f;
                p.DriftY = 25f + (float)_rng.NextDouble() * 20f + windDy * 15f;
            }
        }

        // --- Update particles ---
        for (int i = _activeCount - 1; i >= 0; i--)
        {
            ref var p = ref _particles[i];
            if (p.Type == TypeFlake || p.Type == TypeSettled)
            {
                p.WorldX += p.DriftX * deltaSeconds;
                p.WorldY += p.DriftY * deltaSeconds;
                if (p.Type == TypeFlake)
                    p.WorldX += MathF.Sin(p.Life * 3f) * 2.5f * deltaSeconds;
            }
            p.ScreenX = (int)(p.WorldX - camX);
            p.ScreenY = (int)(p.WorldY - camY);
            p.Life -= deltaSeconds;

            if (p.Life <= 0f ||
                p.ScreenX < -16 || p.ScreenX >= viewportWidth + 16 ||
                p.ScreenY < -16 || p.ScreenY >= viewportHeight + 16)
            {
                _particles[i] = _particles[--_activeCount];
            }
        }
    }

    /// <summary>
    /// Draws rain puddles + ripples. Call BEFORE lighting so they get lit.
    /// </summary>
    public void DrawGroundEffects(SpriteBatch spriteBatch, WeatherState weather, Camera camera,
        TileMap map, ushort playerZoneId, FogOfWar fow)
    {
        if (_rippleSlotCount == 0) return;

        var device = spriteBatch.GraphicsDevice;
        int vpW = device.Viewport.Width;
        int vpH = device.Viewport.Height;

        EnsureTexture(device, vpW, vpH);
        Array.Clear(_particlePixels, 0, _particlePixels.Length);

        int tileSize = GameConfig.TileSize;
        float camPosX = camera.Position.X;
        float camPosY = camera.Position.Y;
        float invTS = 1f / tileSize;

        for (int i = 0; i < _rippleSlotCount; i++)
        {
            ref var slot = ref _rippleSlots[i];
            // Snap to 2px grid for pixel-art consistency
            int sx = (int)(slot.WorldX - camPosX) & ~1;
            int sy = (int)(slot.WorldY - camPosY) & ~1;

            if (sx < -20 || sx >= vpW + 20 || sy < -20 || sy >= vpH + 20)
                continue;

            int ttx = (int)(slot.WorldX * invTS);
            int tty = (int)(slot.WorldY * invTS);
            if (!fow.IsVisible(ttx, tty)) continue;

            // Puddle fill
            int fillAlpha = (int)(weather.Intensity * 38);
            if (fillAlpha > 0)
                DrawFilledEllipse(sx, sy, slot.RadiusX, slot.RadiusY,
                    new Color(80, 120, 155, fillAlpha), vpW, vpH);

            // Ripple animation
            int age = _frameCount - slot.BirthFrame;
            if (age < 12)
                DrawRippleRing(sx, sy, slot.RadiusX, slot.RadiusY,
                    age, weather.Intensity, vpW, vpH);
        }

        _particleTexture.SetData(_particlePixels);
        spriteBatch.Draw(_particleTexture, Vector2.Zero, Color.White);
    }

    /// <summary>
    /// Draws streaks, snow, atmosphere tint, lightning. Call AFTER lighting.
    /// </summary>
    public void DrawOverlayEffects(SpriteBatch spriteBatch, WeatherState weather, Camera camera,
        TileMap map, ushort playerZoneId, FogOfWar fow)
    {
        if (_pixel == null) return;

        var device = spriteBatch.GraphicsDevice;
        int vpW = device.Viewport.Width;
        int vpH = device.Viewport.Height;

        // Atmosphere tint
        if (_atmosphereFade > 0.005f)
        {
            bool isSnow = weather.Type == WeatherType.Snow;
            int tR = isSnow ? 10 : 0;
            int tG = isSnow ? 15 : 15;
            int tB = isSnow ? 25 : 30;
            int tA = (int)(_atmosphereFade * 64);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, vpW, vpH), new Color(tR, tG, tB, tA));
        }

        if (_activeCount > 0)
        {
            EnsureTexture(device, vpW, vpH);
            Array.Clear(_particlePixels, 0, _particlePixels.Length);

            float invTileSize = 1f / GameConfig.TileSize;

            int zx1 = 0, zy1 = 0, zx2 = 0, zy2 = 0;
            bool hasPlayerRoof = false;
            if (playerZoneId != 0)
            {
                var playerZone = map.GetZone(playerZoneId);
                if (playerZone != null && playerZone.HasRoof)
                {
                    hasPlayerRoof = true;
                    zx1 = playerZone.Bounds.X - 1;
                    zy1 = playerZone.Bounds.Y - 1;
                    zx2 = playerZone.Bounds.Right + 1;
                    zy2 = playerZone.Bounds.Bottom + 1;
                }
            }

            for (int i = 0; i < _activeCount; i++)
            {
                ref var p = ref _particles[i];
                int sx = p.ScreenX;
                int sy = p.ScreenY;
                if (sx < -8 || sy < -8 || sx >= vpW + 8 || sy >= vpH + 8) continue;

                if (hasPlayerRoof)
                {
                    int tileX = (int)(p.WorldX * invTileSize);
                    int tileY = (int)(p.WorldY * invTileSize);
                    if (tileX >= zx1 && tileX < zx2 && tileY >= zy1 && tileY < zy2) continue;
                }

                switch (p.Type)
                {
                    case TypeStreak:
                        DrawRainStreak(sx, sy, p.Size, p.DriftX, p.DriftY, p.Life, p.MaxLife, vpW, vpH);
                        break;
                    case TypeFlake:
                    case TypeSettled:
                        DrawSnowParticle(sx, sy, p.Size, p.Type, p.Life, p.MaxLife, vpW, vpH);
                        break;
                }
            }

            _particleTexture.SetData(_particlePixels);
            spriteBatch.Draw(_particleTexture, Vector2.Zero, Color.White);
        }

        // Lightning flash
        if (weather.LightningFlash > 0.01f)
        {
            int alpha = (int)(weather.LightningFlash * 180);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, vpW, vpH), new Color(240, 240, 255, alpha));
        }
    }

    private void RelocateSlot(ref RippleSlot slot, float worldLeft, float worldTop,
        float worldRight, float worldBottom, TileMap map, int tileSize)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float wx = worldLeft + (float)_rng.NextDouble() * (worldRight - worldLeft);
            float wy = worldTop + (float)_rng.NextDouble() * (worldBottom - worldTop);
            int ttx = (int)(wx / tileSize);
            int tty = (int)(wy / tileSize);
            if (!map.IsInBounds(ttx, tty)) continue;
            var tile = map.GetTile(ttx, tty);
            if (tile.HasWall) continue;
            if (tile.ZoneId != 0)
            {
                var zone = map.GetZone(tile.ZoneId);
                if (zone != null && zone.HasRoof) continue;
            }
            slot.WorldX = wx;
            slot.WorldY = wy;
            slot.BirthFrame = _frameCount;
            slot.Interval = 10 + _rng.Next(7);
            slot.RadiusX = 3 + _rng.Next(4);
            slot.RadiusY = Math.Max(2, (int)(slot.RadiusX * 0.55f));
            return;
        }
        // Failed — just reset the timer so it tries again next cycle
        slot.BirthFrame = _frameCount;
    }

    private void SpawnRippleSlot(float worldLeft, float worldTop, float worldRight, float worldBottom,
        TileMap map, int tileSize)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float wx = worldLeft + (float)_rng.NextDouble() * (worldRight - worldLeft);
            float wy = worldTop + (float)_rng.NextDouble() * (worldBottom - worldTop);
            int ttx = (int)(wx / tileSize);
            int tty = (int)(wy / tileSize);
            if (!map.IsInBounds(ttx, tty)) continue;
            var tile = map.GetTile(ttx, tty);
            if (tile.HasWall) continue;
            if (tile.ZoneId != 0)
            {
                var zone = map.GetZone(tile.ZoneId);
                if (zone != null && zone.HasRoof) continue;
            }

            ref var slot = ref _rippleSlots[_rippleSlotCount++];
            slot.WorldX = wx;
            slot.WorldY = wy;
            slot.BirthFrame = _frameCount;
            slot.Interval = 10 + _rng.Next(7);
            slot.RadiusX = 3 + _rng.Next(4);
            slot.RadiusY = Math.Max(2, (int)(slot.RadiusX * 0.55f));
            return;
        }
        // All attempts failed (e.g. mostly walls) — add a dummy to avoid infinite loop
        _rippleSlotCount++;
    }

    private void DrawRippleRing(int cx, int cy, int puddleRx, int puddleRy,
        int rippleAge, float intensity, int vpW, int vpH)
    {
        float radiusScale;
        float alpha01;

        if (rippleAge <= 1)
        {
            // Impact: bright 2x2 dot at center
            alpha01 = 0.5f;
            int a = (int)(alpha01 * intensity * 255);
            if (a <= 0) return;
            SetBlock2(cx, cy, new Color(180, 210, 230, a), vpW, vpH);
            return;
        }
        else if (rippleAge <= 5)
        {
            // Expand to 60%
            radiusScale = 0.6f * ((rippleAge - 2) / 3f);
            alpha01 = 0.35f;
        }
        else if (rippleAge <= 9)
        {
            // Expand to 100%
            radiusScale = 0.6f + 0.4f * ((rippleAge - 6) / 3f);
            alpha01 = 0.35f - 0.2f * ((rippleAge - 6) / 3f);
        }
        else
        {
            // Expand to 120%, fade out
            radiusScale = 1.0f + 0.2f * ((rippleAge - 10) / 1f);
            alpha01 = 0.15f * (1f - (rippleAge - 10) / 1f);
        }

        int alpha = (int)(alpha01 * intensity * 255);
        if (alpha <= 0) return;
        var color = new Color(180, 210, 230, alpha);

        int rx = Math.Max(1, (int)(puddleRx * radiusScale));
        int ry = Math.Max(1, (int)(puddleRy * radiusScale));
        DrawEllipseOutline(cx, cy, rx, ry, color, vpW, vpH);
    }

    // --- Ellipse drawing (midpoint algorithm) ---

    private void DrawFilledEllipse(int cx, int cy, int rx, int ry, Color color, int vpW, int vpH)
    {
        int rxSq = rx * rx, rySq = ry * ry;
        int x = 0, y = ry;
        int px = 0, py = 2 * rxSq * y;

        DrawHSpan(cx - rx, cx + rx, cy, color, vpW, vpH);

        int d1 = rySq - rxSq * ry + rxSq / 4;
        while (px < py)
        {
            x++; px += 2 * rySq;
            if (d1 < 0) d1 += rySq * (2 * x + 1);
            else
            {
                y--; py -= 2 * rxSq;
                d1 += rySq * (2 * x + 1) - 2 * rxSq * y;
            }
            DrawHSpan(cx - x, cx + x, cy + y, color, vpW, vpH);
            DrawHSpan(cx - x, cx + x, cy - y, color, vpW, vpH);
        }

        int d2 = rySq * (x * x + x) + rxSq * (y * y - 2 * y + 1) - rxSq * rySq;
        while (y > 0)
        {
            y--; py -= 2 * rxSq;
            if (d2 > 0) d2 += rxSq * (1 - 2 * y);
            else { x++; px += 2 * rySq; d2 += 2 * rySq * x + rxSq * (1 - 2 * y); }
            DrawHSpan(cx - x, cx + x, cy + y, color, vpW, vpH);
            DrawHSpan(cx - x, cx + x, cy - y, color, vpW, vpH);
        }
    }

    private void DrawEllipseOutline(int cx, int cy, int rx, int ry, Color color, int vpW, int vpH)
    {
        int x = 0, y = ry;
        int rxSq = rx * rx, rySq = ry * ry;
        int px = 0, py = 2 * rxSq * y;

        Plot4(cx, cy, x, y, color, vpW, vpH);

        int d1 = rySq - rxSq * ry + rxSq / 4;
        while (px < py)
        {
            x++; px += 2 * rySq;
            if (d1 < 0) d1 += rySq * (2 * x + 1);
            else { y--; py -= 2 * rxSq; d1 += rySq * (2 * x + 1) - 2 * rxSq * y; }
            Plot4(cx, cy, x, y, color, vpW, vpH);
        }

        int d2 = rySq * (x * x + x) + rxSq * (y * y - 2 * y + 1) - rxSq * rySq;
        while (y > 0)
        {
            y--; py -= 2 * rxSq;
            if (d2 > 0) d2 += rxSq * (1 - 2 * y);
            else { x++; px += 2 * rySq; d2 += 2 * rySq * x + rxSq * (1 - 2 * y); }
            Plot4(cx, cy, x, y, color, vpW, vpH);
        }
    }

    private void Plot4(int cx, int cy, int x, int y, Color color, int vpW, int vpH)
    {
        SetBlock2(cx + x, cy + y, color, vpW, vpH);
        SetBlock2(cx - x - 1, cy + y, color, vpW, vpH);
        SetBlock2(cx + x, cy - y - 1, color, vpW, vpH);
        SetBlock2(cx - x - 1, cy - y - 1, color, vpW, vpH);
    }

    private void DrawHSpan(int x0, int x1, int y, Color color, int vpW, int vpH)
    {
        // Draw 2px tall span
        for (int row = y; row < y + 2; row++)
        {
            if (row < 0 || row >= vpH) continue;
            int left = Math.Max(0, x0);
            int right = Math.Min(vpW - 1, x1 + 1);
            int rowOff = row * vpW;
            for (int x = left; x <= right; x++)
                _particlePixels[rowOff + x] = color;
        }
    }

    private void SetBlock2(int x, int y, Color color, int vpW, int vpH)
    {
        for (int dy = 0; dy < 2; dy++)
        {
            int py = y + dy;
            if (py < 0 || py >= vpH) continue;
            int row = py * vpW;
            for (int dx = 0; dx < 2; dx++)
            {
                int px = x + dx;
                if (px >= 0 && px < vpW)
                    _particlePixels[row + px] = color;
            }
        }
    }

    private void SetPixel(int x, int y, Color color, int vpW, int vpH)
    {
        if (x >= 0 && x < vpW && y >= 0 && y < vpH)
            _particlePixels[y * vpW + x] = color;
    }

    // --- Streak / Snow drawing ---

    private void DrawRainStreak(int sx, int sy, int length, float windDx, float windDy,
        float life, float maxLife, int vpW, int vpH)
    {
        float ageRatio = 1f - life / maxLife;
        int alpha = (int)(180 * (1f - ageRatio));
        if (alpha <= 0) return;
        var color = new Color(190, 215, 235, alpha);

        int stepX, stepY;
        float windMag = MathF.Sqrt(windDx * windDx + windDy * windDy);
        if (windMag > 0.05f)
        {
            float nx = windDx / windMag;
            float ny = windDy / windMag;
            stepX = nx >= 0 ? 1 : -1;
            stepY = Math.Abs(ny) > 0.3f ? (ny >= 0 ? 2 : -2) : (ny >= 0 ? 1 : -1);
        }
        else
        {
            stepX = 1;
            stepY = 2;
        }

        int px = sx, py = sy;
        for (int step = 0; step < length; step++)
        {
            SetPixel(px, py, color, vpW, vpH);
            px += stepX;
            py += stepY;
        }
    }

    private void DrawSnowParticle(int cx, int cy, int size, byte type, float life,
        float maxLife, int vpW, int vpH)
    {
        int alpha;
        if (type == TypeSettled)
            alpha = (int)(180 * Math.Min(1f, life / maxLife * 3f));
        else
        {
            float lifeRatio = life / maxLife;
            float fadeIn = Math.Min(1f, (1f - lifeRatio) * 4f);
            float fadeOut = Math.Min(1f, lifeRatio * 3f);
            alpha = (int)(190 * fadeIn * fadeOut);
        }
        if (alpha <= 0) return;

        var color = new Color(230, 235, 240, alpha);
        int x0 = Math.Max(0, cx);
        int y0 = Math.Max(0, cy);
        int x1 = Math.Min(vpW, cx + size);
        int y1 = Math.Min(vpH, cy + size);
        for (int y = y0; y < y1; y++)
        {
            int row = y * vpW;
            for (int x = x0; x < x1; x++)
                _particlePixels[row + x] = color;
        }
    }
}

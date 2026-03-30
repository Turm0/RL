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
    private const int MaxParticles = 4000;

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _activeCount;
    private Texture2D _pixel;
    private Texture2D _particleTexture;
    private Color[] _particlePixels;
    private int _texWidth, _texHeight;

    private readonly Random _rng = new();

    private float _atmosphereFade;
    private int _frameCount;
    private ushort _playerZoneId;

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
    private const byte TypeSplash = 3;

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
            int targetCount = Math.Min(MaxRippleSlots, 8 + (int)(intensity * 24));

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
            // Streak count, length, and lifetime all scale with intensity
            // Light rain: few short streaks. Heavy rain: many long streaks.
            float streakDensity = 0.003f + intensity * 0.012f; // linear — drizzle still visible, heavy is dense
            int streakCount = (int)(streakDensity * visibleTileCount);
            float invTile = 1f / tileSize;
            for (int i = 0; i < streakCount && _activeCount < MaxParticles; i++)
            {
                float wx = worldLeft + (float)_rng.NextDouble() * (worldRight - worldLeft);
                float wy = worldTop + (float)_rng.NextDouble() * (worldBottom - worldTop);
                int stx = (int)(wx * invTile);
                int sty = (int)(wy * invTile);
                if (!map.IsInBounds(stx, sty)) continue;
                if (IsPlayerIndoorTile(map, stx, sty)) continue;

                ref var p = ref _particles[_activeCount++];
                p.WorldX = wx;
                p.WorldY = wy;
                p.ScreenX = (int)(wx - camX);
                p.ScreenY = (int)(wy - camY);
                p.Type = TypeStreak;

                // Depth layer: 1=far (small, faint, slow), 2=mid, 3=near (big, bright, fast)
                byte depth = (byte)(1 + _rng.Next(3));
                p.Size = depth;

                float depthScale = depth / 3f; // 0.33, 0.67, 1.0
                float baseLife = 0.6f - intensity * 0.15f;
                p.MaxLife = p.Life = (baseLife + (float)_rng.NextDouble() * baseLife * 0.3f) * (1.3f - depthScale * 0.5f);

                float fallSpeed = (280f + intensity * 150f) * (0.5f + depthScale * 0.5f);
                p.DriftX = MathF.Sin(weather.WindAngle) * fallSpeed;
                p.DriftY = MathF.Cos(weather.WindAngle) * fallSpeed;
            }

            // Ground splashes at high intensity
            if (intensity > 0.25f)
            {
                float splashRate = (intensity - 0.25f) / 0.75f * 20f; // 0-20 per frame
                int splashSpawn = (int)splashRate;
                if (_rng.NextDouble() < splashRate - splashSpawn) splashSpawn++;
                for (int i = 0; i < splashSpawn && _activeCount < MaxParticles; i++)
                {
                    float wx = worldLeft + (float)_rng.NextDouble() * (worldRight - worldLeft);
                    float wy = worldTop + (float)_rng.NextDouble() * (worldBottom - worldTop);
                    int stx = (int)(wx * invTile);
                    int sty = (int)(wy * invTile);
                    if (!map.IsInBounds(stx, sty)) continue;
                    if (IsPlayerIndoorTile(map, stx, sty)) continue;

                    ref var p = ref _particles[_activeCount++];
                    p.WorldX = wx;
                    p.WorldY = wy;
                    p.ScreenX = (int)(wx - camX);
                    p.ScreenY = (int)(wy - camY);
                    p.Type = TypeSplash;
                    p.MaxLife = p.Life = 0.08f + (float)_rng.NextDouble() * 0.05f; // ~5-8 frames
                    p.Size = 0;
                    p.DriftX = 0;
                    p.DriftY = 0;
                }
            }
        }

        // --- Snow ---
        if (isSnow && intensity > 0.01f)
        {
            // Low intensity: gentle. High intensity: blizzard density.
            float snowSpawnRate = 150f + intensity * intensity * 3000f;
            float toSpawn = snowSpawnRate * deltaSeconds;
            int spawnCount = (int)toSpawn;
            if (_rng.NextDouble() < toSpawn - spawnCount) spawnCount++;

            // Wind-driven fall direction (same as rain)
            float snowWindX = MathF.Sin(weather.WindAngle);
            float snowWindY = MathF.Cos(weather.WindAngle);

            for (int i = 0; i < spawnCount && _activeCount < MaxParticles; i++)
            {
                float wx = worldLeft + (float)_rng.NextDouble() * (worldRight - worldLeft);
                float wy = worldTop + (float)_rng.NextDouble() * (worldBottom - worldTop);
                int stx = (int)(wx / tileSize);
                int sty = (int)(wy / tileSize);
                if (!map.IsInBounds(stx, sty)) continue;
                if (IsPlayerIndoorTile(map, stx, sty)) continue;

                ref var p = ref _particles[_activeCount++];
                p.WorldX = wx;
                p.WorldY = wy;
                p.ScreenX = (int)(wx - camX);
                p.ScreenY = (int)(wy - camY);
                bool settled = _rng.NextDouble() < (0.15 - intensity * 0.1); // less settling in blizzard
                p.Type = settled ? TypeSettled : TypeFlake;

                float baseLife = 0.6f + intensity * 0.3f;
                p.MaxLife = p.Life = settled
                    ? baseLife * 2f + (float)_rng.NextDouble() * baseLife
                    : baseLife + (float)_rng.NextDouble() * baseLife * 0.5f;
                p.Size = (byte)(2 + _rng.Next(1 + (int)(intensity * 2))); // 2-4px, bigger in blizzard

                // Wind-driven: gentle at low intensity, strong at high
                float snowSpeed = 20f + intensity * 80f;
                float randomSpread = (1f - intensity * 0.5f) * 15f; // less random in blizzard (more uniform)
                p.DriftX = snowWindX * snowSpeed + ((float)_rng.NextDouble() - 0.5f) * randomSpread;
                p.DriftY = snowWindY * snowSpeed + 20f + intensity * 40f + (float)_rng.NextDouble() * 10f;
            }
        }

        // --- Update particles ---
        for (int i = _activeCount - 1; i >= 0; i--)
        {
            ref var p = ref _particles[i];
            p.WorldX += p.DriftX * deltaSeconds;
            p.WorldY += p.DriftY * deltaSeconds;
            if (p.Type == TypeFlake)
                p.WorldX += MathF.Sin(p.Life * 3f) * 2.5f * deltaSeconds;
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
        _playerZoneId = playerZoneId;

        if (_rippleSlotCount == 0 && _activeCount == 0) return;

        var device = spriteBatch.GraphicsDevice;
        int vpW = device.Viewport.Width;
        int vpH = device.Viewport.Height;

        EnsureTexture(device, vpW, vpH);
        Array.Clear(_particlePixels, 0, _particlePixels.Length);

        int tileSize = GameConfig.TileSize;
        float camPosX = camera.Position.X;
        float camPosY = camera.Position.Y;
        float invTS = 1f / tileSize;

        // Roof exclusion: player's zone bounds + 1 tile margin (covers walls)
        int rzx1 = 0, rzy1 = 0, rzx2 = 0, rzy2 = 0;
        bool hasRoofZone = false;
        if (playerZoneId != 0)
        {
            var pZone = map.GetZone(playerZoneId);
            if (pZone != null && pZone.HasRoof)
            {
                hasRoofZone = true;
                rzx1 = pZone.Bounds.X - 1;
                rzy1 = pZone.Bounds.Y - 1;
                rzx2 = pZone.Bounds.Right + 1;
                rzy2 = pZone.Bounds.Bottom + 1;
            }
        }

        for (int i = 0; i < _rippleSlotCount; i++)
        {
            ref var slot = ref _rippleSlots[i];
            int sx = (int)(slot.WorldX - camPosX) & ~1;
            int sy = (int)(slot.WorldY - camPosY) & ~1;

            if (sx < -20 || sx >= vpW + 20 || sy < -20 || sy >= vpH + 20)
                continue;

            int ttx = (int)(slot.WorldX * invTS);
            int tty = (int)(slot.WorldY * invTS);
            if (!fow.IsVisible(ttx, tty)) continue;
            if (hasRoofZone && ttx >= rzx1 && ttx < rzx2 && tty >= rzy1 && tty < rzy2)
                continue;

            // Puddle fill
            int fillAlpha = (int)(weather.Intensity * 50);
            if (fillAlpha > 0)
                DrawFilledEllipse(sx, sy, slot.RadiusX, slot.RadiusY,
                    new Color(80, 120, 155, fillAlpha), vpW, vpH);

            // Ripple animation
            int age = _frameCount - slot.BirthFrame;
            if (age < 12)
                DrawRippleRing(sx, sy, slot.RadiusX, slot.RadiusY,
                    age, weather.Intensity, vpW, vpH);
        }

        // All particles: streaks, splashes, snow
        float invTileSize2 = 1f / tileSize;
        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];

            int ptx = (int)(p.WorldX * invTileSize2);
            int pty = (int)(p.WorldY * invTileSize2);
            if (!map.IsInBounds(ptx, pty)) continue;
            if (!fow.IsVisible(ptx, pty)) continue;
            if (hasRoofZone && ptx >= rzx1 && ptx < rzx2 && pty >= rzy1 && pty < rzy2)
                continue;

            int sx = p.ScreenX;
            int sy = p.ScreenY;
            if (sx < -16 || sy < -16 || sx >= vpW + 16 || sy >= vpH + 16) continue;

            switch (p.Type)
            {
                case TypeStreak:
                    DrawRainStreak(sx, sy, p.Size, p.DriftX, p.DriftY, p.Life, p.MaxLife, vpW, vpH);
                    break;

                case TypeSplash:
                {
                    int spx = sx & ~1;
                    int spy = sy & ~1;
                    float age01 = 1f - p.Life / p.MaxLife;
                    int baseAlpha = (int)(weather.Intensity * 150);

                    if (age01 < 0.25f)
                    {
                        SetBlock2(spx, spy, new Color(170, 205, 235, baseAlpha), vpW, vpH);
                    }
                    else if (age01 < 0.55f)
                    {
                        int a = (int)(baseAlpha * 0.7f);
                        var col = new Color(170, 205, 235, a);
                        SetBlock2(spx, spy, col, vpW, vpH);
                        SetBlock2(spx - 2, spy, col, vpW, vpH);
                        SetBlock2(spx + 2, spy, col, vpW, vpH);
                        SetBlock2(spx, spy - 2, col, vpW, vpH);
                        SetBlock2(spx, spy + 2, col, vpW, vpH);
                    }
                    else
                    {
                        float fade = 1f - (age01 - 0.55f) / 0.45f;
                        int a = (int)(baseAlpha * 0.4f * fade);
                        if (a <= 0) break;
                        var col = new Color(170, 205, 235, a);
                        SetBlock2(spx - 4, spy, col, vpW, vpH);
                        SetBlock2(spx + 4, spy, col, vpW, vpH);
                        SetBlock2(spx, spy - 4, col, vpW, vpH);
                        SetBlock2(spx, spy + 4, col, vpW, vpH);
                    }
                    break;
                }

                case TypeFlake:
                case TypeSettled:
                    DrawSnowParticle(sx, sy, p.Size, p.Type, p.Life, p.MaxLife, vpW, vpH);
                    break;
            }
        }

        _particleTexture.SetData(_particlePixels);
        spriteBatch.Draw(_particleTexture, Vector2.Zero, Color.White);
    }

    /// <summary>
    /// Draws atmosphere tint + lightning. Call AFTER lighting.
    /// </summary>
    public void DrawOverlayEffects(SpriteBatch spriteBatch, WeatherState weather)
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
            if (IsPlayerIndoorTile(map, ttx, tty)) continue;
            slot.WorldX = wx;
            slot.WorldY = wy;
            slot.BirthFrame = _frameCount;
            slot.Interval = 10 + _rng.Next(7);
            slot.RadiusX = 4 + _rng.Next(4);
            slot.RadiusY = Math.Max(2, (int)(slot.RadiusX * 0.55f));
            return;
        }
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
            if (IsPlayerIndoorTile(map, ttx, tty)) continue;

            ref var slot = ref _rippleSlots[_rippleSlotCount++];
            slot.WorldX = wx;
            slot.WorldY = wy;
            slot.BirthFrame = _frameCount;
            slot.Interval = 10 + _rng.Next(7);
            slot.RadiusX = 4 + _rng.Next(4);
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
            alpha01 = 0.6f;
            int a = (int)(alpha01 * intensity * 255);
            if (a <= 0) return;
            SetBlock2(cx, cy, new Color(180, 210, 230, a), vpW, vpH);
            return;
        }
        else if (rippleAge <= 5)
        {
            radiusScale = 0.6f * ((rippleAge - 2) / 3f);
            alpha01 = 0.45f;
        }
        else if (rippleAge <= 9)
        {
            radiusScale = 0.6f + 0.4f * ((rippleAge - 6) / 3f);
            alpha01 = 0.45f - 0.25f * ((rippleAge - 6) / 3f);
        }
        else
        {
            radiusScale = 1.0f + 0.2f * ((rippleAge - 10) / 1f);
            alpha01 = 0.2f * (1f - (rippleAge - 10) / 1f);
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

    /// <summary>
    /// Returns true if this tile should block weather spawning
    /// (player's indoor zone + its walls, or out of bounds).
    /// </summary>
    private bool IsPlayerIndoorTile(TileMap map, int tx, int ty)
    {
        if (_playerZoneId == 0) return false;
        var pZone = map.GetZone(_playerZoneId);
        if (pZone == null || !pZone.HasRoof) return false;
        // Check expanded bounds (zone + 1 tile wall margin)
        return tx >= pZone.Bounds.X - 1 && tx < pZone.Bounds.Right + 1 &&
               ty >= pZone.Bounds.Y - 1 && ty < pZone.Bounds.Bottom + 1;
    }

    // --- Streak / Snow drawing ---

    private void DrawRainStreak(int sx, int sy, int depth, float windDx, float windDy,
        float life, float maxLife, int vpW, int vpH)
    {
        // depth: 1=far, 2=mid, 3=near
        float depthScale = depth / 3f;

        // Far drops: shorter (2px), faint. Near drops: longer (4px), bright.
        int length = 2 + depth;
        int maxAlpha = (int)(80 + depthScale * 130); // far=123, mid=167, near=210

        float lifeRatio = life / maxLife;
        float lifeFade = lifeRatio * lifeRatio;

        // Direction from drift
        float driftMag = MathF.Sqrt(windDx * windDx + windDy * windDy);
        float dirX = driftMag > 0.1f ? windDx / driftMag : 0f;
        float dirY = driftMag > 0.1f ? windDy / driftMag : 1f;

        // Pixel step size: far=2px, near=2px (same block size, length differs)
        int px = sx & ~1, py = sy & ~1;
        for (int step = 0; step < length; step++)
        {
            float t = (float)step / (length - 1 + 0.001f);
            float spatialFade = 0.3f + t * 0.7f;
            int alpha = (int)(maxAlpha * lifeFade * spatialFade);
            if (alpha <= 0) continue;
            // Far drops slightly desaturated, near drops more vivid blue
            int r = (int)(100 + (1f - depthScale) * 30); // far=130, near=100
            int g = (int)(140 + (1f - depthScale) * 20); // far=160, near=140
            int b = (int)(220 - (1f - depthScale) * 10); // far=210, near=220
            var color = new Color(r, g, b, alpha);
            SetBlock2(px, py, color, vpW, vpH);
            px = (sx + (int)(dirX * (step + 1) * 2)) & ~1;
            py = (sy + (int)(dirY * (step + 1) * 2)) & ~1;
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

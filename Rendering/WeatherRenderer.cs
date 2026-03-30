using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// GPU-accelerated weather renderer. All particles drawn via SpriteBatch to a
/// RenderTarget2D — no CPU pixel compositing, no SetData uploads.
/// </summary>
public class WeatherRenderer
{
    private const int MaxParticles = 4000;

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _activeCount;
    private Texture2D _pixel;
    private GraphicsDevice _device;

    private SpriteBatch _weatherBatch;

    private readonly Random _rng = new();

    private float _atmosphereFade;
    private int _frameCount;
    private ushort _lastPlayerZoneId;
    private float _tintR = 1f, _tintG = 1f, _tintB = 1f;

    // Rain puddle pool
    private const int MaxRippleSlots = 32;
    private readonly RippleSlot[] _rippleSlots = new RippleSlot[MaxRippleSlots];
    private int _rippleSlotCount;

    // Pre-rendered ellipse textures for puddles and ripple rings
    private readonly Dictionary<int, Texture2D> _ellipseFillCache = new();
    private readonly Dictionary<int, Texture2D> _ellipseOutlineCache = new();

    private struct RippleSlot
    {
        public float WorldX, WorldY;
        public int BirthFrame;
        public int Interval;
        public int RadiusX, RadiusY;
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
        _device = device;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _weatherBatch = new SpriteBatch(device);
    }

    private Texture2D GetEllipseFill(int rx, int ry)
    {
        int key = rx << 16 | ry;
        if (_ellipseFillCache.TryGetValue(key, out var tex)) return tex;
        tex = CreateEllipseTexture(rx, ry, true);
        _ellipseFillCache[key] = tex;
        return tex;
    }

    private Texture2D GetEllipseOutline(int rx, int ry)
    {
        int key = rx << 16 | ry;
        if (_ellipseOutlineCache.TryGetValue(key, out var tex)) return tex;
        tex = CreateEllipseTexture(rx, ry, false);
        _ellipseOutlineCache[key] = tex;
        return tex;
    }

    private Texture2D CreateEllipseTexture(int rx, int ry, bool filled)
    {
        int w = rx * 2 + 2; // +2 for 2px block margin
        int h = ry * 2 + 2;
        var pixels = new Color[w * h];
        int cx = rx, cy = ry;

        if (filled)
        {
            // Midpoint filled ellipse
            int rxSq = rx * rx, rySq = ry * ry;
            int ex = 0, ey = ry;
            int px = 0, py = 2 * rxSq * ey;

            FillHSpan(pixels, w, cx - rx, cx + rx, cy);

            int d1 = rySq - rxSq * ry + rxSq / 4;
            while (px < py)
            {
                ex++; px += 2 * rySq;
                if (d1 < 0) d1 += rySq * (2 * ex + 1);
                else { ey--; py -= 2 * rxSq; d1 += rySq * (2 * ex + 1) - 2 * rxSq * ey; }
                FillHSpan(pixels, w, cx - ex, cx + ex, cy + ey);
                FillHSpan(pixels, w, cx - ex, cx + ex, cy - ey);
            }
            int d2 = rySq * (ex * ex + ex) + rxSq * (ey * ey - 2 * ey + 1) - rxSq * rySq;
            while (ey > 0)
            {
                ey--; py -= 2 * rxSq;
                if (d2 > 0) d2 += rxSq * (1 - 2 * ey);
                else { ex++; px += 2 * rySq; d2 += 2 * rySq * ex + rxSq * (1 - 2 * ey); }
                FillHSpan(pixels, w, cx - ex, cx + ex, cy + ey);
                FillHSpan(pixels, w, cx - ex, cx + ex, cy - ey);
            }
        }
        else
        {
            // Midpoint outline ellipse
            int rxSq = rx * rx, rySq = ry * ry;
            int ex = 0, ey = ry;
            int px = 0, py = 2 * rxSq * ey;

            Plot4Tex(pixels, w, cx, cy, ex, ey);

            int d1 = rySq - rxSq * ry + rxSq / 4;
            while (px < py)
            {
                ex++; px += 2 * rySq;
                if (d1 < 0) d1 += rySq * (2 * ex + 1);
                else { ey--; py -= 2 * rxSq; d1 += rySq * (2 * ex + 1) - 2 * rxSq * ey; }
                Plot4Tex(pixels, w, cx, cy, ex, ey);
            }
            int d2 = rySq * (ex * ex + ex) + rxSq * (ey * ey - 2 * ey + 1) - rxSq * rySq;
            while (ey > 0)
            {
                ey--; py -= 2 * rxSq;
                if (d2 > 0) d2 += rxSq * (1 - 2 * ey);
                else { ex++; px += 2 * rySq; d2 += 2 * rySq * ex + rxSq * (1 - 2 * ey); }
                Plot4Tex(pixels, w, cx, cy, ex, ey);
            }
        }

        var texture = new Texture2D(_device, w, h);
        texture.SetData(pixels);
        return texture;
    }

    private static void FillHSpan(Color[] pixels, int w, int x0, int x1, int y)
    {
        if (y < 0 || y >= pixels.Length / w) return;
        // 2px tall spans for pixel-art consistency
        for (int row = y; row <= y + 1 && row < pixels.Length / w; row++)
        {
            int left = Math.Max(0, x0);
            int right = Math.Min(w - 1, x1 + 1);
            for (int x = left; x <= right; x++)
                pixels[row * w + x] = Color.White;
        }
    }

    private static void Plot4Tex(Color[] pixels, int w, int cx, int cy, int x, int y)
    {
        void Set(int px, int py)
        {
            // 2px blocks
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    int tx = px + dx, ty = py + dy;
                    if (tx >= 0 && tx < w && ty >= 0 && ty < pixels.Length / w)
                        pixels[ty * w + tx] = Color.White;
                }
        }
        Set(cx + x, cy + y);
        Set(cx - x - 1, cy + y);
        Set(cx + x, cy - y - 1);
        Set(cx - x - 1, cy - y - 1);
    }

    // ========== Update ==========

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
        float worldLeft = Math.Max(camX, 0);
        float worldTop = Math.Max(camY, 0);
        float worldRight = Math.Min(camX + viewportWidth, map.Width * tileSize);
        float worldBottom = Math.Min(camY + viewportHeight, map.Height * tileSize);

        // --- Manage rain puddle pool ---
        if (isRain && intensity > 0.01f)
        {
            int targetCount = Math.Min(MaxRippleSlots, 8 + (int)(intensity * 24));
            while (_rippleSlotCount < targetCount)
                SpawnRippleSlot(worldLeft, worldTop, worldRight, worldBottom, map, tileSize);

            for (int i = 0; i < _rippleSlotCount; i++)
            {
                ref var slot = ref _rippleSlots[i];
                if (_frameCount - slot.BirthFrame >= slot.Interval)
                    RelocateSlot(ref slot, worldLeft, worldTop, worldRight, worldBottom, map, tileSize);
            }
        }
        else
        {
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
            float streakDensity = 0.003f + intensity * 0.012f;
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
                p.WorldX = wx; p.WorldY = wy;
                p.ScreenX = (int)(wx - camX); p.ScreenY = (int)(wy - camY);
                p.Type = TypeStreak;

                byte depth = (byte)(1 + _rng.Next(3));
                p.Size = depth;
                float depthScale = depth / 3f;
                float baseLife = 0.6f - intensity * 0.15f;
                p.MaxLife = p.Life = (baseLife + (float)_rng.NextDouble() * baseLife * 0.3f) * (1.3f - depthScale * 0.5f);

                float fallSpeed = (380f + intensity * 150f) * (0.5f + depthScale * 0.5f);
                p.DriftX = MathF.Sin(weather.WindAngle) * fallSpeed;
                p.DriftY = MathF.Cos(weather.WindAngle) * fallSpeed;
            }

            // Ground splashes
            if (intensity > 0.25f)
            {
                float splashRate = (intensity - 0.25f) / 0.75f * 20f;
                int splashSpawn = (int)splashRate;
                if (_rng.NextDouble() < splashRate - splashSpawn) splashSpawn++;
                float invTile2 = 1f / tileSize;
                for (int i = 0; i < splashSpawn && _activeCount < MaxParticles; i++)
                {
                    float wx = worldLeft + (float)_rng.NextDouble() * (worldRight - worldLeft);
                    float wy = worldTop + (float)_rng.NextDouble() * (worldBottom - worldTop);
                    int stx = (int)(wx * invTile2);
                    int sty = (int)(wy * invTile2);
                    if (!map.IsInBounds(stx, sty)) continue;
                    if (IsPlayerIndoorTile(map, stx, sty)) continue;

                    ref var p = ref _particles[_activeCount++];
                    p.WorldX = wx; p.WorldY = wy;
                    p.ScreenX = (int)(wx - camX); p.ScreenY = (int)(wy - camY);
                    p.Type = TypeSplash;
                    p.MaxLife = p.Life = 0.08f + (float)_rng.NextDouble() * 0.05f;
                    p.Size = 0; p.DriftX = 0; p.DriftY = 0;
                }
            }
        }

        // --- Snow ---
        if (isSnow && intensity > 0.01f)
        {
            float snowSpawnRate = 150f + intensity * intensity * 3000f;
            float toSpawn = snowSpawnRate * deltaSeconds;
            int spawnCount = (int)toSpawn;
            if (_rng.NextDouble() < toSpawn - spawnCount) spawnCount++;

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
                p.WorldX = wx; p.WorldY = wy;
                p.ScreenX = (int)(wx - camX); p.ScreenY = (int)(wy - camY);
                bool settled = _rng.NextDouble() < (0.15 - intensity * 0.1);
                p.Type = settled ? TypeSettled : TypeFlake;

                float baseLife = 0.6f + intensity * 0.3f;
                p.MaxLife = p.Life = settled
                    ? baseLife * 2f + (float)_rng.NextDouble() * baseLife
                    : baseLife + (float)_rng.NextDouble() * baseLife * 0.5f;
                p.Size = (byte)(2 + _rng.Next(1 + (int)(intensity * 2)));

                float snowSpeed = 20f + intensity * 80f;
                float randomSpread = (1f - intensity * 0.5f) * 15f;
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

    // ========== Draw Methods (GPU-based via SpriteBatch to RenderTarget) ==========

    /// <summary>
    /// Draws ground-level weather (puddles, streaks, splashes, snow). Call BEFORE lighting.
    /// </summary>
    public void DrawGroundEffects(SpriteBatch spriteBatch, WeatherState weather, Camera camera,
        TileMap map, ushort playerZoneId, FogOfWar fow)
    {
        _lastPlayerZoneId = playerZoneId;
        if (_rippleSlotCount == 0 && _activeCount == 0) return;

        int vpW = _device.Viewport.Width;
        int vpH = _device.Viewport.Height;

        _weatherBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);

        int tileSize = GameConfig.TileSize;
        float camPosX = camera.Position.X;
        float camPosY = camera.Position.Y;
        float invTS = 1f / tileSize;

        // Puddles
        for (int i = 0; i < _rippleSlotCount; i++)
        {
            ref var slot = ref _rippleSlots[i];
            int sx = (int)(slot.WorldX - camPosX) & ~1;
            int sy = (int)(slot.WorldY - camPosY) & ~1;
            if (sx < -20 || sx >= vpW + 20 || sy < -20 || sy >= vpH + 20) continue;

            int ttx = (int)(slot.WorldX * invTS);
            int tty = (int)(slot.WorldY * invTS);
            if (!fow.IsVisible(ttx, tty)) continue;
            if (map.HasElevatedCover(ttx, tty)) continue;

            // Puddle fill
            int fillAlpha = (int)(weather.Intensity * 50);
            if (fillAlpha > 0)
            {
                var fillTex = GetEllipseFill(slot.RadiusX, slot.RadiusY);
                _weatherBatch.Draw(fillTex,
                    new Vector2(sx - slot.RadiusX, sy - slot.RadiusY),
                    Tint(80, 120, 155, fillAlpha));
            }

            // Ripple ring
            int age = _frameCount - slot.BirthFrame;
            if (age < 12)
                DrawRippleRing(_weatherBatch, sx, sy, slot.RadiusX, slot.RadiusY, age, weather.Intensity);
        }

        // Particles
        float invTileSize = 1f / tileSize;
        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];

            int ptx = (int)(p.WorldX * invTileSize);
            int pty = (int)(p.WorldY * invTileSize);
            if (!map.IsInBounds(ptx, pty)) continue;
            if (!fow.IsVisible(ptx, pty)) continue;
            if (map.HasElevatedCover(ptx, pty)) continue;

            int sx = p.ScreenX;
            int sy = p.ScreenY;
            if (sx < -16 || sy < -16 || sx >= vpW + 16 || sy >= vpH + 16) continue;

            switch (p.Type)
            {
                case TypeStreak:
                    DrawRainStreak(_weatherBatch, sx, sy, p.Size, p.DriftX, p.DriftY, p.Life, p.MaxLife);
                    break;
                case TypeSplash:
                    DrawSplash(_weatherBatch, sx, sy, p.Life, p.MaxLife, weather.Intensity);
                    break;
                case TypeFlake:
                case TypeSettled:
                    DrawSnowParticle(_weatherBatch, sx, sy, p.Size, p.Type, p.Life, p.MaxLife);
                    break;
            }
        }

        _weatherBatch.End();
    }

    /// <summary>
    /// Draws weather on elevated surfaces. Call AFTER elevated layer rendering.
    /// </summary>
    public void DrawElevatedEffects(SpriteBatch spriteBatch, WeatherState weather, Camera camera,
        TileMap map, ushort playerZoneId, FogOfWar fow, Vector3 ambientColor)
    {
        if (_activeCount == 0 || playerZoneId != 0) return;

        int vpW = _device.Viewport.Width;
        int vpH = _device.Viewport.Height;

        float invTileSize = 1f / GameConfig.TileSize;

        // Apply ambient tint to particle colors for elevated surfaces
        _tintR = ambientColor.X;
        _tintG = ambientColor.Y;
        _tintB = ambientColor.Z;

        _weatherBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);

        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];

            int ptx = (int)(p.WorldX * invTileSize);
            int pty = (int)(p.WorldY * invTileSize);
            if (!map.IsInBounds(ptx, pty)) continue;
            if (!map.HasElevatedCover(ptx, pty)) continue;
            if (!fow.IsExplored(ptx, pty)) continue;

            int sx = p.ScreenX;
            int sy = p.ScreenY;
            if (sx < -16 || sy < -16 || sx >= vpW + 16 || sy >= vpH + 16) continue;

            switch (p.Type)
            {
                case TypeStreak:
                    DrawRainStreak(_weatherBatch, sx, sy, p.Size, p.DriftX, p.DriftY, p.Life, p.MaxLife);
                    break;
                case TypeFlake:
                case TypeSettled:
                    DrawSnowParticle(_weatherBatch, sx, sy, p.Size, p.Type, p.Life, p.MaxLife);
                    break;
            }
        }

        _weatherBatch.End();

        // Reset tint for ground pass
        _tintR = _tintG = _tintB = 1f;
    }

    /// <summary>
    /// Draws atmosphere tint + lightning. Call AFTER lighting.
    /// </summary>
    public void DrawOverlayEffects(SpriteBatch spriteBatch, WeatherState weather)
    {
        if (_pixel == null) return;

        var device = _pixel.GraphicsDevice;
        int vpW = device.Viewport.Width;
        int vpH = device.Viewport.Height;

        if (_atmosphereFade > 0.005f)
        {
            bool isSnow = weather.Type == WeatherType.Snow;
            int tR = isSnow ? 10 : 0;
            int tG = isSnow ? 15 : 15;
            int tB = isSnow ? 25 : 30;
            int tA = (int)(_atmosphereFade * 64);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, vpW, vpH), new Color(tR, tG, tB, tA));
        }

        if (weather.LightningFlash > 0.01f)
        {
            int alpha = (int)(weather.LightningFlash * 180);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, vpW, vpH), new Color(240, 240, 255, alpha));
        }
    }

    // ========== Particle Drawing (SpriteBatch-based) ==========

    private void DrawRippleRing(SpriteBatch batch, int cx, int cy, int puddleRx, int puddleRy,
        int rippleAge, float intensity)
    {
        float radiusScale;
        float alpha01;

        if (rippleAge <= 1)
        {
            alpha01 = 0.6f;
            int a = (int)(alpha01 * intensity * 255);
            if (a <= 0) return;
            batch.Draw(_pixel, new Rectangle(cx, cy, 2, 2), Tint(180, 210, 230, a));
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

        int rx = Math.Max(1, (int)(puddleRx * radiusScale));
        int ry = Math.Max(1, (int)(puddleRy * radiusScale));
        var outlineTex = GetEllipseOutline(rx, ry);
        batch.Draw(outlineTex, new Vector2(cx - rx, cy - ry), Tint(180, 210, 230, alpha));
    }

    private void DrawRainStreak(SpriteBatch batch, int sx, int sy, int depth,
        float windDx, float windDy, float life, float maxLife)
    {
        float depthScale = depth / 3f;
        int length = 2 + depth;
        int maxAlpha = (int)(80 + depthScale * 130);

        float lifeRatio = life / maxLife;
        float lifeFade = lifeRatio * lifeRatio;

        float driftMag = MathF.Sqrt(windDx * windDx + windDy * windDy);
        float dirX = driftMag > 0.1f ? windDx / driftMag : 0f;
        float dirY = driftMag > 0.1f ? windDy / driftMag : 1f;

        int px = sx & ~1, py = sy & ~1;
        for (int step = 0; step < length; step++)
        {
            float t = (float)step / (length - 1 + 0.001f);
            float spatialFade = 0.4f + t * 0.6f;
            int alpha = (int)(maxAlpha * lifeFade * spatialFade);
            if (alpha <= 0) continue;
            int r = (int)(100 + (1f - depthScale) * 30);
            int g = (int)(140 + (1f - depthScale) * 20);
            int b = (int)(220 - (1f - depthScale) * 10);
            batch.Draw(_pixel, new Rectangle(px, py, 2, 2), Tint(r, g, b, alpha));
            px = (sx + (int)(dirX * (step + 1) * 2)) & ~1;
            py = (sy + (int)(dirY * (step + 1) * 2)) & ~1;
        }
    }

    private void DrawSplash(SpriteBatch batch, int sx, int sy, float life, float maxLife, float intensity)
    {
        int spx = sx & ~1, spy = sy & ~1;
        float age01 = 1f - life / maxLife;
        int baseAlpha = (int)(intensity * 150);
        var col = Tint(170, 205, 235, baseAlpha);

        if (age01 < 0.25f)
        {
            batch.Draw(_pixel, new Rectangle(spx, spy, 2, 2), col);
        }
        else if (age01 < 0.55f)
        {
            int a = (int)(baseAlpha * 0.7f);
            var col2 = Tint(170, 205, 235, a);
            batch.Draw(_pixel, new Rectangle(spx, spy, 2, 2), col2);
            batch.Draw(_pixel, new Rectangle(spx - 2, spy, 2, 2), col2);
            batch.Draw(_pixel, new Rectangle(spx + 2, spy, 2, 2), col2);
            batch.Draw(_pixel, new Rectangle(spx, spy - 2, 2, 2), col2);
            batch.Draw(_pixel, new Rectangle(spx, spy + 2, 2, 2), col2);
        }
        else
        {
            float fade = 1f - (age01 - 0.55f) / 0.45f;
            int a = (int)(baseAlpha * 0.4f * fade);
            if (a <= 0) return;
            var col3 = Tint(170, 205, 235, a);
            batch.Draw(_pixel, new Rectangle(spx - 4, spy, 2, 2), col3);
            batch.Draw(_pixel, new Rectangle(spx + 4, spy, 2, 2), col3);
            batch.Draw(_pixel, new Rectangle(spx, spy - 4, 2, 2), col3);
            batch.Draw(_pixel, new Rectangle(spx, spy + 4, 2, 2), col3);
        }
    }

    private void DrawSnowParticle(SpriteBatch batch, int sx, int sy, int size, byte type,
        float life, float maxLife)
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

        batch.Draw(_pixel, new Rectangle(sx, sy, size, size), Tint(230, 235, 240, alpha));
    }

    // ========== Puddle Management ==========

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
            slot.WorldX = wx; slot.WorldY = wy;
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
            slot.WorldX = wx; slot.WorldY = wy;
            slot.BirthFrame = _frameCount;
            slot.Interval = 10 + _rng.Next(7);
            slot.RadiusX = 4 + _rng.Next(4);
            slot.RadiusY = Math.Max(2, (int)(slot.RadiusX * 0.55f));
            return;
        }
        _rippleSlotCount++;
    }

    private Color Tint(int r, int g, int b, int a)
        => new Color((int)(r * _tintR), (int)(g * _tintG), (int)(b * _tintB), a);

    private Color Tint(Color c)
        => new Color((int)(c.R * _tintR), (int)(c.G * _tintG), (int)(c.B * _tintB), c.A);

    private bool IsPlayerIndoorTile(TileMap map, int tx, int ty)
    {
        if (!map.HasElevatedCover(tx, ty)) return false;
        if (_lastPlayerZoneId == 0) return false;
        ushort zoneId = map.GetZoneId(tx, ty);
        if (zoneId == _lastPlayerZoneId) return true;
        if (zoneId == 0 && map.GetTile(tx, ty).HasWall) return true;
        return false;
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.World;
using System;

namespace RoguelikeEngine.Rendering;

public class RenderPipeline
{
    private Camera _camera;
    private TerrainRenderer _terrainRenderer;
    private EntityRenderer _entityRenderer;
    private RoofRenderer _roofRenderer;
    private EffectOverlayRenderer _effectOverlayRenderer;
    private FogEdgeRenderer _fogEdgeRenderer;
    private WeatherRenderer _weatherRenderer;
    private LightingSystem _lightingSystem;
    private FogOfWar _fogOfWar;
    private SpriteBatch _spriteBatch;
    private GraphicsDevice _graphicsDevice;
    private GameWindow _window;
    private SpriteFont _font;
    private WeatherState _weatherState;
    private TileMap _map;

    private const int FovRadius = 60;

    public Camera Camera => _camera;
    public FogOfWar FogOfWar => _fogOfWar;
    public EffectOverlayRenderer EffectOverlay => _effectOverlayRenderer;
    public LightingSystem Lighting => _lightingSystem;

    public void Initialize(GraphicsDevice graphicsDevice, GameWindow window, TileMap map, SpriteFont font)
    {
        _graphicsDevice = graphicsDevice;
        _window = window;
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _font = font;

        _camera = new Camera
        {
            ViewportWidth = graphicsDevice.Viewport.Width,
            ViewportHeight = graphicsDevice.Viewport.Height
        };

        _map = map;
        _terrainRenderer = new TerrainRenderer(map);
        _entityRenderer = new EntityRenderer(new VectorRasterizer(), new TextureCache());
        _roofRenderer = new RoofRenderer();
        _effectOverlayRenderer = new EffectOverlayRenderer();
        _fogEdgeRenderer = new FogEdgeRenderer();
        _fogEdgeRenderer.Initialize(graphicsDevice);
        _weatherRenderer = new WeatherRenderer();
        _weatherRenderer.Initialize(graphicsDevice);
        _lightingSystem = new LightingSystem();
        _fogOfWar = new FogOfWar(map.Width, map.Height);

        _window.ClientSizeChanged += OnClientSizeChanged;
    }

    public void SetWeatherState(WeatherState weatherState)
    {
        _weatherState = weatherState;
    }

    private void OnClientSizeChanged(object sender, System.EventArgs e)
    {
        _camera.ViewportWidth = _graphicsDevice.Viewport.Width;
        _camera.ViewportHeight = _graphicsDevice.Viewport.Height;
    }

    public void Update(GameTime gameTime)
    {
        _camera.Update(gameTime);

        if (_weatherState != null)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _weatherRenderer.Update(dt, _weatherState, _camera,
                _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, _map);
        }
    }

    public void UpdateFov(int playerTileX, int playerTileY, TileMap map)
    {
        ushort playerZoneId = map.GetZoneId(playerTileX, playerTileY);
        _fogOfWar.Compute(playerTileX, playerTileY, FovRadius, map, playerZoneId);
    }

    public void Draw(GameTime gameTime, TileMap map, DefaultEcs.World ecsWorld)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = _camera.GetVisibleTileRect(tileSize);
        float time = (float)gameTime.TotalGameTime.TotalSeconds;

        // Get player zone for roof logic
        ushort playerZoneId = 0;
        using (var players = ecsWorld.GetEntities()
            .With<PlayerControlled>()
            .With<Position>()
            .AsSet())
        {
            foreach (ref readonly var entity in players.GetEntities())
            {
                ref readonly var pos = ref entity.Get<Position>();
                playerZoneId = map.GetZoneId(pos.TileX, pos.TileY);
                break;
            }
        }

        // Update roof fade
        _roofRenderer.Update(gameTime, map, playerZoneId);

        // Apply weather ambient modifier
        if (_weatherState != null && _weatherState.Intensity > 0.01f)
        {
            float i = _weatherState.Intensity;
            Vector3 mod = _weatherState.Type switch
            {
                // Rain: darken + blue shift (reduce R/G more than B)
                WeatherType.Rain => new Vector3(1f - i * 0.3f, 1f - i * 0.25f, 1f - i * 0.1f),
                WeatherType.Thunderstorm => new Vector3(1f - i * 0.45f, 1f - i * 0.4f, 1f - i * 0.15f),
                // Snow: slight cool shift
                WeatherType.Snow => new Vector3(1f - i * 0.1f, 1f - i * 0.05f, 1f + i * 0.05f),
                _ => Vector3.One
            };
            _lightingSystem.SetWeatherAmbientMod(mod);
        }
        else
        {
            _lightingSystem.SetWeatherAmbientMod(Vector3.One);
        }

        // Compute lighting
        _lightingSystem.Resize(visibleRect.Width, visibleRect.Height, _graphicsDevice);
        _lightingSystem.BeginFrame(visibleRect, map);

        using var lights = ecsWorld.GetEntities()
            .With<Position>()
            .With<LightEmitter>()
            .AsSet();

        foreach (ref readonly var entity in lights.GetEntities())
        {
            ref readonly var pos = ref entity.Get<Position>();
            ref readonly var light = ref entity.Get<LightEmitter>();

            if (pos.TileX + (int)light.Radius < visibleRect.X ||
                pos.TileX - (int)light.Radius >= visibleRect.X + visibleRect.Width ||
                pos.TileY + (int)light.Radius < visibleRect.Y ||
                pos.TileY - (int)light.Radius >= visibleRect.Y + visibleRect.Height)
                continue;

            // Stable flicker seed from position — small range so Sin() stays precise
            int flickerSeed = ((pos.TileX * 7 + pos.TileY * 13) & 0xFF);

            _lightingSystem.AddLight(pos.TileX, pos.TileY, light.Radius, light.Intensity,
                light.Color, map, time, light.Flicker, light.FlickerIntensity, flickerSeed);
        }

        // Terrain-emitted lights (lava, etc.)
        for (int tx = visibleRect.X; tx < visibleRect.X + visibleRect.Width; tx++)
        {
            for (int ty = visibleRect.Y; ty < visibleRect.Y + visibleRect.Height; ty++)
            {
                if (!map.IsInBounds(tx, ty)) continue;
                var tile = map.GetTile(tx, ty);
                if (tile.HasWall) continue;
                var terrainDef = Data.TerrainRegistry.Get(tile.Terrain);
                if (terrainDef.LightRadius <= 0f) continue;

                int flickerSeed = ((tx * 7 + ty * 13) & 0xFF);
                _lightingSystem.AddLight(tx, ty, terrainDef.LightRadius, terrainDef.LightIntensity,
                    terrainDef.LightColor, map, time, terrainDef.LightFlicker,
                    terrainDef.LightFlickerIntensity, flickerSeed);
            }
        }

        _lightingSystem.BlurBuffer(map);
        _lightingSystem.BuildTexture(_graphicsDevice, _fogOfWar);

        // Draw scene
        _graphicsDevice.Clear(Color.Black);

        // 1. Terrain
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _terrainRenderer.Draw(_spriteBatch, map, _camera, _fogOfWar, time, _lightingSystem.AmbientColor);
        _fogEdgeRenderer.Draw(_spriteBatch, map, _camera, _fogOfWar);
        _spriteBatch.End();

        // 2. Weather ground effects (puddles, streaks, snow — renders to internal RT then composites)
        if (_weatherState != null && _weatherState.Intensity > 0.01f)
        {
            _weatherRenderer.DrawGroundEffects(_spriteBatch, _weatherState, _camera, map, playerZoneId, _fogOfWar);
        }

        // 3. Entities
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _entityRenderer.Draw(_spriteBatch, _camera, ecsWorld, tileSize, _fogOfWar, dt,
            (tx, ty) => _roofRenderer.IsHiddenByRoof(map, tx, ty, playerZoneId));
        _spriteBatch.End();

        // 4. Lighting overlay (multiply blend)
        _lightingSystem.Draw(_spriteBatch, _camera, tileSize);

        // 5. Effect overlays AFTER lighting
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _effectOverlayRenderer.Draw(_spriteBatch, map, _camera, _fogOfWar, time);
        _spriteBatch.End();

        // 6. Roofs/elevated layer
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _roofRenderer.Draw(_spriteBatch, map, _camera, _fogOfWar, _lightingSystem.AmbientColor);
        _spriteBatch.End();

        // 7. Weather on elevated surfaces (renders to internal RT, tinted by ambient)
        if (_weatherState != null && _weatherState.Intensity > 0.01f)
        {
            _weatherRenderer.DrawElevatedEffects(_spriteBatch, _weatherState, _camera, map, playerZoneId, _fogOfWar, _lightingSystem.AmbientColor);
        }

        // 8. Atmosphere tint + lightning flash
        if (_weatherState != null && (_weatherState.Intensity > 0.01f || _weatherState.LightningFlash > 0.01f))
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            _weatherRenderer.DrawOverlayEffects(_spriteBatch, _weatherState);
            _spriteBatch.End();
        }

        // 9. HUD
        if (_font != null)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            // Get player move anim type
            string moveType = "None";
            using (var players = ecsWorld.GetEntities()
                .With<PlayerControlled>()
                .With<MovementAnimation>()
                .AsSet())
            {
                foreach (ref readonly var e in players.GetEntities())
                {
                    moveType = e.Get<MovementAnimation>().Type.ToString();
                    break;
                }
            }
            string weatherInfo = _weatherState != null && _weatherState.Type != WeatherType.Clear
                ? $"  Weather: {_weatherState.Type} {_weatherState.Intensity:F1} [F2]"
                : "  Weather: Clear [F2]";
            string text = $"Light: {_lightingSystem.CurrentAmbientName} [L]  Move: {moveType} [M]{weatherInfo}";
            _spriteBatch.DrawString(_font, text, new Vector2(12, 12), Color.Black);
            _spriteBatch.DrawString(_font, text, new Vector2(10, 10), Color.White);
            _spriteBatch.End();
        }
    }
}

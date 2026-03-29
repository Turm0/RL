using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

public class RenderPipeline
{
    private Camera _camera;
    private TerrainRenderer _terrainRenderer;
    private EntityRenderer _entityRenderer;
    private RoofRenderer _roofRenderer;
    private EffectOverlayRenderer _effectOverlayRenderer;
    private LightingSystem _lightingSystem;
    private FogOfWar _fogOfWar;
    private SpriteBatch _spriteBatch;
    private GraphicsDevice _graphicsDevice;
    private GameWindow _window;

    private const int FovRadius = 30;

    public Camera Camera => _camera;
    public FogOfWar FogOfWar => _fogOfWar;
    public EffectOverlayRenderer EffectOverlay => _effectOverlayRenderer;

    public void Initialize(GraphicsDevice graphicsDevice, GameWindow window, TileMap map)
    {
        _graphicsDevice = graphicsDevice;
        _window = window;
        _spriteBatch = new SpriteBatch(graphicsDevice);

        _camera = new Camera
        {
            ViewportWidth = graphicsDevice.Viewport.Width,
            ViewportHeight = graphicsDevice.Viewport.Height
        };

        _terrainRenderer = new TerrainRenderer();
        _entityRenderer = new EntityRenderer(new VectorRasterizer(), new TextureCache());
        _roofRenderer = new RoofRenderer();
        _effectOverlayRenderer = new EffectOverlayRenderer();
        _lightingSystem = new LightingSystem();
        _fogOfWar = new FogOfWar(map.Width, map.Height);

        _window.ClientSizeChanged += OnClientSizeChanged;
    }

    private void OnClientSizeChanged(object sender, System.EventArgs e)
    {
        _camera.ViewportWidth = _graphicsDevice.Viewport.Width;
        _camera.ViewportHeight = _graphicsDevice.Viewport.Height;
    }

    public void Update(GameTime gameTime)
    {
        _camera.Update(gameTime);
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

        // Compute lighting
        _lightingSystem.Resize(visibleRect.Width, visibleRect.Height, _graphicsDevice);
        _lightingSystem.BeginFrame(visibleRect);
        _lightingSystem.SetViewerZone(playerZoneId);

        using var lights = ecsWorld.GetEntities()
            .With<Position>()
            .With<LightEmitter>()
            .AsSet();

        int seed = 0;
        foreach (ref readonly var entity in lights.GetEntities())
        {
            ref readonly var pos = ref entity.Get<Position>();
            ref readonly var light = ref entity.Get<LightEmitter>();

            if (pos.TileX + (int)light.Radius < visibleRect.X ||
                pos.TileX - (int)light.Radius >= visibleRect.X + visibleRect.Width ||
                pos.TileY + (int)light.Radius < visibleRect.Y ||
                pos.TileY - (int)light.Radius >= visibleRect.Y + visibleRect.Height)
            {
                seed++;
                continue;
            }

            _lightingSystem.AddLight(pos.TileX, pos.TileY, light.Radius, light.Intensity,
                light.Color, map, time, light.Flicker, light.FlickerIntensity, seed);
            seed++;
        }

        _lightingSystem.BuildTexture(_graphicsDevice, _fogOfWar);

        // Draw scene
        _graphicsDevice.Clear(Color.Black);

        // 1. Terrain + entities
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _terrainRenderer.Draw(_spriteBatch, map, _camera, _fogOfWar, time);
        _entityRenderer.Draw(_spriteBatch, _camera, ecsWorld, tileSize, _fogOfWar,
            (tx, ty) => _roofRenderer.IsHiddenByRoof(map, tx, ty, playerZoneId));
        _spriteBatch.End();

        // 2. Lighting overlay (multiply blend)
        _lightingSystem.Draw(_spriteBatch, _camera, tileSize);

        // 3. Effect overlays AFTER lighting — snow/wet/etc covers everything on the tile
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _effectOverlayRenderer.Draw(_spriteBatch, map, _camera, _fogOfWar, time);
        _spriteBatch.End();

        // 4. Roofs on top of everything
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _roofRenderer.Draw(_spriteBatch, map, _camera, _fogOfWar);
        _spriteBatch.End();
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Orchestrates all draw calls. Owns Camera, TerrainRenderer, EntityRenderer, LightingSystem, and FogOfWar.
/// </summary>
public class RenderPipeline
{
    private Camera _camera;
    private TerrainRenderer _terrainRenderer;
    private EntityRenderer _entityRenderer;
    private LightingSystem _lightingSystem;
    private FogOfWar _fogOfWar;
    private SpriteBatch _spriteBatch;
    private Texture2D _whitePixel;
    private GraphicsDevice _graphicsDevice;
    private GameWindow _window;

    private const int FovRadius = 30;

    /// <summary>The camera used for viewport transformations.</summary>
    public Camera Camera => _camera;

    /// <summary>The fog-of-war state, accessible for minimap and other systems.</summary>
    public FogOfWar FogOfWar => _fogOfWar;

    /// <summary>
    /// Initializes the render pipeline, creating shared resources.
    /// </summary>
    public void Initialize(GraphicsDevice graphicsDevice, GameWindow window, TileMap map)
    {
        _graphicsDevice = graphicsDevice;
        _window = window;
        _spriteBatch = new SpriteBatch(graphicsDevice);

        _whitePixel = new Texture2D(graphicsDevice, 1, 1);
        _whitePixel.SetData(new[] { Color.White });

        _camera = new Camera
        {
            ViewportWidth = graphicsDevice.Viewport.Width,
            ViewportHeight = graphicsDevice.Viewport.Height
        };

        _terrainRenderer = new TerrainRenderer();
        _entityRenderer = new EntityRenderer(new VectorRasterizer(), new TextureCache());
        _lightingSystem = new LightingSystem();
        _fogOfWar = new FogOfWar(map.Width, map.Height);

        _window.ClientSizeChanged += OnClientSizeChanged;
    }

    private void OnClientSizeChanged(object sender, System.EventArgs e)
    {
        _camera.ViewportWidth = _graphicsDevice.Viewport.Width;
        _camera.ViewportHeight = _graphicsDevice.Viewport.Height;
    }

    /// <summary>Updates the camera each frame.</summary>
    public void Update(GameTime gameTime)
    {
        _camera.Update(gameTime);
    }

    /// <summary>
    /// Recomputes the player FOV from the given tile position.
    /// </summary>
    public void UpdateFov(int playerTileX, int playerTileY, TileMap map)
    {
        _fogOfWar.Compute(playerTileX, playerTileY, FovRadius, map);
    }

    /// <summary>
    /// Draws the world: terrain, entities, then lighting overlay.
    /// </summary>
    public void Draw(GameTime gameTime, TileMap map, DefaultEcs.World ecsWorld)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = _camera.GetVisibleTileRect(tileSize);
        float time = (float)gameTime.TotalGameTime.TotalSeconds;

        // Compute lighting
        _lightingSystem.Resize(visibleRect.Width, visibleRect.Height, _graphicsDevice);
        _lightingSystem.BeginFrame(visibleRect);

        // Gather all light emitters — only add lights for tiles currently in player FOV
        using var lights = ecsWorld.GetEntities()
            .With<Position>()
            .With<LightEmitter>()
            .AsSet();

        int seed = 0;
        foreach (ref readonly var entity in lights.GetEntities())
        {
            ref readonly var pos = ref entity.Get<Position>();
            ref readonly var light = ref entity.Get<LightEmitter>();

            // Skip lights outside the visible rect
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

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _terrainRenderer.Draw(_spriteBatch, _whitePixel, map, _camera, _fogOfWar);
        _entityRenderer.Draw(_spriteBatch, _camera, ecsWorld, tileSize, _fogOfWar);
        _spriteBatch.End();

        // Composite lighting overlay
        _lightingSystem.Draw(_spriteBatch, _camera, tileSize);
    }
}

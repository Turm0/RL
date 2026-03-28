using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Orchestrates all draw calls. Owns Camera, TerrainRenderer, and EntityRenderer.
/// </summary>
public class RenderPipeline
{
    private Camera _camera;
    private TerrainRenderer _terrainRenderer;
    private EntityRenderer _entityRenderer;
    private SpriteBatch _spriteBatch;
    private Texture2D _whitePixel;
    private GraphicsDevice _graphicsDevice;
    private GameWindow _window;

    /// <summary>The camera used for viewport transformations.</summary>
    public Camera Camera => _camera;

    /// <summary>
    /// Initializes the render pipeline, creating shared resources.
    /// </summary>
    public void Initialize(GraphicsDevice graphicsDevice, GameWindow window)
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
    /// Draws the world: terrain first, then entities (y-sorted).
    /// </summary>
    public void Draw(GameTime gameTime, TileMap map, DefaultEcs.World ecsWorld)
    {
        _graphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);

        // Draw terrain
        _terrainRenderer.Draw(_spriteBatch, _whitePixel, map, _camera);

        // Draw entities (y-sorted, vector-rasterized)
        _entityRenderer.Draw(_spriteBatch, _camera, ecsWorld, GameConfig.TileSize);

        _spriteBatch.End();
    }
}

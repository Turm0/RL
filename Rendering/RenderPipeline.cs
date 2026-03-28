using DefaultEcs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Orchestrates all draw calls. Owns Camera and TerrainRenderer.
/// </summary>
public class RenderPipeline
{
    private Camera _camera;
    private TerrainRenderer _terrainRenderer;
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
    /// Draws the world: terrain first, then entities.
    /// </summary>
    public void Draw(GameTime gameTime, TileMap map, DefaultEcs.World ecsWorld)
    {
        _graphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // Draw terrain
        _terrainRenderer.Draw(_spriteBatch, _whitePixel, map, _camera);

        // Draw entities with Position + Renderable
        int tileSize = GameConfig.TileSize;
        int entitySize = (int)(tileSize * 0.8f);
        int offset = (tileSize - entitySize) / 2;

        using var entitySet = ecsWorld.GetEntities()
            .With<Position>()
            .With<Renderable>()
            .AsSet();

        foreach (ref readonly var entity in entitySet.GetEntities())
        {
            ref readonly var pos = ref entity.Get<Position>();
            ref readonly var renderable = ref entity.Get<Renderable>();

            if (!_camera.IsInView(pos.TileX, pos.TileY, tileSize))
                continue;

            var worldPos = new Vector2(pos.TileX * tileSize + offset, pos.TileY * tileSize + offset);
            var screenPos = _camera.WorldToScreen(worldPos);
            var destRect = new Rectangle((int)screenPos.X, (int)screenPos.Y, entitySize, entitySize);

            _spriteBatch.Draw(_whitePixel, destRect, renderable.Color);
        }

        _spriteBatch.End();
    }
}

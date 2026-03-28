using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.ECS.Systems;
using RoguelikeEngine.Rendering;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Core;

/// <summary>
/// MonoGame entry point. Owns the RenderPipeline and ECS World.
/// </summary>
public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private RenderPipeline _renderPipeline;
    private DefaultEcs.World _ecsWorld;
    private TileMap _tileMap;
    private PlayerInputSystem _playerInputSystem;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;

        _graphics.PreferredBackBufferWidth = GameConfig.DefaultWindowWidth;
        _graphics.PreferredBackBufferHeight = GameConfig.DefaultWindowHeight;
    }

    protected override void Initialize()
    {
        base.Initialize();

        _tileMap = TileMap.CreateTestDungeon();
        _ecsWorld = new DefaultEcs.World();

        _renderPipeline = new RenderPipeline();
        _renderPipeline.Initialize(GraphicsDevice, Window);

        // Player
        var player = _ecsWorld.CreateEntity();
        player.Set(new Position(5, 5));
        player.Set(new SpriteShape("player", 1.0f));
        player.Set(new PlayerControlled());

        // Goblins in Room 2
        SpawnCreature(18, 5, "goblin", 0.7f);
        SpawnCreature(22, 9, "goblin", 0.7f);

        // Rats in Room 3
        SpawnCreature(5, 16, "rat", 0.45f);
        SpawnCreature(9, 20, "rat", 0.45f);

        // Skeleton in Room 4
        SpawnCreature(22, 18, "skeleton", 0.85f);

        // Dragon in Room 4
        SpawnCreature(25, 20, "dragon", 1.4f);

        // Ghost in corridor
        SpawnCreature(14, 6, "ghost", 0.9f);

        // Fire Elemental in Room 2
        SpawnCreature(20, 7, "fire_elemental", 1.0f);

        // Set initial camera target and snap to position
        _renderPipeline.Camera.TargetTile = new Point(5, 5);
        _renderPipeline.Camera.Position = new Vector2(
            5 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportWidth / 2f,
            5 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportHeight / 2f);

        _playerInputSystem = new PlayerInputSystem(_ecsWorld, _tileMap, _renderPipeline.Camera);
    }

    private void SpawnCreature(int x, int y, string creatureType, float size)
    {
        var entity = _ecsWorld.CreateEntity();
        entity.Set(new Position(x, y));
        entity.Set(new SpriteShape(creatureType, size));
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _playerInputSystem.Update(gameTime);
        _renderPipeline.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _renderPipeline.Draw(gameTime, _tileMap, _ecsWorld);
        base.Draw(gameTime);
    }
}

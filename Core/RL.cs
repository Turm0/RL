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
public class RL : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private RenderPipeline _renderPipeline;
    private DefaultEcs.World _ecsWorld;
    private TileMap _tileMap;
    private PlayerInputSystem _playerInputSystem;

    public RL()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;

        _graphics.PreferredBackBufferWidth = GameConfig.DefaultWindowWidth;
        _graphics.PreferredBackBufferHeight = GameConfig.DefaultWindowHeight;
        _graphics.IsFullScreen = true;
        _graphics.HardwareModeSwitch = false; // borderless fullscreen
    }

    protected override void Initialize()
    {
        base.Initialize();

        _tileMap = TileMap.CreateTestDungeon();
        _ecsWorld = new DefaultEcs.World();

        _renderPipeline = new RenderPipeline();
        _renderPipeline.Initialize(GraphicsDevice, Window, _tileMap);

        // Player with carried light
        var player = _ecsWorld.CreateEntity();
        player.Set(new Position(5, 5));
        player.Set(new SpriteShape("creatures/human_ranger.yaml", 1.0f));
        player.Set(new PlayerControlled());
        player.Set(new LightEmitter(8f, 1.0f, new Vector3(1.2f, 1.1f, 0.9f), true, 0.15f));

        // Room 2 — mage and knight
        SpawnCreature(18, 5, "creatures/human_mage.yaml", 1.0f);
        SpawnCreature(22, 9, "creatures/human_knight.yaml", 1.0f);

        // Room 3 — thief and cleric
        SpawnCreature(5, 16, "creatures/human_thief.yaml", 1.0f);
        SpawnCreature(9, 20, "creatures/human_cleric.yaml", 1.0f);

        // Room 4 — orc and dark elf
        SpawnCreature(22, 18, "creatures/orc_warrior.yaml", 1.0f);
        SpawnCreature(25, 20, "creatures/dark_elf.yaml", 1.0f);

        // Corridor — undead
        SpawnCreature(14, 6, "creatures/undead.yaml", 1.0f);

        // Room 2 — dwarf smith
        SpawnCreature(20, 7, "creatures/dwarf_smith.yaml", 1.0f);

        // One torch per room
        SpawnTorch(7, 6);    // Room 1 center
        SpawnTorch(21, 7);   // Room 2 center
        SpawnTorch(8, 18);   // Room 3 center
        SpawnTorch(23, 19);  // Room 4 center

        // Set initial camera target and snap to position
        _renderPipeline.Camera.TargetTile = new Point(5, 5);
        _renderPipeline.Camera.Position = new Vector2(
            5 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportWidth / 2f,
            5 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportHeight / 2f);

        _playerInputSystem = new PlayerInputSystem(_ecsWorld, _tileMap, _renderPipeline.Camera);

        // Compute initial FOV
        _renderPipeline.UpdateFov(5, 5, _tileMap);
    }

    private DefaultEcs.Entity SpawnCreature(int x, int y, string creatureType, float size)
    {
        var entity = _ecsWorld.CreateEntity();
        entity.Set(new Position(x, y));
        entity.Set(new SpriteShape(creatureType, size));
        return entity;
    }

    private void SpawnTorch(int x, int y)
    {
        var entity = _ecsWorld.CreateEntity();
        entity.Set(new Position(x, y));
        entity.Set(new SpriteShape("torch", 0.5f));
        entity.Set(new LightEmitter(8f, 1.1f, new Vector3(1.3f, 1.0f, 0.65f), true, 0.3f));
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _playerInputSystem.Update(gameTime);
        _renderPipeline.Update(gameTime);

        // Update FOV from current player position
        UpdatePlayerFov();

        base.Update(gameTime);
    }

    private void UpdatePlayerFov()
    {
        using var players = _ecsWorld.GetEntities()
            .With<PlayerControlled>()
            .With<Position>()
            .AsSet();

        foreach (ref readonly var entity in players.GetEntities())
        {
            ref readonly var pos = ref entity.Get<Position>();
            _renderPipeline.UpdateFov(pos.TileX, pos.TileY, _tileMap);
            break; // only one player
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        _renderPipeline.Draw(gameTime, _tileMap, _ecsWorld);
        base.Draw(gameTime);
    }
}

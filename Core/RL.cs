using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RoguelikeEngine.Data;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.ECS.Systems;
using RoguelikeEngine.Rendering;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Core;

public class RL : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private RenderPipeline _renderPipeline;
    private DefaultEcs.World _ecsWorld;
    private TileMap _tileMap;
    private PlayerInputSystem _playerInputSystem;
    private WeatherSystem _weatherSystem;

    public RL()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;

        _graphics.PreferredBackBufferWidth = GameConfig.DefaultWindowWidth;
        _graphics.PreferredBackBufferHeight = GameConfig.DefaultWindowHeight;
        _graphics.IsFullScreen = true;
        _graphics.HardwareModeSwitch = false;
    }

    protected override void Initialize()
    {
        base.Initialize();

        _tileMap = CreateDemoLevel();
        _ecsWorld = new DefaultEcs.World();

        _renderPipeline = new RenderPipeline();
        _renderPipeline.Initialize(GraphicsDevice, Window, _tileMap);

        SpawnEntities();

        _renderPipeline.Camera.TargetTile = new Point(15, 18);
        _renderPipeline.Camera.Position = new Vector2(
            15 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportWidth / 2f,
            18 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportHeight / 2f);

        _playerInputSystem = new PlayerInputSystem(_ecsWorld, _tileMap, _renderPipeline.Camera);

        // Weather system disabled for now
        // _weatherSystem = new WeatherSystem(_tileMap, _renderPipeline.EffectOverlay,
        //     snowX1: 1, snowY1: 28, snowX2: 12, snowY2: 38);

        _renderPipeline.UpdateFov(15, 18, _tileMap);
    }

    private void SpawnEntities()
    {
        // Player in the outdoor area
        var player = _ecsWorld.CreateEntity();
        player.Set(new Position(15, 18));
        player.Set(new SpriteShape("creatures/human_ranger.yaml", 1.0f));
        player.Set(new PlayerControlled());
        player.Set(new RenderLayer(RenderLayer.CreatureLayer));
        player.Set(new LightEmitter(10f, 1.0f, new Vector3(1.2f, 1.1f, 0.9f), true, 0.15f));

        // --- Outdoor NPCs ---
        SpawnCreature(20, 17, "creatures/human_mage.yaml");
        SpawnCreature(12, 22, "creatures/orc_warrior.yaml");

        // --- Cottage interior NPCs ---
        SpawnCreature(7, 7, "creatures/human_knight.yaml");
        SpawnCreature(9, 8, "creatures/dwarf_smith.yaml");

        // --- Cave NPCs ---
        SpawnCreature(38, 8, "creatures/undead.yaml");
        SpawnCreature(42, 11, "creatures/dark_elf.yaml");

        // --- Tavern NPCs ---
        SpawnCreature(28, 29, "creatures/human_thief.yaml");
        SpawnCreature(32, 31, "creatures/human_cleric.yaml");

        // === Torches ===
        SpawnTorch(8, 5);    // Cottage
        SpawnTorch(17, 18);  // Village center
        SpawnTorch(22, 17);  // Near path
        SpawnTorch(36, 8);   // Cave entrance
        SpawnTorch(40, 10);  // Deep cave
        SpawnTorch(30, 28);  // Tavern
        SpawnTorch(33, 32);  // Tavern back
    }

    private DefaultEcs.Entity SpawnCreature(int x, int y, string creatureType, float size = 1.0f)
    {
        var entity = _ecsWorld.CreateEntity();
        entity.Set(new Position(x, y));
        entity.Set(new SpriteShape(creatureType, size));
        entity.Set(new RenderLayer(RenderLayer.CreatureLayer));
        return entity;
    }

    private void SpawnTerrainObject(int x, int y, string objectType, bool blocksMove, bool blocksLight)
    {
        var entity = _ecsWorld.CreateEntity();
        entity.Set(new Position(x, y));
        entity.Set(new SpriteShape(objectType, 1.0f));
        entity.Set(new TerrainObject(objectType, blocksMove, blocksLight));
        entity.Set(new RenderLayer(RenderLayer.TerrainObjectLayer));

        _tileMap.SetObjectBlocking(x, y, blocksMove, blocksLight);
    }

    private void SpawnTorch(int x, int y)
    {
        var entity = _ecsWorld.CreateEntity();
        entity.Set(new Position(x, y));
        entity.Set(new SpriteShape("torch", 0.5f));
        entity.Set(new RenderLayer(RenderLayer.TerrainObjectLayer));
        entity.Set(new LightEmitter(8f, 1.1f, new Vector3(1.3f, 1.0f, 0.65f), true, 0.3f));
    }

    private static TileMap CreateDemoLevel()
    {
        int W = 50, H = 40;
        var map = new TileMap(W, H);

        // Fill with grass (outdoor default)
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                map.SetTile(x, y, new TileData(TerrainId.Grass, WallType.None,
                    TileMap.ComputeVariantSeed(x, y)));

        // --- Border walls ---
        for (int x = 0; x < W; x++)
        {
            SetWall(map, x, 0, TerrainId.Stone, WallType.StoneWall);
            SetWall(map, x, H - 1, TerrainId.Stone, WallType.StoneWall);
        }
        for (int y = 0; y < H; y++)
        {
            SetWall(map, 0, y, TerrainId.Stone, WallType.StoneWall);
            SetWall(map, W - 1, y, TerrainId.Stone, WallType.StoneWall);
        }

        // === OUTDOOR AREA (center, y=13..26) ===
        // Dirt path running east-west
        for (int x = 1; x < W - 1; x++)
            for (int dy = -1; dy <= 1; dy++)
                SetFloor(map, x, 18 + dy, TerrainId.Dirt);

        // Dirt path running north-south
        for (int y = 1; y < H - 1; y++)
            SetFloor(map, 15, y, TerrainId.Dirt);
        for (int y = 1; y < H - 1; y++)
            SetFloor(map, 16, y, TerrainId.Dirt);

        // Pond (water with deep water center)
        for (int x = 6; x <= 10; x++)
            for (int y = 17; y <= 21; y++)
                SetFloor(map, x, y, TerrainId.Water);
        for (int x = 7; x <= 9; x++)
            for (int y = 18; y <= 20; y++)
                SetFloor(map, x, y, TerrainId.DeepWater);

        // Sandy beach around pond
        for (int x = 5; x <= 11; x++)
        {
            SetFloor(map, x, 16, TerrainId.Sand);
            SetFloor(map, x, 22, TerrainId.Sand);
        }
        for (int y = 16; y <= 22; y++)
        {
            SetFloor(map, 5, y, TerrainId.Sand);
            SetFloor(map, 11, y, TerrainId.Sand);
        }

        // === COTTAGE (top-left, zone 1 with thatch roof) ===
        ushort cottageZone = 1;
        var cottageBounds = new Rectangle(4, 4, 9, 7);
        map.RegisterZone(new ZoneDefinition
        {
            Id = cottageZone,
            HasRoof = true,
            RoofMaterial = RoofMaterialType.WoodShingle,
            Bounds = cottageBounds
        });

        // Cottage walls
        for (int x = 3; x <= 12; x++)
        {
            SetWall(map, x, 3, TerrainId.Wood, WallType.WoodWall);
            SetWall(map, x, 10, TerrainId.Wood, WallType.WoodWall);
        }
        for (int y = 3; y <= 10; y++)
        {
            SetWall(map, 3, y, TerrainId.Wood, WallType.WoodWall);
            SetWall(map, 12, y, TerrainId.Wood, WallType.WoodWall);
        }
        // Interior: wood floor
        for (int x = 4; x <= 11; x++)
            for (int y = 4; y <= 9; y++)
            {
                SetFloor(map, x, y, TerrainId.Wood, cottageZone);
            }
        // Door (south wall gap)
        SetFloor(map, 8, 10, TerrainId.Wood, cottageZone);
        // Path from door to main path
        for (int y = 11; y <= 17; y++)
            SetFloor(map, 8, y, TerrainId.Dirt);

        // === CAVE (top-right) ===
        // Cave entrance and tunnels with cave floor and cave walls
        for (int x = 34; x <= 45; x++)
            for (int y = 2; y <= 14; y++)
                SetWall(map, x, y, TerrainId.CaveFloor, WallType.CaveWall);

        // Cave rooms
        for (int x = 35; x <= 44; x++)
            for (int y = 3; y <= 5; y++)
                SetFloor(map, x, y, TerrainId.CaveFloor);
        for (int x = 35; x <= 38; x++)
            for (int y = 6; y <= 13; y++)
                SetFloor(map, x, y, TerrainId.CaveFloor);
        for (int x = 39; x <= 44; x++)
            for (int y = 8; y <= 13; y++)
                SetFloor(map, x, y, TerrainId.CaveFloor);
        // Connecting corridor
        for (int x = 39; x <= 40; x++)
            for (int y = 5; y <= 8; y++)
                SetFloor(map, x, y, TerrainId.CaveFloor);

        // Cave entrance connects to outdoor at y=5
        for (int x = 27; x <= 34; x++)
            SetFloor(map, x, 4, TerrainId.Dirt);
        SetFloor(map, 34, 4, TerrainId.CaveFloor);

        // Lava pool in deep cave
        for (int x = 42; x <= 44; x++)
            for (int y = 10; y <= 12; y++)
                SetFloor(map, x, y, TerrainId.Lava);

        // === TAVERN (bottom-right, zone 2 with stone tile roof) ===
        ushort tavernZone = 2;
        var tavernBounds = new Rectangle(27, 27, 10, 8);
        map.RegisterZone(new ZoneDefinition
        {
            Id = tavernZone,
            HasRoof = true,
            RoofMaterial = RoofMaterialType.WoodShingle,
            Bounds = tavernBounds
        });

        // Tavern walls (brick)
        for (int x = 26; x <= 36; x++)
        {
            SetWall(map, x, 26, TerrainId.Stone, WallType.BrickWall);
            SetWall(map, x, 34, TerrainId.Stone, WallType.BrickWall);
        }
        for (int y = 26; y <= 34; y++)
        {
            SetWall(map, 26, y, TerrainId.Stone, WallType.BrickWall);
            SetWall(map, 36, y, TerrainId.Stone, WallType.BrickWall);
        }
        // Interior: stone floor
        for (int x = 27; x <= 35; x++)
            for (int y = 27; y <= 33; y++)
                SetFloor(map, x, y, TerrainId.Stone, tavernZone);
        // Door (north wall)
        SetFloor(map, 30, 26, TerrainId.Stone, tavernZone);
        // Path from tavern to main path
        for (int y = 19; y <= 25; y++)
            SetFloor(map, 30, y, TerrainId.Dirt);

        // === ICE AREA (bottom-left) ===
        for (int x = 2; x <= 10; x++)
            for (int y = 30; y <= 37; y++)
                SetFloor(map, x, y, TerrainId.Ice);
        // Snow around ice
        for (int x = 1; x <= 11; x++)
        {
            SetFloor(map, x, 29, TerrainId.Grass);
            SetFloor(map, x, 38, TerrainId.Grass);
        }

        return map;
    }

    private static void SetFloor(TileMap map, int x, int y, TerrainId terrain, ushort zoneId = 0)
    {
        if (!map.IsInBounds(x, y)) return;
        map.SetTile(x, y, new TileData(terrain, WallType.None,
            TileMap.ComputeVariantSeed(x, y), zoneId));
    }

    private static void SetWall(TileMap map, int x, int y, TerrainId terrain, WallType wall)
    {
        if (!map.IsInBounds(x, y)) return;
        map.SetTile(x, y, new TileData(terrain, wall,
            TileMap.ComputeVariantSeed(x, y)));
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _playerInputSystem.Update(gameTime);
        _renderPipeline.Update(gameTime);
        _weatherSystem?.Update(gameTime);
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
            break;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        _renderPipeline.Draw(gameTime, _tileMap, _ecsWorld);
        base.Draw(gameTime);
    }
}

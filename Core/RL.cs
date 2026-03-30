using System;
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
    private WorldClock _worldClock;
    private WeatherSystem _weatherSystem;
    private KeyboardState _prevKeyboard;
    private int _lastFovX = int.MinValue;
    private int _lastFovY = int.MinValue;

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
        _tileMap.PopulateElevation();
        _ecsWorld = new DefaultEcs.World();

        var font = Content.Load<Microsoft.Xna.Framework.Graphics.SpriteFont>("DefaultFont");
        var postProcessFx = Content.Load<Microsoft.Xna.Framework.Graphics.Effect>("Effects/PostProcess");

        _renderPipeline = new RenderPipeline();
        _renderPipeline.Initialize(GraphicsDevice, Window, _tileMap, font, postProcessFx);

        SpawnEntities();

        _renderPipeline.Camera.TargetTile = new Point(15, 18);
        _renderPipeline.Camera.Position = new Vector2(
            15 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportWidth / 2f,
            18 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportHeight / 2f);

        _playerInputSystem = new PlayerInputSystem(_ecsWorld, _tileMap, _renderPipeline.Camera);

        _worldClock = new WorldClock();
        _weatherSystem = new WeatherSystem(_worldClock);
        _renderPipeline.SetWeatherState(_weatherSystem.State);

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
        player.Set(new MovementAnimation(12f, MoveAnimType.Slide));

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

        // Pond — just Water tiles, depth computed automatically
        for (int x = 6; x <= 10; x++)
            for (int y = 17; y <= 21; y++)
                SetFloor(map, x, y, TerrainId.Water);

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

        // === CAVE (top-right) — organic shape using distance from center ===
        ushort caveZone = 3;
        int caveCX = 40, caveCY = 9; // cave center
        int caveRadius = 7;
        int caveBoundsX1 = caveCX - caveRadius - 1;
        int caveBoundsY1 = caveCY - caveRadius - 1;
        int caveBoundsW = caveRadius * 2 + 3;
        int caveBoundsH = caveRadius * 2 + 3;

        map.RegisterZone(new ZoneDefinition
        {
            Id = caveZone,
            HasRoof = true,
            RoofMaterial = RoofMaterialType.CaveStone,
            Bounds = new Rectangle(caveBoundsX1, caveBoundsY1, caveBoundsW, caveBoundsH)
        });

        // First fill the area with cave walls
        for (int x = caveBoundsX1; x < caveBoundsX1 + caveBoundsW; x++)
            for (int y = caveBoundsY1; y < caveBoundsY1 + caveBoundsH; y++)
                if (map.IsInBounds(x, y))
                    SetWall(map, x, y, TerrainId.CaveFloor, WallType.CaveWall);

        // Carve organic cave shape using noise-perturbed distance
        for (int x = caveBoundsX1; x < caveBoundsX1 + caveBoundsW; x++)
        {
            for (int y = caveBoundsY1; y < caveBoundsY1 + caveBoundsH; y++)
            {
                if (!map.IsInBounds(x, y)) continue;
                float dx = x - caveCX;
                float dy = y - caveCY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                // Noise-perturbed radius for organic shape
                int h = TileMap.ComputeVariantSeed(x, y);
                float noise = ((h & 0xFF) / 255f - 0.5f) * 3f; // ±1.5 tile wobble
                float effectiveRadius = caveRadius + noise;

                if (dist < effectiveRadius)
                    SetFloor(map, x, y, TerrainId.CaveFloor, caveZone);
            }
        }

        // Cave entrance — winding tunnel to the west
        for (int x = 27; x <= caveCX - caveRadius + 2; x++)
        {
            int tunnelY = 5 + (int)(MathF.Sin(x * 0.5f) * 1.2f);
            SetFloor(map, x, tunnelY, TerrainId.CaveFloor, caveZone);
            SetFloor(map, x, tunnelY + 1, TerrainId.CaveFloor, caveZone);
            // Cave walls around tunnel
            if (map.IsInBounds(x, tunnelY - 1) && !map.GetTile(x, tunnelY - 1).HasWall)
                SetWall(map, x, tunnelY - 1, TerrainId.CaveFloor, WallType.CaveWall);
            if (map.IsInBounds(x, tunnelY + 2) && !map.GetTile(x, tunnelY + 2).HasWall)
                SetWall(map, x, tunnelY + 2, TerrainId.CaveFloor, WallType.CaveWall);
        }
        // Connect entrance to dirt path
        for (int x = 25; x <= 27; x++)
            SetFloor(map, x, 5, TerrainId.Dirt);

        // Lava pool in deep cave (southeast area)
        for (int x = 42; x <= 44; x++)
            for (int y = 10; y <= 12; y++)
                if (!map.GetTile(x, y).HasWall)
                    SetFloor(map, x, y, TerrainId.Lava, caveZone);

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

        // Compute water depth from distance to shore
        map.ComputeWaterDepth();

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
        var kb = Keyboard.GetState();

        if (kb.IsKeyDown(Keys.Escape))
            Exit();

        // L key: cycle ambient light mode
        if (kb.IsKeyDown(Keys.L) && !_prevKeyboard.IsKeyDown(Keys.L))
            _renderPipeline.Lighting.CycleAmbient();

        // M key: cycle movement animation mode
        if (kb.IsKeyDown(Keys.M) && !_prevKeyboard.IsKeyDown(Keys.M))
            CycleMoveAnim();

        // F2 key: cycle weather mode (manual override)
        if (kb.IsKeyDown(Keys.F2) && !_prevKeyboard.IsKeyDown(Keys.F2))
            _weatherSystem.CycleManual();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _worldClock.Update(dt);
        _playerInputSystem.Update(gameTime);
        _renderPipeline.Update(gameTime);
        _weatherSystem.Update(gameTime);
        UpdatePlayerFov();

        _prevKeyboard = kb;
        base.Update(gameTime);
    }

    private void CycleMoveAnim()
    {
        using var players = _ecsWorld.GetEntities()
            .With<PlayerControlled>()
            .With<MovementAnimation>()
            .AsSet();

        foreach (ref readonly var entity in players.GetEntities())
        {
            ref var anim = ref entity.Get<MovementAnimation>();
            anim.Type = (MoveAnimType)(((int)anim.Type + 1) % 4);
        }
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
            if (pos.TileX == _lastFovX && pos.TileY == _lastFovY)
                return;
            _lastFovX = pos.TileX;
            _lastFovY = pos.TileY;
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

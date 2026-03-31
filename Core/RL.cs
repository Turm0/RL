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
    private CreatureFactory _creatureFactory;
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

        // Initialize content registry — auto-discover all YAML content
        var registry = new ContentRegistry();
        string spritesRoot = System.IO.Path.Combine(AppContext.BaseDirectory, "Content", "Sprites");
        registry.ScanDirectory(spritesRoot);

        _tileMap = CreateDemoLevel();
        _tileMap.PopulateElevation();
        _ecsWorld = new DefaultEcs.World();

        var font = Content.Load<Microsoft.Xna.Framework.Graphics.SpriteFont>("DefaultFont");
        var postProcessFx = Content.Load<Microsoft.Xna.Framework.Graphics.Effect>("Effects/PostProcess");

        _creatureFactory = new CreatureFactory(registry);

        _renderPipeline = new RenderPipeline();
        _renderPipeline.Initialize(GraphicsDevice, Window, _tileMap, font, postProcessFx);

        SpawnEntities();

        // Start player in the village square
        _renderPipeline.Camera.TargetTile = new Point(40, 35);
        _renderPipeline.Camera.Position = new Vector2(
            40 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportWidth / 2f,
            35 * GameConfig.TileSize + GameConfig.TileSize / 2f - _renderPipeline.Camera.ViewportHeight / 2f);

        _playerInputSystem = new PlayerInputSystem(_ecsWorld, _tileMap, _renderPipeline.Camera);

        _worldClock = new WorldClock();
        _weatherSystem = new WeatherSystem(_worldClock);
        _renderPipeline.SetWeatherState(_weatherSystem.State);

        _renderPipeline.UpdateFov(40, 35, _tileMap);
    }

    private void SpawnEntities()
    {
        // Player — spawned via factory then enhanced with player-specific components
        var player = _creatureFactory.Spawn(_ecsWorld, 40, 35, "species.human", "occupation.ranger");
        player.Set(new PlayerControlled());
        player.Set(new LightEmitter(10f, 1.0f, new Vector3(1.2f, 1.1f, 0.9f), true, 0.15f));
        player.Set(new MovementAnimation(12f, MoveAnimType.Slide));

        // --- Village NPCs (random humans) ---
        _creatureFactory.Spawn(_ecsWorld, 38, 33, "species.human", "occupation.mage");
        _creatureFactory.Spawn(_ecsWorld, 43, 36, "species.human", "occupation.guard");
        _creatureFactory.Spawn(_ecsWorld, 35, 38, "species.human", "occupation.ranger");

        // --- House interiors ---
        _creatureFactory.Spawn(_ecsWorld, 33, 27, "species.human", "occupation.guard");
        _creatureFactory.Spawn(_ecsWorld, 51, 26, "species.human", "occupation.guard");
        _creatureFactory.Spawn(_ecsWorld, 48, 44, "species.human", "occupation.mage");

        // --- Tavern ---
        _creatureFactory.Spawn(_ecsWorld, 35, 48, "species.human", "occupation.ranger");
        _creatureFactory.Spawn(_ecsWorld, 39, 50, "species.human", "occupation.guard");

        // --- Mountain caves ---
        _creatureFactory.Spawn(_ecsWorld, 25, 12, "species.orc", "occupation.guard");
        _creatureFactory.Spawn(_ecsWorld, 30, 8, "species.orc", "occupation.ranger");
        _creatureFactory.Spawn(_ecsWorld, 18, 18, "species.elf", "occupation.mage");
        _creatureFactory.Spawn(_ecsWorld, 35, 15, "species.orc", "occupation.guard");

        // === Torches ===
        // Village
        SpawnTorch(37, 34);  // Square west
        SpawnTorch(43, 34);  // Square east
        SpawnTorch(40, 31);  // Square north
        SpawnTorch(40, 39);  // Square south

        // Houses
        SpawnTorch(33, 26);  // House 1
        SpawnTorch(51, 25);  // Smithy
        SpawnTorch(48, 43);  // House 3

        // Tavern
        SpawnTorch(35, 47);
        SpawnTorch(40, 50);

        // Mountain/cave
        SpawnTorch(28, 22);  // Cave entrance area
        SpawnTorch(22, 15);  // Deep cave
        SpawnTorch(32, 10);  // Upper cave
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
        entity.Set(new SpriteShape("objects/torch.yaml", 1.0f)); // TODO: use registry key
        entity.Set(new RenderLayer(RenderLayer.TerrainObjectLayer));
        entity.Set(new LightEmitter(8f, 1.1f, new Vector3(1.3f, 1.0f, 0.65f), true, 0.3f));
    }

    private static TileMap CreateDemoLevel()
    {
        int W = 80, H = 60;
        var map = new TileMap(W, H);

        // ============================================================
        // LAYOUT:
        //   Top half (y=0..30):  Mountain with cave system
        //   Bottom half (y=30..60): Village with river
        //   River runs roughly north-south through the middle
        // ============================================================

        // === BASE TERRAIN ===
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                TerrainId terrain;
                if (y < 25)
                    terrain = TerrainId.Stone; // Mountain base
                else if (y < 30)
                    terrain = TerrainId.Dirt;  // Mountain-village transition
                else
                    terrain = TerrainId.Grass; // Village area

                map.SetTile(x, y, new TileData(terrain, WallType.None,
                    TileMap.ComputeVariantSeed(x, y)));
            }
        }

        // === MOUNTAIN AREA (top) ===
        // Fill mountain with cave walls — organic edge
        for (int x = 1; x < W - 1; x++)
        {
            for (int y = 1; y < 28; y++)
            {
                // Organic mountain edge — noise-perturbed boundary
                int h = TileMap.ComputeVariantSeed(x, y);
                float noise = ((h & 0xFF) / 255f - 0.5f) * 4f;
                float mountainEdge = 26 + noise + MathF.Sin(x * 0.3f) * 2f;

                if (y < mountainEdge)
                    SetWall(map, x, y, TerrainId.CaveFloor, WallType.CaveWall);
            }
        }

        // === CAVE SYSTEM ===
        // Main cavern (zone 10)
        ushort mainCaveZone = 10;
        int caveCX = 25, caveCY = 12;
        int caveR = 8;
        // Bounds cover both main + secondary caverns
        int boundsX1 = Math.Min(caveCX - caveR - 1, 35 - 5 - 1);
        int boundsY1 = Math.Min(caveCY - caveR - 1, 8 - 5 - 1);
        int boundsX2 = Math.Max(caveCX + caveR + 2, 35 + 5 + 2);
        int boundsY2 = caveCY + caveR + 2;
        var mainCaveBounds = new Rectangle(boundsX1, boundsY1,
            boundsX2 - boundsX1, boundsY2 - boundsY1);
        map.RegisterZone(new ZoneDefinition
        {
            Id = mainCaveZone, HasRoof = true,
            RoofMaterial = RoofMaterialType.CaveStone, Bounds = mainCaveBounds
        });

        // Carve main cavern
        for (int x = mainCaveBounds.X; x < mainCaveBounds.Right; x++)
        {
            for (int y = mainCaveBounds.Y; y < mainCaveBounds.Bottom; y++)
            {
                if (!map.IsInBounds(x, y)) continue;
                float dx = x - caveCX, dy = y - caveCY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                int h = TileMap.ComputeVariantSeed(x, y);
                float noise = ((h & 0xFF) / 255f - 0.5f) * 3f;
                if (dist < caveR + noise)
                    SetFloor(map, x, y, TerrainId.CaveFloor, mainCaveZone);
            }
        }

        // Secondary cavern — part of the same cave system (same zone)
        int cave2CX = 35, cave2CY = 8;
        int cave2R = 5;

        for (int x = cave2CX - cave2R - 1; x <= cave2CX + cave2R + 1; x++)
        {
            for (int y = cave2CY - cave2R - 1; y <= cave2CY + cave2R + 1; y++)
            {
                if (!map.IsInBounds(x, y)) continue;
                float dx = x - cave2CX, dy = y - cave2CY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                int h = TileMap.ComputeVariantSeed(x, y);
                float noise = ((h & 0xFF) / 255f - 0.5f) * 2.5f;
                if (dist < cave2R + noise)
                    SetFloor(map, x, y, TerrainId.CaveFloor, mainCaveZone);
            }
        }

        // Tunnel connecting main cavern to secondary cavern
        for (int x = caveCX + caveR - 2; x <= cave2CX - cave2R + 2; x++)
        {
            int tunnelY = (int)(10 + MathF.Sin(x * 0.4f) * 1.5f);
            SetFloor(map, x, tunnelY, TerrainId.CaveFloor, mainCaveZone);
            SetFloor(map, x, tunnelY + 1, TerrainId.CaveFloor, mainCaveZone);
        }

        // Lava pool in secondary cavern
        for (int x = 34; x <= 37; x++)
            for (int y = 6; y <= 8; y++)
                if (!map.GetTile(x, y).HasWall)
                    SetFloor(map, x, y, TerrainId.Lava, mainCaveZone);

        // Cave entrance — tunnel from mountain face down to village area
        for (int y = caveCY + caveR - 2; y <= 28; y++)
        {
            int tunnelX = (int)(28 + MathF.Sin(y * 0.3f) * 1.5f);
            SetFloor(map, tunnelX, y, TerrainId.CaveFloor, mainCaveZone);
            SetFloor(map, tunnelX + 1, y, TerrainId.CaveFloor, mainCaveZone);
            // Walls around tunnel
            if (map.IsInBounds(tunnelX - 1, y) && map.GetTile(tunnelX - 1, y).HasWall)
                SetWall(map, tunnelX - 1, y, TerrainId.CaveFloor, WallType.CaveWall);
            if (map.IsInBounds(tunnelX + 2, y) && map.GetTile(tunnelX + 2, y).HasWall)
                SetWall(map, tunnelX + 2, y, TerrainId.CaveFloor, WallType.CaveWall);
        }
        // Cave entrance — carve a clear opening through the mountain face
        // Force-clear walls at the tunnel exit point regardless of what's there
        for (int y = 24; y <= 32; y++)
            for (int x = 27; x <= 30; x++)
                SetFloor(map, x, y, TerrainId.Dirt);

        // === RIVER (runs north-south through village, slight curve) ===
        for (int y = 0; y < H; y++)
        {
            float riverCenterX = 55 + MathF.Sin(y * 0.08f) * 4f;
            int riverWidth = y < 25 ? 2 : 3; // narrower in mountain

            for (int dx = -riverWidth; dx <= riverWidth; dx++)
            {
                int rx = (int)(riverCenterX + dx);
                if (!map.IsInBounds(rx, y)) continue;
                if (map.GetTile(rx, y).HasWall) continue; // don't carve through mountain walls

                TerrainId water = TerrainId.Water;
                SetFloor(map, rx, y, water);
            }

            // Sandy banks
            for (int side = -1; side <= 1; side += 2)
            {
                int bankX = (int)(riverCenterX + (riverWidth + 1) * side);
                if (map.IsInBounds(bankX, y) && !map.GetTile(bankX, y).HasWall)
                    SetFloor(map, bankX, y, TerrainId.Sand);
            }
        }

        // === VILLAGE PATHS (dirt roads) ===
        // Main east-west road
        for (int x = 1; x < W - 1; x++)
        {
            if (map.GetTile(x, 35).HasWall) continue;
            var t = map.GetTile(x, 35);
            if (t.Terrain == TerrainId.Water || t.Terrain == TerrainId.DeepWater) continue;
            SetFloor(map, x, 35, TerrainId.Dirt);
            if (!map.GetTile(x, 36).HasWall) SetFloor(map, x, 36, TerrainId.Dirt);
        }

        // North-south village road
        for (int y = 30; y < H - 1; y++)
        {
            SetFloor(map, 40, y, TerrainId.Dirt);
            SetFloor(map, 41, y, TerrainId.Dirt);
        }

        // Path to mountain/cave entrance
        for (int y = 28; y <= 35; y++)
        {
            SetFloor(map, 29, y, TerrainId.Dirt);
            SetFloor(map, 30, y, TerrainId.Dirt);
        }

        // === VILLAGE BUILDINGS (well-spaced, minimum 3 tiles gap) ===

        // House 1 — northwest of square (zone 1, wood cottage)
        BuildHouse(map, 30, 25, 7, 5, 1, TerrainId.Wood, WallType.WoodWall,
            RoofMaterialType.Thatch, doorSide: 2, doorOffset: 3); // door south

        // Smithy — far east, north of road (zone 2, stone)
        BuildHouse(map, 48, 24, 7, 5, 2, TerrainId.Stone, WallType.StoneWall,
            RoofMaterialType.Slate, doorSide: 2, doorOffset: 3);

        // House 3 — south of road, east side (zone 3, wood)
        BuildHouse(map, 46, 42, 6, 5, 3, TerrainId.Wood, WallType.WoodWall,
            RoofMaterialType.WoodShingle, doorSide: 0, doorOffset: 3); // door north

        // Tavern — large, southwest (zone 4, brick)
        BuildHouse(map, 32, 46, 10, 7, 4, TerrainId.Wood, WallType.BrickWall,
            RoofMaterialType.ClayTile, doorSide: 0, doorOffset: 5); // door north

        // Small house near river (zone 5)
        BuildHouse(map, 60, 40, 5, 4, 5, TerrainId.Wood, WallType.WoodWall,
            RoofMaterialType.Thatch, doorSide: 3, doorOffset: 2); // door west

        // === VILLAGE SQUARE (stone paved area around the crossroads) ===
        for (int x = 38; x <= 43; x++)
            for (int y = 33; y <= 38; y++)
                SetFloor(map, x, y, TerrainId.Stone);

        // Pond — circular, west of village square
        int pondCX = 28, pondCY = 36;
        int pondR = 3;
        for (int px = pondCX - pondR - 1; px <= pondCX + pondR + 1; px++)
        {
            for (int py = pondCY - pondR - 1; py <= pondCY + pondR + 1; py++)
            {
                if (!map.IsInBounds(px, py)) continue;
                float dx = px - pondCX, dy = py - pondCY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                int h = TileMap.ComputeVariantSeed(px, py);
                float noise = ((h & 0xFF) / 255f - 0.5f) * 1.0f;
                if (dist < pondR + noise)
                    SetFloor(map, px, py, TerrainId.Water);
                else if (dist < pondR + 1.2f + noise)
                    SetFloor(map, px, py, TerrainId.Sand);
            }
        }

        // === BORDER ===
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

        map.ComputeWaterDepth();
        return map;
    }

    /// <summary>
    /// Builds a rectangular house with walls, floor, zone, and a door.
    /// doorSide: 0=north, 1=east, 2=south, 3=west. doorOffset: tiles from left/top of that wall.
    /// </summary>
    private static void BuildHouse(TileMap map, int x, int y, int w, int h,
        ushort zoneId, TerrainId floorTerrain, WallType wallType, RoofMaterialType roofMaterial,
        int doorSide, int doorOffset)
    {
        var bounds = new Rectangle(x, y, w, h);
        map.RegisterZone(new ZoneDefinition
        {
            Id = zoneId, HasRoof = true,
            RoofMaterial = roofMaterial, Bounds = bounds
        });

        // Walls around the zone (1 tile outside bounds)
        for (int wx = x - 1; wx <= x + w; wx++)
        {
            SetWall(map, wx, y - 1, floorTerrain, wallType);
            SetWall(map, wx, y + h, floorTerrain, wallType);
        }
        for (int wy = y - 1; wy <= y + h; wy++)
        {
            SetWall(map, x - 1, wy, floorTerrain, wallType);
            SetWall(map, x + w, wy, floorTerrain, wallType);
        }

        // Floor
        for (int fx = x; fx < x + w; fx++)
            for (int fy = y; fy < y + h; fy++)
                SetFloor(map, fx, fy, floorTerrain, zoneId);

        // Door
        int doorX, doorY;
        switch (doorSide)
        {
            case 0: doorX = x + doorOffset; doorY = y - 1; break;     // north
            case 1: doorX = x + w; doorY = y + doorOffset; break;      // east
            case 2: doorX = x + doorOffset; doorY = y + h; break;      // south
            case 3: doorX = x - 1; doorY = y + doorOffset; break;      // west
            default: return;
        }
        SetFloor(map, doorX, doorY, floorTerrain, zoneId);

        // Windows on south wall (skip door tile and corners)
        int southWallY = y + h;
        for (int wx = x; wx < x + w; wx++)
        {
            if (wx == doorX && southWallY == doorY) continue; // skip door
            // Place windows every 2-3 tiles
            if ((wx - x) % 3 == 1 && wx > x && wx < x + w - 1)
                SetWindow(map, wx, southWallY);
        }
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

    private static void SetFloor(TileMap map, int x, int y, TerrainId terrain, ushort zoneId = 0)
    {
        if (!map.IsInBounds(x, y)) return;
        map.SetTile(x, y, new TileData(terrain, WallType.None,
            TileMap.ComputeVariantSeed(x, y), zoneId));
    }

    private static void SetWall(TileMap map, int x, int y, TerrainId terrain, WallType wall)
    {
        if (!map.IsInBounds(x, y)) return;
        map.SetTile(x, y, new TileData(terrain, wall, TileMap.ComputeVariantSeed(x, y)));
    }

    private static void SetWindow(TileMap map, int x, int y)
    {
        if (!map.IsInBounds(x, y)) return;
        var tile = map.GetTile(x, y);
        tile.HasWindow = true;
        map.SetTile(x, y, tile);
    }
}

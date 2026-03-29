using System.Collections.Generic;
using RoguelikeEngine.Data;

namespace RoguelikeEngine.World;

public class TileMap
{
    private readonly TileData[,] _tiles;
    private readonly List<TileEffect>[,] _effects;
    private readonly bool[,] _objectBlocksMovement;
    private readonly bool[,] _objectBlocksLight;
    private readonly Dictionary<ushort, ZoneDefinition> _zones = new();

    public int Width { get; }
    public int Height { get; }

    public TileMap(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new TileData[width, height];
        _effects = new List<TileEffect>[width, height];
        _objectBlocksMovement = new bool[width, height];
        _objectBlocksLight = new bool[width, height];
    }

    public TileData GetTile(int x, int y)
    {
        if (!IsInBounds(x, y))
            return new TileData(TerrainId.Stone, WallType.StoneWall);
        return _tiles[x, y];
    }

    public ref TileData GetTileRef(int x, int y) => ref _tiles[x, y];

    public void SetTile(int x, int y, TileData data)
    {
        if (IsInBounds(x, y))
            _tiles[x, y] = data;
    }

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public bool HasWall(int x, int y)
    {
        return GetTile(x, y).HasWall;
    }

    public bool IsWalkable(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        var tile = _tiles[x, y];
        if (tile.HasWall) return false;
        if (!TerrainRegistry.Get(tile.Terrain).Walkable) return false;
        if (_objectBlocksMovement[x, y]) return false;
        return true;
    }

    public bool BlocksLight(int x, int y)
    {
        if (!IsInBounds(x, y)) return true;
        if (_tiles[x, y].HasWall) return true;
        if (_objectBlocksLight[x, y]) return true;
        return false;
    }

    /// <summary>
    /// Same as BlocksLight, but also treats tiles inside roofed zones
    /// that the viewer is NOT in as opaque. This prevents FOV and light
    /// from leaking through doors of roofed buildings.
    /// </summary>
    public bool BlocksLightForViewer(int x, int y, ushort viewerZoneId)
    {
        if (BlocksLight(x, y)) return true;

        if (!IsInBounds(x, y)) return false;
        ushort tileZone = _tiles[x, y].ZoneId;
        if (tileZone == 0 || tileZone == viewerZoneId) return false;

        var zone = GetZone(tileZone);
        return zone != null && zone.HasRoof;
    }

    // --- Effects ---

    public IReadOnlyList<TileEffect> GetEffects(int x, int y)
    {
        if (!IsInBounds(x, y) || _effects[x, y] == null)
            return System.Array.Empty<TileEffect>();
        return _effects[x, y];
    }

    public float GetEffectIntensity(int x, int y, TerrainEffectType type)
    {
        if (!IsInBounds(x, y) || _effects[x, y] == null) return 0f;
        foreach (var e in _effects[x, y])
            if (e.Type == type) return e.Intensity;
        return 0f;
    }

    public void SetEffect(int x, int y, TerrainEffectType type, float intensity)
    {
        if (!IsInBounds(x, y)) return;

        _effects[x, y] ??= new List<TileEffect>(2);
        var list = _effects[x, y];

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Type == type)
            {
                if (intensity <= 0f)
                    list.RemoveAt(i);
                else
                    list[i] = new TileEffect(type, intensity);
                return;
            }
        }

        if (intensity > 0f)
            list.Add(new TileEffect(type, intensity));
    }

    // --- Object occupancy ---

    public void SetObjectBlocking(int x, int y, bool blocksMovement, bool blocksLight)
    {
        if (!IsInBounds(x, y)) return;
        _objectBlocksMovement[x, y] = blocksMovement;
        _objectBlocksLight[x, y] = blocksLight;
    }

    public void ClearObjectBlocking(int x, int y)
    {
        if (!IsInBounds(x, y)) return;
        _objectBlocksMovement[x, y] = false;
        _objectBlocksLight[x, y] = false;
    }

    // --- Zones ---

    public void RegisterZone(ZoneDefinition zone) => _zones[zone.Id] = zone;

    public ZoneDefinition GetZone(ushort zoneId)
    {
        _zones.TryGetValue(zoneId, out var zone);
        return zone;
    }

    public IEnumerable<ZoneDefinition> GetAllZones() => _zones.Values;

    public ushort GetZoneId(int x, int y)
    {
        if (!IsInBounds(x, y)) return 0;
        return _tiles[x, y].ZoneId;
    }

    // --- Variant seed helper ---

    public static ushort ComputeVariantSeed(int x, int y, int mapSeed = 12345)
    {
        int h = x * 374761393 + y * 668265263 + mapSeed;
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (ushort)(h & 0xFFFF);
    }
}

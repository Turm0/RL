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
        if (_tiles[x, y].HasWall && !_tiles[x, y].HasWindow) return true;
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
        ref var tile = ref _tiles[x, y];
        if (tile.ElevationLayer == 0) return false;
        if (tile.ZoneId == 0 || tile.ZoneId == viewerZoneId) return false;
        return true;
    }

    public bool HasElevatedCover(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        return _tiles[x, y].ElevationLayer > 0;
    }

    /// <summary>
    /// Populates ElevationLayer on tiles based on roofed zones.
    /// Call after all zones are registered and floors are set.
    /// </summary>
    public void PopulateElevation()
    {
        // Pass 1: mark all zone interior tiles
        foreach (var zone in _zones.Values)
        {
            if (!zone.HasRoof) continue;
            for (int x = zone.Bounds.X; x < zone.Bounds.Right; x++)
                for (int y = zone.Bounds.Y; y < zone.Bounds.Bottom; y++)
                {
                    if (!IsInBounds(x, y)) continue;
                    if (_tiles[x, y].ZoneId == zone.Id)
                        _tiles[x, y].ElevationLayer = 1;
                }
        }

        // Pass 2: mark walls adjacent to any elevated tile (any shape building)
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (!_tiles[x, y].HasWall) continue;
                if (_tiles[x, y].ElevationLayer > 0) continue;

                // Check 8 neighbors for any elevated tile
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (IsInBounds(nx, ny) && _tiles[nx, ny].ElevationLayer > 0)
                        {
                            _tiles[x, y].ElevationLayer = 1;
                            goto nextWall;
                        }
                    }
                }
                nextWall:;
            }
        }
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

    // --- Water depth computation ---

    /// <summary>
    /// Computes WaterDepth for all water tiles based on distance to nearest non-water tile.
    /// Call after all tiles are placed.
    /// </summary>
    public void ComputeWaterDepth(float maxDepthDistance = 4f)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (!TerrainTextureGenerator_IsWater(_tiles[x, y].Terrain))
                {
                    _tiles[x, y].WaterDepth = 0f;
                    continue;
                }

                // Find minimum distance to any non-water tile
                float minDist = maxDepthDistance;
                int searchRadius = (int)maxDepthDistance + 1;

                for (int dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    for (int dy = -searchRadius; dy <= searchRadius; dy++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (!IsInBounds(nx, ny))
                        {
                            // Map edge counts as shore
                            float edgeDist = System.MathF.Sqrt(dx * dx + dy * dy);
                            if (edgeDist < minDist) minDist = edgeDist;
                            continue;
                        }

                        if (!TerrainTextureGenerator_IsWater(_tiles[nx, ny].Terrain))
                        {
                            float dist = System.MathF.Sqrt(dx * dx + dy * dy);
                            if (dist < minDist) minDist = dist;
                        }
                    }
                }

                _tiles[x, y].WaterDepth = System.Math.Clamp(minDist / maxDepthDistance, 0f, 1f);
            }
        }
    }

    private static bool TerrainTextureGenerator_IsWater(TerrainId terrain) =>
        terrain == TerrainId.Water || terrain == TerrainId.DeepWater;

    /// <summary>
    /// Gets the interpolated water depth at a sub-tile position (for per-pixel rendering).
    /// </summary>
    public float GetInterpolatedWaterDepth(int tileX, int tileY, float localX, float localY)
    {
        float centerDepth = GetWaterDepth(tileX, tileY);

        // Bilinear interpolation with neighbors for smooth gradients
        float nx = localX / 32f - 0.5f; // -0.5 to 0.5
        float ny = localY / 32f - 0.5f;

        int dx = nx >= 0 ? 1 : -1;
        int dy = ny >= 0 ? 1 : -1;
        float fx = System.Math.Abs(nx);
        float fy = System.Math.Abs(ny);

        float d00 = centerDepth;
        float d10 = GetWaterDepth(tileX + dx, tileY);
        float d01 = GetWaterDepth(tileX, tileY + dy);
        float d11 = GetWaterDepth(tileX + dx, tileY + dy);

        float top = d00 * (1f - fx) + d10 * fx;
        float bot = d01 * (1f - fx) + d11 * fx;
        return top * (1f - fy) + bot * fy;
    }

    private float GetWaterDepth(int x, int y)
    {
        if (!IsInBounds(x, y)) return 0f;
        return _tiles[x, y].WaterDepth;
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

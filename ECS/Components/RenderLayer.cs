namespace RoguelikeEngine.ECS.Components;

public struct RenderLayer
{
    public byte Layer; // 0 = terrain object, 1 = item, 2 = creature

    public const byte TerrainObjectLayer = 0;
    public const byte ItemLayer = 1;
    public const byte CreatureLayer = 2;

    public RenderLayer(byte layer) => Layer = layer;
}

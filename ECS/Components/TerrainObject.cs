namespace RoguelikeEngine.ECS.Components;

public struct TerrainObject
{
    public string ObjectType;
    public bool BlocksMovement;
    public bool BlocksLight;
    public byte FootprintWidth;
    public byte FootprintHeight;
    public bool Destructible;
    public int MaxHitPoints;
    public int CurrentHitPoints;

    public TerrainObject(string objectType, bool blocksMovement = true, bool blocksLight = false)
    {
        ObjectType = objectType;
        BlocksMovement = blocksMovement;
        BlocksLight = blocksLight;
        FootprintWidth = 1;
        FootprintHeight = 1;
        Destructible = false;
        MaxHitPoints = 0;
        CurrentHitPoints = 0;
    }
}

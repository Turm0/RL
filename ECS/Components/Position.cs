namespace RoguelikeEngine.ECS.Components;

/// <summary>
/// Tile-based position component.
/// </summary>
public struct Position
{
    public int TileX;
    public int TileY;

    public Position(int tileX, int tileY)
    {
        TileX = tileX;
        TileY = tileY;
    }
}

namespace RoguelikeEngine.ECS.Components;

public struct GroundItem
{
    public string ItemType;
    public int Quantity;

    public GroundItem(string itemType, int quantity = 1)
    {
        ItemType = itemType;
        Quantity = quantity;
    }
}

namespace RoguelikeEngine.ECS.Components;

public enum MoveAnimType : byte
{
    None,
    Slide,
    Hop,
    Bob
}

public struct MovementAnimation
{
    public int FromX, FromY;
    public float Progress; // 0 = at FromX/FromY, 1 = at current Position
    public float Speed;    // how fast to animate (tiles per second)
    public MoveAnimType Type;
    public bool Moving;

    public MovementAnimation(float speed, MoveAnimType type)
    {
        FromX = FromY = 0;
        Progress = 1f;
        Speed = speed;
        Type = type;
        Moving = false;
    }

    public void StartMove(int fromX, int fromY)
    {
        FromX = fromX;
        FromY = fromY;
        Progress = 0f;
        Moving = true;
    }
}

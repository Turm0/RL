using DefaultEcs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.Rendering;
using RoguelikeEngine.World;

namespace RoguelikeEngine.ECS.Systems;

/// <summary>
/// Dungeonmans-style movement: game state updates immediately on input,
/// animation is purely visual and never blocks input.
/// </summary>
public class PlayerInputSystem
{
    private readonly EntitySet _players;
    private readonly TileMap _map;
    private readonly Camera _camera;
    private KeyboardState _previousKeyboard;

    private const float InitialDelay = 0.15f;
    private const float RepeatRate = 0.085f;
    private float _holdTimer;
    private bool _holding;

    public PlayerInputSystem(DefaultEcs.World world, TileMap map, Camera camera)
    {
        _players = world.GetEntities()
            .With<PlayerControlled>()
            .With<Position>()
            .AsSet();
        _map = map;
        _camera = camera;
    }

    public void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Prioritize freshly pressed directions over held ones
        int dx = 0, dy = 0;
        bool freshUp = IsNewPress(kb, Keys.Up) || IsNewPress(kb, Keys.W);
        bool freshDown = IsNewPress(kb, Keys.Down) || IsNewPress(kb, Keys.S);
        bool freshLeft = IsNewPress(kb, Keys.Left) || IsNewPress(kb, Keys.A);
        bool freshRight = IsNewPress(kb, Keys.Right) || IsNewPress(kb, Keys.D);

        if (freshUp) dy = -1;
        else if (freshDown) dy = 1;
        else if (freshLeft) dx = -1;
        else if (freshRight) dx = 1;
        else if (kb.IsKeyDown(Keys.Up) || kb.IsKeyDown(Keys.W)) dy = -1;
        else if (kb.IsKeyDown(Keys.Down) || kb.IsKeyDown(Keys.S)) dy = 1;
        else if (kb.IsKeyDown(Keys.Left) || kb.IsKeyDown(Keys.A)) dx = -1;
        else if (kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D)) dx = 1;

        bool wantsMove = dx != 0 || dy != 0;
        bool freshPress = freshUp || freshDown || freshLeft || freshRight;

        if (freshPress)
        {
            ExecuteMove(dx, dy);
            _holdTimer = 0f;
            _holding = true;
        }
        else if (wantsMove && _holding)
        {
            _holdTimer += dt;
            float threshold = _holdTimer < InitialDelay + RepeatRate ? InitialDelay : RepeatRate;
            if (_holdTimer >= threshold)
            {
                ExecuteMove(dx, dy);
                _holdTimer -= threshold;
            }
        }
        else if (!wantsMove)
        {
            _holdTimer = 0f;
            _holding = false;
        }

        _previousKeyboard = kb;
    }

    private void ExecuteMove(int dx, int dy)
    {
        foreach (ref readonly var entity in _players.GetEntities())
        {
            ref var pos = ref entity.Get<Position>();
            int newX = pos.TileX + dx;
            int newY = pos.TileY + dy;

            if (_map.IsWalkable(newX, newY))
            {
                int oldX = pos.TileX, oldY = pos.TileY;
                pos.TileX = newX;
                pos.TileY = newY;
                _camera.TargetTile = new Point(newX, newY);

                if (entity.Has<MovementAnimation>())
                {
                    ref var anim = ref entity.Get<MovementAnimation>();
                    // Snap any in-progress animation to completion
                    anim.Progress = 1f;
                    anim.Moving = false;
                    // Start new lerp
                    anim.StartMove(oldX, oldY);
                }
            }
        }
    }

    private bool IsNewPress(KeyboardState current, Keys key)
        => current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}

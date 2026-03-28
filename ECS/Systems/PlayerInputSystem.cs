using DefaultEcs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RoguelikeEngine.ECS.Components;
using RoguelikeEngine.Rendering;
using RoguelikeEngine.World;

namespace RoguelikeEngine.ECS.Systems;

/// <summary>
/// Reads keyboard input and moves the player entity one tile per keypress.
/// </summary>
public class PlayerInputSystem
{
    private readonly EntitySet _players;
    private readonly TileMap _map;
    private readonly Camera _camera;
    private KeyboardState _previousKeyboard;

    public PlayerInputSystem(DefaultEcs.World world, TileMap map, Camera camera)
    {
        _players = world.GetEntities()
            .With<PlayerControlled>()
            .With<Position>()
            .AsSet();
        _map = map;
        _camera = camera;
    }

    /// <summary>
    /// Processes input each frame. Moves player on fresh key press only.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        var currentKeyboard = Keyboard.GetState();

        int dx = 0, dy = 0;

        if (IsNewPress(currentKeyboard, Keys.Up) || IsNewPress(currentKeyboard, Keys.W))
            dy = -1;
        else if (IsNewPress(currentKeyboard, Keys.Down) || IsNewPress(currentKeyboard, Keys.S))
            dy = 1;
        else if (IsNewPress(currentKeyboard, Keys.Left) || IsNewPress(currentKeyboard, Keys.A))
            dx = -1;
        else if (IsNewPress(currentKeyboard, Keys.Right) || IsNewPress(currentKeyboard, Keys.D))
            dx = 1;

        if (dx != 0 || dy != 0)
        {
            foreach (ref readonly var entity in _players.GetEntities())
            {
                ref var pos = ref entity.Get<Position>();
                int newX = pos.TileX + dx;
                int newY = pos.TileY + dy;

                if (_map.IsWalkable(newX, newY))
                {
                    pos.TileX = newX;
                    pos.TileY = newY;
                    _camera.TargetTile = new Point(newX, newY);
                }
            }
        }

        _previousKeyboard = currentKeyboard;
    }

    private bool IsNewPress(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using BodyGenerator.Pipeline;

namespace BodyGenerator.TestHarness;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private SpriteGenerator _generator;

    private readonly string[] _poseNames = { "idle", "walk", "attack", "guard" };
    private readonly string[] _creatureFiles =
    {
        "creatures/human_ranger.yaml",
        "creatures/human_mage.yaml",
        "creatures/human_knight.yaml",
        "creatures/human_thief.yaml",
        "creatures/orc_warrior.yaml",
        "creatures/undead.yaml",
        "creatures/human_cleric.yaml",
        "creatures/dark_elf.yaml",
        "creatures/dwarf_smith.yaml",
    };

    // creatureFile -> (poseName -> Texture2D)
    private Dictionary<string, Dictionary<string, Texture2D>> _allTextures = new();
    private int _currentPose;
    private bool _outlineEnabled = true;
    private bool _gridEnabled;

    private KeyboardState _prevKeyState;
    private Texture2D _pixel;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1100;
        _graphics.PreferredBackBufferHeight = 720;
        IsMouseVisible = true;
        Window.Title = "Pixel Sprite Test - idle";
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        string contentRoot = Path.Combine(AppContext.BaseDirectory, "Content", "Sprites");
        _generator = new SpriteGenerator(contentRoot);

        RegenerateTextures();
    }

    private void RegenerateTextures()
    {
        foreach (var creature in _allTextures.Values)
            foreach (var tex in creature.Values)
                tex?.Dispose();
        _allTextures.Clear();

        foreach (string creatureFile in _creatureFiles)
        {
            var poseTextures = new Dictionary<string, Texture2D>();
            foreach (string pose in _poseNames)
            {
                poseTextures[pose] = _generator.Generate(
                    GraphicsDevice, creatureFile, pose, _outlineEnabled);
            }
            _allTextures[creatureFile] = poseTextures;
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var keyState = Keyboard.GetState();

        if (WasPressed(Keys.Escape, keyState)) Exit();

        bool needRegen = false;

        if (WasPressed(Keys.D1, keyState)) _currentPose = 0;
        if (WasPressed(Keys.D2, keyState)) _currentPose = 1;
        if (WasPressed(Keys.D3, keyState)) _currentPose = 2;
        if (WasPressed(Keys.D4, keyState)) _currentPose = 3;

        if (WasPressed(Keys.O, keyState))
        {
            _outlineEnabled = !_outlineEnabled;
            needRegen = true;
        }

        if (WasPressed(Keys.G, keyState))
            _gridEnabled = !_gridEnabled;

        if (needRegen)
            RegenerateTextures();

        Window.Title = $"Pixel Sprite Test - {_poseNames[_currentPose]}" +
                       (_outlineEnabled ? "" : " [no outline]") +
                       (_gridEnabled ? " [grid]" : "");

        _prevKeyState = keyState;
        base.Update(gameTime);
    }

    private bool WasPressed(Keys key, KeyboardState current)
    {
        return current.IsKeyDown(key) && _prevKeyState.IsKeyUp(key);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(26, 26, 42));
        int screenW = _graphics.PreferredBackBufferWidth;

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        string poseName = _poseNames[_currentPose];

        // Draw all creatures in a grid, 5 per row
        int cols = 5;
        int scale = 6;
        int spriteSize = 32 * scale;
        int gap = 12;
        int rows = (_creatureFiles.Length + cols - 1) / cols;
        int gridW = cols * spriteSize + (cols - 1) * gap;
        int gridStartX = (screenW - gridW) / 2;
        int gridStartY = 20;

        for (int i = 0; i < _creatureFiles.Length; i++)
        {
            int row = i / cols;
            int col = i % cols;
            int x = gridStartX + col * (spriteSize + gap);
            int y = gridStartY + row * (spriteSize + gap + 20);

            var tex = _allTextures[_creatureFiles[i]][poseName];
            _spriteBatch.Draw(tex, new Rectangle(x, y, spriteSize, spriteSize), Color.White);

            if (_gridEnabled)
                DrawGrid(x, y, spriteSize, 32, scale);

            // Draw creature name below
            // Extract name from filename
            string name = Path.GetFileNameWithoutExtension(_creatureFiles[i]).Replace('_', ' ');
            // Simple label using the pixel texture as underline
            int labelY = y + spriteSize + 2;
            int labelW = name.Length * 6;
            _spriteBatch.Draw(_pixel, new Rectangle(x + (spriteSize - labelW) / 2, labelY, labelW, 1),
                new Color(180, 180, 200, 80));
        }

        // Draw 4 poses of first creature (ranger) at bottom for pose reference
        int bottomY = gridStartY + rows * (spriteSize + gap + 20) + 10;
        int smallScale = 4;
        int smallSize = 32 * smallScale;
        int smallGap = 8;
        int totalSmallW = 4 * smallSize + 3 * smallGap;
        int smallStartX = (screenW - totalSmallW) / 2;

        for (int i = 0; i < _poseNames.Length; i++)
        {
            int x = smallStartX + i * (smallSize + smallGap);
            var tex = _allTextures[_creatureFiles[0]][_poseNames[i]];

            if (i == _currentPose)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x - 2, bottomY - 2, smallSize + 4, smallSize + 4),
                    new Color(255, 255, 255, 40));
            }

            _spriteBatch.Draw(tex, new Rectangle(x, bottomY, smallSize, smallSize), Color.White);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void DrawGrid(int originX, int originY, int totalSize, int gridCells, int scale)
    {
        var gridColor = new Color(255, 255, 255, 30);
        for (int i = 0; i <= gridCells; i++)
        {
            int offset = i * scale;
            _spriteBatch.Draw(_pixel, new Rectangle(originX + offset, originY, 1, totalSize), gridColor);
            _spriteBatch.Draw(_pixel, new Rectangle(originX, originY + offset, totalSize, 1), gridColor);
        }
    }

    protected override void UnloadContent()
    {
        foreach (var creature in _allTextures.Values)
            foreach (var tex in creature.Values)
                tex?.Dispose();
        _pixel?.Dispose();
    }
}

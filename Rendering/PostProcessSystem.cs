using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoguelikeEngine.Core;
using RoguelikeEngine.World;

namespace RoguelikeEngine.Rendering;

/// <summary>
/// Post-process pipeline: renders the scene to a RenderTarget, then applies
/// shader effects (fog, wetness, frost) as a full-screen pass.
/// </summary>
public class PostProcessSystem
{
    private GraphicsDevice _device;
    private SpriteBatch _spriteBatch;
    private Effect _postProcessEffect;

    // Scene render target — all rendering goes here first
    private RenderTarget2D _sceneRT;

    // Fog data texture — encodes per-pixel visibility info for the shader
    // R = visibility (1=visible, 0=not), G = distance factor (0=close, 1=far), B = elevation
    private Texture2D _fogDataTexture;
    private Color[] _fogDataPixels;
    private int _fogDataWidth, _fogDataHeight;

    private int _rtWidth, _rtHeight;
    private float _visibleRectScreenX, _visibleRectScreenY;
    private float _visibleRectScreenW, _visibleRectScreenH;

    // Shader parameters
    public float FogIntensity { get; set; } = 0.0f;
    public float DesatIntensity { get; set; } = 0.0f;
    public float WetAmount { get; set; } = 0.0f;
    public float FrostAmount { get; set; } = 0.0f;

    public bool Enabled => _postProcessEffect != null;

    public void Initialize(GraphicsDevice device, Effect effect)
    {
        _device = device;
        _spriteBatch = new SpriteBatch(device);
        _postProcessEffect = effect;
    }

    private void EnsureRT(int width, int height)
    {
        if (_sceneRT != null && _rtWidth == width && _rtHeight == height)
            return;
        _sceneRT?.Dispose();
        _rtWidth = width;
        _rtHeight = height;
        _sceneRT = new RenderTarget2D(_device, width, height, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    }

    /// <summary>
    /// Call before rendering the scene. Redirects all drawing to the offscreen RT.
    /// </summary>
    public void BeginScene()
    {
        int w = _device.Viewport.Width;
        int h = _device.Viewport.Height;
        EnsureRT(w, h);
        _device.SetRenderTarget(_sceneRT);
        _device.Clear(Color.Black);
    }

    /// <summary>
    /// Call after all scene rendering. Applies the post-process shader and draws to screen.
    /// </summary>
    public void EndScene(float time, Camera camera, FogOfWar fow, TileMap map, Vector2 playerScreenPos, Vector3 ambientColor)
    {
        _device.SetRenderTarget(null);

        // Build fog data texture
        BuildFogData(camera, fow, map);

        // Set shader parameters
        _postProcessEffect.Parameters["Time"]?.SetValue(time);
        _postProcessEffect.Parameters["FogIntensity"]?.SetValue(FogIntensity);
        _postProcessEffect.Parameters["DesatIntensity"]?.SetValue(DesatIntensity);
        _postProcessEffect.Parameters["WetAmount"]?.SetValue(WetAmount);
        _postProcessEffect.Parameters["FrostAmount"]?.SetValue(FrostAmount);
        _postProcessEffect.Parameters["ViewportSize"]?.SetValue(new Vector2(_rtWidth, _rtHeight));
        _postProcessEffect.Parameters["PlayerScreenPos"]?.SetValue(playerScreenPos);
        _postProcessEffect.Parameters["FogDataTexture"]?.SetValue(_fogDataTexture);

        // Pass the screen-space rect that maps to the data texture
        _postProcessEffect.Parameters["DataRectOrigin"]?.SetValue(new Vector2(_visibleRectScreenX, _visibleRectScreenY));
        _postProcessEffect.Parameters["DataRectSize"]?.SetValue(new Vector2(_visibleRectScreenW, _visibleRectScreenH));
        _postProcessEffect.Parameters["CameraPos"]?.SetValue(camera.Position);
        _postProcessEffect.Parameters["AmbientColor"]?.SetValue(ambientColor);

        // Draw the scene through the shader
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
            SamplerState.PointClamp, null, null, _postProcessEffect);
        _spriteBatch.Draw(_sceneRT, Vector2.Zero, Color.White);
        _spriteBatch.End();
    }

    /// <summary>
    /// Builds a screen-sized texture encoding FOV data for the shader.
    /// Downsampled to tile resolution for performance.
    /// </summary>
    private void BuildFogData(Camera camera, FogOfWar fow, TileMap map)
    {
        int tileSize = GameConfig.TileSize;
        var visibleRect = camera.GetVisibleTileRect(tileSize);
        int tilesW = visibleRect.Width;
        int tilesH = visibleRect.Height;

        if (_fogDataTexture == null || _fogDataWidth != tilesW || _fogDataHeight != tilesH)
        {
            _fogDataTexture?.Dispose();
            _fogDataWidth = tilesW;
            _fogDataHeight = tilesH;
            _fogDataTexture = new Texture2D(_device, tilesW, tilesH);
            _fogDataPixels = new Color[tilesW * tilesH];
        }

        for (int ty = 0; ty < tilesH; ty++)
        {
            for (int tx = 0; tx < tilesW; tx++)
            {
                int mapX = visibleRect.X + tx;
                int mapY = visibleRect.Y + ty;

                float visibility = 0f;
                float distFactor = 1f;
                float elevation = 0f;

                if (map.IsInBounds(mapX, mapY))
                {
                    visibility = fow.IsVisible(mapX, mapY) ? 1f : (fow.IsExplored(mapX, mapY) ? 0.5f : 0f);
                    distFactor = 1f - fow.GetVisibilityFactor(mapX, mapY);
                    // Under roof = elevated cover (zone interior + walls)
                    elevation = map.HasElevatedCover(mapX, mapY) ? 1f : 0f;
                }

                _fogDataPixels[ty * tilesW + tx] = new Color(
                    (byte)(visibility * 255),
                    (byte)(distFactor * 255),
                    (byte)(elevation * 255));
            }
        }

        _fogDataTexture.SetData(_fogDataPixels);

        // Store the screen-space origin of the visible rect for the shader
        _visibleRectScreenX = (visibleRect.X * tileSize - camera.Position.X) / _rtWidth;
        _visibleRectScreenY = (visibleRect.Y * tileSize - camera.Position.Y) / _rtHeight;
        _visibleRectScreenW = (visibleRect.Width * tileSize) / (float)_rtWidth;
        _visibleRectScreenH = (visibleRect.Height * tileSize) / (float)_rtHeight;
    }
}

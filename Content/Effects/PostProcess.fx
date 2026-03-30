#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Scene texture (rendered terrain + entities + lighting)
sampler2D SceneTexture : register(s0);

// Auxiliary data textures
texture2D FogDataTexture;
sampler2D FogDataSampler = sampler_state
{
    Texture = <FogDataTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
};

// Parameters
float Time;
float FogIntensity;       // 0 = no fog, 1 = full fog
float DesatIntensity;     // 0 = full color, 1 = full grayscale
float WetAmount;          // 0 = dry, 1 = fully wet
float FrostAmount;        // 0 = no frost, 1 = full frost
float2 ViewportSize;      // screen dimensions in pixels
float2 PlayerScreenPos;   // player position in screen UV (0-1)
float2 DataRectOrigin;    // screen UV origin of the data texture coverage
float2 DataRectSize;      // screen UV size of the data texture coverage
float2 CameraPos;         // camera position in world pixels
float3 AmbientColor;      // sky/ambient light color for wet reflections

// Fog data texture layout:
// R = visibility (1 = visible, 0 = not visible)
// G = distance from player (0 = at player, 1 = at FOV edge)
// B = elevation (1 = has cover, 0 = open)

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 scene = tex2D(SceneTexture, input.TexCoord);

    // Map screen UV to data texture UV
    float2 dataUV = (input.TexCoord - DataRectOrigin) / DataRectSize;
    float4 fogData = tex2D(FogDataSampler, dataUV);

    float visibility = fogData.r;
    float distFactor = fogData.g; // 0 = close, 1 = far
    float elevation = fogData.b;

    float3 color = scene.rgb;

    // === FOG: distance-based desaturation + slight darkening ===
    if (FogIntensity > 0 && visibility > 0.01)
    {
        float fogAmount = distFactor * FogIntensity;

        // Desaturate
        float gray = dot(color, float3(0.299, 0.587, 0.114));
        color = lerp(color, float3(gray, gray, gray), fogAmount * 0.6);

        // Slight cool tint at distance
        float3 fogColor = float3(gray * 0.85, gray * 0.9, gray * 0.95);
        color = lerp(color, fogColor, fogAmount * 0.3);
    }

    // === WETNESS (placeholder — visual effect TBD) ===

    // === FROST: ice-blue tint + crystalline edge pattern ===
    if (FrostAmount > 0 && visibility > 0.01 && elevation < 0.5)
    {
        float frost = FrostAmount;

        // Cool blue tint
        float3 iceColor = float3(0.7, 0.85, 1.0);
        color = lerp(color, color * iceColor, frost * 0.4);

        // Brighten slightly (frost is reflective)
        color += frost * 0.05;

        // Crystalline noise pattern (world-anchored)
        float2 worldUV = (input.TexCoord * ViewportSize + CameraPos) / 32.0;
        float crystal = frac(sin(dot(floor(worldUV * 4.0), float2(127.1, 311.7))) * 43758.5453);
        float edge = frac(sin(dot(floor(worldUV * 8.0), float2(269.5, 183.3))) * 43758.5453);

        if (crystal > 0.7)
            color = lerp(color, float3(0.85, 0.92, 1.0), frost * 0.25 * edge);
    }

    color = saturate(color);

    return float4(color, scene.a) * input.Color;
}

technique PostProcess
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

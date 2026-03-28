\# Rendering Engine — Phase Implementation Docs



\---



\## Phase 1: Project Setup, Camera, and Basic Terrain



\### Goal

A MonoGame window showing a hardcoded dungeon with colored rectangles for floors and walls. Camera follows a movable player position. Arrow keys move the player one tile per press. Viewport culling — only tiles on screen get drawn.



\### Project Setup



Create a MonoGame DesktopGL project. Install packages:

\- DefaultEcs (ECS framework)

\- SixLabors.ImageSharp (vector rasterization, used later but install now)

\- FastNoiseLite (noise generation, used later but install now)



Project namespace structure:

```

RoguelikeEngine/

├── Core/

│   ├── Game1.cs                 (MonoGame entry, owns RenderPipeline)

│   └── GameConfig.cs            (constants: TileSize, screen dimensions)

├── ECS/

│   ├── Components/              (ECS component structs)

│   │   ├── Position.cs          (int TileX, TileY)

│   │   ├── Renderable.cs        (Color, char/placeholder for now)

│   │   └── PlayerControlled.cs  (tag component, empty struct)

│   └── Systems/

│       └── PlayerInputSystem.cs (reads keyboard, moves player entity)

├── Rendering/

│   ├── RenderPipeline.cs        (orchestrates draw calls)

│   ├── Camera.cs                (position, viewport, WorldToScreen conversion)

│   └── TerrainRenderer.cs       (draws tile grid)

├── World/

│   └── TileMap.cs               (2D array of TileType enum, hardcoded test dungeon)

└── Data/

&#x20;   └── TileType.cs              (enum: Floor, Wall, Water, Lava)

```



\### Camera



```csharp

Camera

├── Position (Vector2, world pixel space — top-left of viewport)

├── TargetTile (Point, the tile to follow — player position)

├── ViewportWidth, ViewportHeight (int, in pixels, from window size)

├── TileSize (int, from config)

│

├── Methods:

│   ├── Update(GameTime) — lerp Position toward TargetTile × TileSize

│   ├── WorldToScreen(Vector2 worldPos) → Vector2 screenPos

│   ├── ScreenToWorld(Vector2 screenPos) → Vector2 worldPos

│   ├── GetVisibleTileRect() → Rectangle (min/max tile coords visible)

│   └── IsInView(int tileX, int tileY) → bool

```



Camera smoothing: `Position = Vector2.Lerp(Position, target, 8f \* deltaTime)`. This gives a smooth follow without lag.



\### TileMap



```csharp

TileMap

├── Width, Height (int)

├── Tiles (TileType\[,])

│

├── Methods:

│   ├── GetTile(int x, int y) → TileType

│   ├── SetTile(int x, int y, TileType)

│   ├── IsInBounds(int x, int y) → bool

│   └── IsWalkable(int x, int y) → bool

```



Hardcode a test dungeon: 40×30 grid. Border walls. Two rooms connected by a corridor. A few internal wall pillars for visual interest.



\### TerrainRenderer



Iterates over `Camera.GetVisibleTileRect()`. For each tile, draws a filled rectangle using SpriteBatch and a 1×1 white pixel texture (standard MonoGame trick for drawing colored rects).



Colors (hardcoded for now, will come from config later):

\- Floor: dark brown-gray (RGB 55, 50, 44)

\- Wall: medium gray-brown (RGB 75, 70, 62)

\- Wall with floor below: draw a 4px lighter strip at the bottom edge (wall top highlight)

\- Water: dark blue (RGB 30, 50, 70)



\### RenderPipeline



```csharp

RenderPipeline

├── Camera

├── TerrainRenderer

├── SpriteBatch

├── WhitePixel (Texture2D, 1×1 white — created in Initialize)

│

├── Initialize(GraphicsDevice)

├── Update(GameTime) — updates camera

└── Draw(GameTime) — calls terrain draw, then entity draw (placeholder)

```



\### Player Input



Arrow keys check once per press (not held — use IsKeyDown with previous state tracking or a simple cooldown). Move the player entity's Position component by one tile. Camera.TargetTile updates to match. Prevent walking into walls (check TileMap.IsWalkable).



\### Acceptance Criteria

\- Window opens showing a dungeon with two rooms and a corridor

\- Floor and wall tiles are visually distinct colored rectangles

\- Arrow keys move a colored square (the player) one tile per press

\- Camera smoothly follows the player

\- Walking into walls is blocked

\- Only visible tiles are drawn (verify by checking draw call count or adding a tile counter)



\---



\## Phase 2: Basic Entity Rendering with Simple Vector Shapes



\### Goal

Entities drawn as simple procedural shapes instead of colored rectangles. Player is a green hooded figure. Add a few test creatures (goblin, rat, skeleton) as hardcoded vector shapes. Entities y-sorted so overlapping looks correct.



\### Entity Rendering



Add new components:

```csharp

SpriteShape — component

├── ShapeType (enum: Circle, Ellipse, Triangle, Rect, Composite)

├── PrimaryColor (Color)

├── SecondaryColor (Color)

├── EyeColor (Color)

├── Size (float, multiplier: 0.5 = small, 1.0 = normal, 1.5 = large)

└── CreatureType (string: "player", "goblin", "rat", "skeleton")

```



For phase 2, creature rendering is hardcoded switch/case on CreatureType. Each case draws directly to a RenderTarget2D using the VectorRasterizer. In phase 4 we replace this with data-driven VectorDefinitions.



\### VectorRasterizer (v1 — minimal)



A utility class that draws basic shapes to a Color\[] array, which gets uploaded to Texture2D.



Required operations:

\- FillEllipse(centerX, centerY, radiusX, radiusY, color)

\- FillRect(x, y, width, height, color)

\- FillTriangle(p1, p2, p3, color)

\- DrawLine(p1, p2, color, thickness)

\- FillCircle(centerX, centerY, radius, color)



Use SixLabors.ImageSharp for the actual drawing:

1\. Create an ImageSharp Image<Rgba32> at the desired size

2\. Use ImageSharp's drawing API to render shapes

3\. Copy pixel data to a Texture2D



\### TextureCache



```csharp

TextureCache

├── Dictionary<string, Texture2D> cache

├── GetOrCreate(string key, Func<Texture2D> factory) → Texture2D

├── Invalidate(string key)

└── Clear()

```



Cache key for creatures: `"{creatureType}\_{size}"`. Textures only regenerated when definition changes.



\### EntityRenderer



```csharp

EntityRenderer

├── TextureCache

├── VectorRasterizer

├── SpriteBatch

│

├── Draw(entities, camera)

│   1. Collect all visible entities (position in camera viewport)

│   2. Sort by TileY (ascending) for correct overlap

│   3. For each entity:

│      a. Get or create cached texture from SpriteShape

│      b. Calculate screen position from Camera.WorldToScreen

│      c. Draw with SpriteBatch, centered on tile

```



\### Y-Sorting



Entities on higher Y tiles (further down screen) are drawn later, so they overlap entities above them. This gives a basic depth illusion. Compare by TileY first, then by TileX for ties.



\### Test Setup



Spawn 5-6 test entities in the hardcoded dungeon:

\- Player (green, hooded figure shape)

\- 2 goblins (small, green, pointy ears)

\- 2 rats (tiny, brown, tail)

\- 1 skeleton (white, visible ribs)



For now, creatures are static — they just stand in place. AI and movement come in game systems phases (not rendering).



\### Acceptance Criteria

\- Player is drawn as a multi-shape vector figure (not a colored rect)

\- Test creatures are visible in the dungeon as distinct vector shapes

\- Each creature type is visually distinguishable by shape, size, and color

\- Entities correctly overlap based on Y position

\- Textures are cached (no per-frame rasterization — verify via logging or debug counter)

\- Camera and movement still work from Phase 1



\---



\## Phase 3: Dynamic Lighting



\### Goal

Full dynamic lighting with multiple colored light sources, shadowcasting for line-of-sight, and smooth sub-tile interpolation. The dungeon should be dark except where lit. Torches flicker. The player carries a small light.



\### Lighting System



```csharp

LightingSystem

├── LightRT (RenderTarget2D, size = visibleTiles × SubTileRes)

├── SubTileRes (int = 4, meaning 4 samples per tile per axis)

├── LightBuffer (float\[], size = lightRT\_width × lightRT\_height × 3 for RGB)

├── AmbientColor (Vector3, default very dim: 0.06, 0.06, 0.08)

│

├── Methods:

│   ├── BeginFrame() — clear LightBuffer to ambient

│   ├── AddLight(tileX, tileY, radius, intensity, color, flickerPhase)

│   ├── ComputeShadows(tileMap) — for each light, run shadowcast, accumulate

│   ├── BuildTexture(graphicsDevice) — upload LightBuffer to LightRT

│   └── Draw(spriteBatch, camera) — draw LightRT over scene with multiply blend

```



\### Shadowcasting Algorithm



Use recursive shadowcasting (Björn Bergström's approach, widely used in roguelikes). For each light source:



1\. From the light's tile position, cast shadows in 8 octants

2\. For each octant, scan rows outward from the source

3\. Track which angular ranges are blocked by walls

4\. Mark visible cells, compute distance-based falloff

5\. Add light contribution (color × intensity × falloff²) to LightBuffer



The algorithm operates at tile resolution (not sub-tile). After shadowcasting determines which tiles a light reaches and at what intensity, the intensity is written to all SubTileRes×SubTileRes sub-cells of each reached tile. The smooth look comes from the bilinear upscale, not from sub-tile shadowcasting.



\### Light Accumulation



LightBuffer stores RGB floats. For each visible cell of each light:

```

falloff = 1.0 - (distance / radius)

falloff = falloff \* falloff  // quadratic falloff, looks natural

intensity = baseIntensity \* falloff \* flickerMultiplier



buffer\[cell].R += intensity \* lightColor.R

buffer\[cell].G += intensity \* lightColor.G

buffer\[cell].B += intensity \* lightColor.B

```



Multiple lights accumulate additively. Clamp final values to \[0, 2] range (allow slight overexposure for bright areas).



\### Flicker



Each torch/fire light source has a flicker phase derived from its position hash:

```

phase1 = hash(tileX, tileY, 1)

phase2 = hash(tileX, tileY, 2)

flicker = 1.0 - flickerIntensity \* sin(time \* 4.0 + phase1) \* sin(time \* 7.0 + phase2) \* 0.5

```



This gives each torch a unique flicker pattern that never syncs with others.



\### Compositing



After building LightRT:

1\. Draw terrain and entities to the backbuffer as normal

2\. Set SpriteBatch blend state to Multiply: `new BlendState { ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero }`

3\. Draw LightRT stretched to cover the visible area

4\. The dark areas (low light values) darken the scene, lit areas pass through



Set `SamplerState.LinearClamp` when drawing LightRT for smooth bilinear interpolation between sub-tile samples.



\### Light Sources for Testing



Place torches in the test dungeon (hardcoded positions). Each torch is a LightSource:

```

TorchLight: radius=7, intensity=0.9, color=(1.3, 1.0, 0.65), flicker=true

PlayerLight: radius=4, intensity=0.45, color=(1.1, 1.15, 1.0), flicker=false

```



Add an ECS component:

```csharp

LightEmitter — component

├── Radius (float)

├── Intensity (float)

├── Color (Vector3, RGB multipliers)

├── Flicker (bool)

├── FlickerIntensity (float, 0-1)

└── IsActive (bool)

```



Torch entities are placed in the world with Position + LightEmitter components.

The player entity gets a LightEmitter for their carried light.



\### Performance Note



For a typical visible area of \~30×22 tiles with 6-10 light sources, the shadowcasting + buffer fill is cheap — well under 1ms on modern hardware. No need to optimize yet. If it becomes an issue later, the obvious optimization is to only recompute shadowcasts when light sources or walls change, and only update flicker multipliers each frame.



\### Acceptance Criteria

\- Dungeon is dark by default with very dim ambient

\- Torch positions emit warm flickering light that illuminates nearby tiles

\- Light is blocked by walls (shadowcasting works — light doesn't leak through corners)

\- Player carries a small cool-white light

\- Multiple lights blend additively (overlapping areas are brighter)

\- Lighting transitions smoothly between tiles (no blocky per-tile look)

\- Moving the player moves their light and the scene re-lights correctly

\- Torches flicker with unique patterns (not synchronized)



\---



\## Phase 4: Data-Driven Vector Definitions



\### Goal

Replace hardcoded creature drawing with a data-driven JSON format. Any creature, item, or equipment piece is defined in a JSON file. The engine loads these at startup, rasterizes them to cached textures, and draws them. Adding a new creature means adding a JSON file, zero code changes.



\### VectorDefinition JSON Schema



```json

{

&#x20; "name": "goblin",

&#x20; "size": 0.7,

&#x20; "sizeCategory": "small",

&#x20; "anchor": { "x": 0, "y": 0 },

&#x20; "parts": \[

&#x20;   {

&#x20;     "name": "body",

&#x20;     "shape": "ellipse",

&#x20;     "params": { "radiusX": 6, "radiusY": 8 },

&#x20;     "fill": "#7CB342",

&#x20;     "offset": { "x": 0, "y": 2 },

&#x20;     "zLayer": 0,

&#x20;     "tags": \["body"]

&#x20;   },

&#x20;   {

&#x20;     "name": "head",

&#x20;     "shape": "ellipse",

&#x20;     "params": { "radiusX": 5, "radiusY": 5 },

&#x20;     "fill": "#7CB342",

&#x20;     "offset": { "x": 0, "y": -7 },

&#x20;     "zLayer": 1,

&#x20;     "tags": \["head"]

&#x20;   },

&#x20;   {

&#x20;     "name": "left\_ear",

&#x20;     "shape": "triangle",

&#x20;     "params": {

&#x20;       "points": \[

&#x20;         { "x": -5, "y": -8 },

&#x20;         { "x": -8, "y": -15 },

&#x20;         { "x": -2, "y": -9 }

&#x20;       ]

&#x20;     },

&#x20;     "fill": "#558B2F",

&#x20;     "zLayer": 2,

&#x20;     "tags": \["ear"]

&#x20;   },

&#x20;   {

&#x20;     "name": "right\_ear",

&#x20;     "shape": "triangle",

&#x20;     "params": {

&#x20;       "points": \[

&#x20;         { "x": 5, "y": -8 },

&#x20;         { "x": 8, "y": -15 },

&#x20;         { "x": 2, "y": -9 }

&#x20;       ]

&#x20;     },

&#x20;     "fill": "#558B2F",

&#x20;     "zLayer": 2,

&#x20;     "tags": \["ear"]

&#x20;   },

&#x20;   {

&#x20;     "name": "left\_eye",

&#x20;     "shape": "circle",

&#x20;     "params": { "radius": 1.2 },

&#x20;     "fill": "#FFEB3B",

&#x20;     "offset": { "x": -2, "y": -8 },

&#x20;     "zLayer": 3,

&#x20;     "tags": \["eye"]

&#x20;   },

&#x20;   {

&#x20;     "name": "right\_eye",

&#x20;     "shape": "circle",

&#x20;     "params": { "radius": 1.2 },

&#x20;     "fill": "#FFEB3B",

&#x20;     "offset": { "x": 2, "y": -8 },

&#x20;     "zLayer": 3,

&#x20;     "tags": \["eye"]

&#x20;   },

&#x20;   {

&#x20;     "name": "left\_arm",

&#x20;     "shape": "line",

&#x20;     "params": {

&#x20;       "from": { "x": -5, "y": 0 },

&#x20;       "to": { "x": -9, "y": 5 }

&#x20;     },

&#x20;     "stroke": "#7CB342",

&#x20;     "strokeWidth": 2,

&#x20;     "zLayer": 1,

&#x20;     "tags": \["arm"]

&#x20;   },

&#x20;   {

&#x20;     "name": "right\_arm",

&#x20;     "shape": "line",

&#x20;     "params": {

&#x20;       "from": { "x": 5, "y": 0 },

&#x20;       "to": { "x": 9, "y": 5 }

&#x20;     },

&#x20;     "stroke": "#7CB342",

&#x20;     "strokeWidth": 2,

&#x20;     "zLayer": 1,

&#x20;     "tags": \["arm"]

&#x20;   }

&#x20; ],

&#x20; "animations": {

&#x20;   "idle": \[

&#x20;     {

&#x20;       "partTag": "body",

&#x20;       "property": "offsetY",

&#x20;       "waveform": "sin",

&#x20;       "speed": 2.0,

&#x20;       "amplitude": 1.5

&#x20;     }

&#x20;   ]

&#x20; },

&#x20; "lightEmission": null,

&#x20; "particleEmitters": \[]

}

```



\### Supported Shapes



\- \*\*circle\*\*: `{ radius }` — filled circle

\- \*\*ellipse\*\*: `{ radiusX, radiusY }` — filled ellipse

\- \*\*rect\*\*: `{ width, height }` — filled rectangle

\- \*\*triangle\*\*: `{ points: \[p1, p2, p3] }` — filled triangle from 3 vertices

\- \*\*line\*\*: `{ from, to }` — stroked line segment

\- \*\*arc\*\*: `{ radius, startAngle, endAngle, fill/stroke }` — partial circle/ring

\- \*\*bezier\*\*: `{ points: \[start, control1, control2, end] }` — cubic bezier curve (stroked)

\- \*\*polygon\*\*: `{ points: \[...] }` — filled arbitrary polygon



All coordinates are relative to the entity center. The rasterizer scales everything by `size` and the current TileSize.



\### VectorDefinitionLoader



```csharp

VectorDefinitionLoader

├── Definitions (Dictionary<string, VectorDefinition>)

│

├── LoadAll(string directoryPath)

│   └── Scan for \*.json, deserialize each, store by name

├── Get(string name) → VectorDefinition

└── Reload(string name) — hot-reload a single definition (dev convenience)

```



\### Updated VectorRasterizer



Extend from Phase 2 to handle all shape types. Input is a VectorDefinition, output is a Texture2D.



```csharp

Texture2D Rasterize(VectorDefinition def, int tileSize, GraphicsDevice device)

{

&#x20;   int texSize = (int)(tileSize \* def.Size \* 2.5); // enough room for overflow

&#x20;   using var image = new Image<Rgba32>(texSize, texSize);

&#x20;   

&#x20;   var center = new PointF(texSize / 2f, texSize / 2f);

&#x20;   float scale = tileSize / 24f; // normalize to base tile size

&#x20;   

&#x20;   foreach (var part in def.Parts.OrderBy(p => p.ZLayer))

&#x20;   {

&#x20;       // Transform part offset by scale, apply to center

&#x20;       // Draw shape using ImageSharp drawing API

&#x20;       // Handle fill and/or stroke

&#x20;   }

&#x20;   

&#x20;   // Convert Image<Rgba32> pixel data to Texture2D

&#x20;   return texture;

}

```



\### Animation Frames



For idle animations, pre-rasterize N frames (default 8) with the animated properties evaluated at evenly spaced phase values (0, π/4, π/2, ..., 7π/4). Cache all frames. At draw time, select frame based on `(time \* speed) % frameCount`.



\### Updated EntityRenderer



Replace the hardcoded switch/case from Phase 2:

1\. Read entity's `SpriteShape.CreatureType` (or new `VectorDefinitionRef` component)

2\. Look up VectorDefinition by name

3\. Get cached texture (or rasterize and cache)

4\. Draw at correct screen position, scaled



\### New Component



```csharp

VectorDefinitionRef — component (replaces SpriteShape)

├── DefinitionName (string: "goblin", "dragon", etc.)

├── ColorOverrides (Dictionary<string, Color>, optional — recolor parts by tag)

├── CurrentAnimation (string: "idle", "move", "attack")

└── AnimationTime (float, accumulated)

```



\### Starter Creature Definitions to Create



Create JSON files for at least these creatures:

\- player (green hooded figure with sword)

\- goblin (small, green, pointy ears)

\- rat (tiny, brown, long tail)

\- skeleton (white bones, red eye glow)

\- dragon (large, red, wings, fire particles)

\- ghost (translucent, wavy bottom edge)

\- fire\_elemental (flame-shaped, layered orange/yellow)

\- spider (8 legs radiating from body)

\- slime (simple blob, semi-transparent)

\- bat (small, wings, dark)



\### Acceptance Criteria

\- All creatures render from JSON definitions, not hardcoded drawing

\- Adding a new JSON file to the creatures folder and restarting shows the new creature

\- Creatures have idle animations (bobbing, swaying) driven by the animation data

\- ColorOverrides work (spawn a "red goblin" by overriding the body color)

\- TextureCache correctly caches and only re-rasterizes when needed

\- Visual quality matches or exceeds the hardcoded Phase 2 rendering

\- Performance: texture rasterization happens at load time, not per frame



\---



\## Phase 5: Idle Animations and Turn Animations



\### Goal

Entities animate smoothly: continuous idle animations (bobbing, wing flaps, tail sways) and turn-triggered animations (movement slides, attack lunges, damage flashes). The game remains turn-based but looks fluid.



\### Idle Animation System



Idle animations run continuously on real-time deltaTime, independent of game turns. They're defined per VectorDefinition and pre-baked into texture frames (from Phase 4).



The EntityRenderer cycles through cached frames:

```

frameIndex = (int)((animationTime \* speed) % frameCount)

```



`animationTime` accumulates every Update() call: `animationTime += deltaTime`.



\### Turn Animation Queue



When a game turn resolves, it produces AnimationEvents. These queue up and play sequentially (or in parallel groups where appropriate).



```csharp

AnimationEvent (abstract base)

├── Duration (float, seconds)

├── ElapsedTime (float)

├── IsComplete → ElapsedTime >= Duration

├── Update(float deltaTime)

└── Apply(entity) — modifies entity render state (not logical state)



MoveAnimation : AnimationEvent

├── Entity (entity reference)

├── FromPixel (Vector2, source tile center in pixels)

├── ToPixel (Vector2, destination tile center in pixels)

├── Duration = 0.15s

└── Apply → entity.RenderOffset = Lerp(FromPixel, ToPixel, progress) - logicalPosition



AttackAnimation : AnimationEvent

├── Attacker (entity)

├── TargetDirection (Vector2, normalized)

├── Duration = 0.2s

├── LungeDistance = TileSize \* 0.4

└── Apply → lunge forward then back (out-and-return ease curve)



DamageAnimation : AnimationEvent

├── Target (entity)

├── Duration = 0.25s

└── Apply → flash entity white for first 0.05s, slight knockback in hit direction



DeathAnimation : AnimationEvent

├── Entity (entity)

├── Duration = 0.4s

└── Apply → fade opacity to 0, slight downward drift



ProjectileAnimation : AnimationEvent

├── FromPixel, ToPixel (Vector2)

├── ProjectileVisual (small shape/color)

├── Duration = 0.1–0.3s (based on distance)

└── Apply → draw projectile at lerped position



CameraShakeAnimation : AnimationEvent

├── Intensity (float)

├── Duration = 0.2s

└── Apply → set camera.ShakeOffset to decaying random displacement

```



\### Animation Queue Manager



```csharp

TurnAnimationManager

├── Queue (List<AnimationGroup>)

├── CurrentGroup (AnimationGroup — events playing in parallel)

├── IsAnimating → CurrentGroup != null || Queue.Count > 0

│

├── Enqueue(AnimationEvent) — add to current group

├── EnqueueBarrier() — start a new group (next events wait for current group to finish)

├── Update(deltaTime)

│   ├── If CurrentGroup is null, pop next group from queue

│   ├── Update all events in CurrentGroup

│   ├── Remove completed events

│   └── If CurrentGroup is empty, set to null (triggers next group)

└── SkipAll() — instantly complete everything (for impatient players)

```



Example turn sequence:

```

1\. Player attacks goblin

&#x20;  → Enqueue: AttackAnimation(player, toward goblin)  \[group 1]

&#x20;  → EnqueueBarrier()

&#x20;  → Enqueue: DamageAnimation(goblin)                 \[group 2]

&#x20;  → Enqueue: CameraShakeAnimation(small)             \[group 2, parallel with damage]

&#x20;  → EnqueueBarrier()

&#x20;  → Enqueue: DeathAnimation(goblin)                  \[group 3, if goblin dies]

```



\### Input Blocking



While `TurnAnimationManager.IsAnimating` is true, player input is ignored (except a "skip" key — Space or Enter — which calls SkipAll). This prevents the player from queuing up moves while animations play.



\### RenderOffset Component



```csharp

RenderOffset — component

├── Offset (Vector2, pixels — added to entity's screen position during draw)

├── TintOverride (Color?, if set, multiplies entity texture color)

├── OpacityOverride (float?, if set, overrides draw opacity)

└── Reset() — clear all overrides back to defaults

```



The EntityRenderer checks for RenderOffset and applies it during drawing. Animations write to this component, then Reset() when complete.



\### Acceptance Criteria

\- Creatures bob/sway continuously in idle state

\- Moving the player shows a smooth slide between tiles (\~0.15s)

\- If a creature is adjacent, pressing toward it plays attack animation (lunge + return)

\- Damage shows a white flash on the target

\- Camera shakes on impacts

\- Animations play in correct sequence (attack → damage → death)

\- Player input is blocked during animations

\- Space/Enter skips all pending animations instantly

\- Idle animations continue playing during turn animations



\---



\## Phase 6: Particle System



\### Goal

A general-purpose particle system supporting torch sparks, blood splatter, fire, magic effects, and environmental particles. Data-driven emitter definitions loaded from JSON. Particles interact with lighting (bright particles are visible, dark area particles are dimmed).



\### Particle Struct



```csharp

struct Particle  // struct for cache-friendly storage in arrays

{

&#x20;   public Vector2 Position;        // world pixel space

&#x20;   public Vector2 Velocity;

&#x20;   public Vector2 Acceleration;    // gravity + wind

&#x20;   public Color StartColor;

&#x20;   public Color EndColor;

&#x20;   public float StartSize;

&#x20;   public float EndSize;

&#x20;   public float Life;              // 0 to 1, increases by 1/MaxLife per second

&#x20;   public float MaxLife;           // seconds

&#x20;   public float Rotation;

&#x20;   public float AngularVelocity;

&#x20;   public BlendMode Blend;         // Alpha or Additive

&#x20;   public bool Alive;

}

```



\### Particle Pool



Pre-allocate a fixed array (default 4096 particles). Dead particles (Alive=false) are recycled by scanning for the next dead slot. No heap allocation during gameplay.



```csharp

ParticlePool

├── Particles (Particle\[], fixed size)

├── MaxParticles (int)

├── AliveCount (int, tracked for stats)

│

├── Emit(ref Particle template) → int (index, or -1 if pool full)

├── Update(float deltaTime, Vector2 wind)

│   └── For each alive particle: age, move, apply acceleration, kill if Life >= 1

└── Draw(SpriteBatch, Camera, LightingSystem)

&#x20;   └── For each alive particle:

&#x20;       ├── Compute current color = Lerp(StartColor, EndColor, Life)

&#x20;       ├── Compute current size = Lerp(StartSize, EndSize, Life)

&#x20;       ├── If not Additive blend: dim by light value at particle's tile position

&#x20;       └── Draw as a small filled rect or circle (1×1 white pixel scaled)

```



\### Emitter Definition (JSON)



```json

{

&#x20; "name": "torch\_spark",

&#x20; "emitRate": 3.0,

&#x20; "emitBurst": 0,

&#x20; "particle": {

&#x20;   "speedMin": 10, "speedMax": 30,

&#x20;   "angleMin": -1.8, "angleMax": -1.3,

&#x20;   "lifeMin": 0.3, "lifeMax": 0.8,

&#x20;   "startSizeMin": 1.0, "startSizeMax": 2.0,

&#x20;   "endSize": 0.5,

&#x20;   "startColor": "#FFC847",

&#x20;   "endColor": "#FF4500",

&#x20;   "gravityY": -15,

&#x20;   "blend": "additive"

&#x20; },

&#x20; "windAffected": true,

&#x20; "spawnRadius": 3.0

}

```



\### Emitter Types to Create



\- \*\*torch\_spark\*\*: low rate, upward, orange→red, small, additive, short life

\- \*\*fire\*\*: medium rate, upward, yellow→orange→transparent, additive

\- \*\*blood\_splat\*\*: burst only (8-15 particles), radial, red→darkred, alpha, gravity, short life

\- \*\*magic\_sparkle\*\*: medium rate, radial outward, cyan/purple, additive, no gravity

\- \*\*dust\_puff\*\*: burst (5-8), radial slow, brown, alpha, fades quickly

\- \*\*steam\*\*: low rate, upward slow drift, white→transparent, alpha, long life, large size

\- \*\*ember\*\*: low rate, upward with horizontal wobble, orange, additive, medium life

\- \*\*poison\_drip\*\*: very low rate, downward, green, alpha, gravity



\### Emitter Component



```csharp

ParticleEmitterComponent — ECS component

├── EmitterName (string → lookup definition)

├── Offset (Vector2, relative to entity center)

├── Active (bool)

├── Condition (enum: Always, OnFire, InWater, WhenMoving, OnDamage, etc.)

└── AccumulatedTime (float, for rate-based emission)

```



\### Integration Points



\- Torch entities: Position + LightEmitter + ParticleEmitterComponent("torch\_spark")

\- Dragon: ParticleEmitterComponent("ember") at head position

\- Fire elemental: ParticleEmitterComponent("fire") at center

\- Blood on hit: TurnAnimationManager spawns a burst emitter at damage location

\- Environmental steam: placed at map generation where water meets lava



\### Acceptance Criteria

\- Torches emit small rising sparks that fade from orange to red

\- Particles respect lighting (alpha-blended particles in dark areas are dimmed)

\- Additive particles (sparks, fire) glow even in dim areas

\- Blood bursts appear when entities take damage (placeholder combat trigger)

\- Particle definitions are loaded from JSON

\- Performance: 1000+ simultaneous particles at 60fps

\- No heap allocations during particle update/emit cycle

\- Wind vector affects wind-sensitive particles (provide a global wind value for testing)



\---



\## Phase 7: Weather System



\### Goal

Visual weather effects that overlay the scene: rain, snow, fog, and storms. Weather modifies ambient lighting and interacts with the particle system. Weather transitions smoothly between states.



\### Weather State



```csharp

WeatherState

├── Current (enum: Clear, Cloudy, LightRain, HeavyRain, Storm, Snow, Fog)

├── Intensity (float, 0–1)

├── WindDirection (Vector2, normalized)

├── WindStrength (float, 0–1)

├── TransitionProgress (float, 0–1, for blending between states)

├── TargetState (enum, what we're transitioning to)

│

├── Update(deltaTime) — advance transition

├── SetWeather(targetState, transitionDuration)

└── GetAmbientModifier() → Vector3 (RGB multiplier applied to lighting ambient)

```



\### Rain Renderer



Uses the particle system with a screen-space rain emitter:

\- Spawn rain particles across the top of the viewport

\- Diagonal fall angle based on wind direction

\- Light rain: rate=100/s, moderate opacity

\- Heavy rain: rate=300/s, higher opacity, splashes on ground

\- Ground splashes: tiny burst emitters at random floor tiles when rain hits



Ambient modifier: darken by 20-40% depending on intensity.



\### Snow Renderer



Particle-based:

\- Slow downward fall with horizontal drift (wind-affected)

\- White particles, alpha blend, longer life than rain

\- Larger than rain particles

\- Gentle swaying motion (sinusoidal horizontal velocity)



Ambient modifier: slight brightening (white reflection).



\### Fog Renderer



Not particle-based — use overlaid semi-transparent ellipses that drift slowly:

\- 8-15 large fog patches (ellipses, 3-6 tiles wide, low opacity 0.1-0.2)

\- Slow drift in wind direction

\- Fog patches wrap around viewport edges

\- Drawn after lighting pass, before UI



Ambient modifier: no change to ambient, but reduce light source effective radius by 30-50%.



\### Storm



Combines heavy rain with:

\- Periodic lightning: random interval (4-12 seconds), brief full-screen light flash

&#x20; - Add a massive temporary light source to LightingSystem (radius=100, duration=0.1s, white)

&#x20; - Or simpler: draw a white semi-transparent overlay for 2 frames

\- Camera shake on thunder (delayed 0.5-2s after lightning flash)



\### Weather Transition



When `SetWeather(newState, duration)` is called:

\- Begin interpolating Intensity from current to target

\- Particle emission rates lerp accordingly

\- Ambient modifiers lerp

\- After transition completes, Current = TargetState



\### Testing Controls



Add debug key bindings for testing:

\- F1: cycle through weather states

\- F2: increase wind

\- F3: decrease wind



\### Acceptance Criteria

\- Rain falls diagonally based on wind direction

\- Heavy rain shows splash particles on ground tiles

\- Snow drifts slowly with wind influence

\- Fog patches drift across the viewport and partially obscure the scene

\- Storm lightning briefly illuminates the entire visible area

\- Weather transitions smoothly (rain doesn't snap on/off)

\- Ambient lighting changes with weather (darker in rain, slightly brighter in snow)

\- Fog reduces effective light radius

\- Debug keys allow cycling through all weather states

\- Performance: weather at full intensity with all other systems running maintains 60fps



\---



\## Phase 8: Equipment Overlays



\### Goal

Humanoid entities (player, humanoid NPCs/monsters) visually display equipped items. Weapons, shields, helmets, and armor are drawn as additional vector layers on top of the base creature.



\### Equipment Vector Definitions



Equipment uses the same JSON format as creatures but simpler — typically 1-3 parts:



```json

{

&#x20; "name": "iron\_sword",

&#x20; "equipSlot": "weapon",

&#x20; "parts": \[

&#x20;   {

&#x20;     "name": "blade",

&#x20;     "shape": "line",

&#x20;     "params": { "from": {"x":0,"y":0}, "to": {"x":4,"y":-10} },

&#x20;     "stroke": "#B0BEC5",

&#x20;     "strokeWidth": 2,

&#x20;     "zLayer": 10,

&#x20;     "tags": \["blade"]

&#x20;   },

&#x20;   {

&#x20;     "name": "guard",

&#x20;     "shape": "rect",

&#x20;     "params": { "width": 6, "height": 2 },

&#x20;     "fill": "#FFD54F",

&#x20;     "offset": { "x": 0, "y": 0 },

&#x20;     "zLayer": 10,

&#x20;     "tags": \["guard"]

&#x20;   }

&#x20; ],

&#x20; "attachPoint": "weapon\_hand"

}

```



\### Equipment Slots



```csharp

EquipmentSlots — ECS component

├── Weapon (string, equipment definition name or null)

├── Shield (string)

├── Helmet (string)

├── Armor (string)

└── Cloak (string)

```



\### Attachment Points



Each humanoid VectorDefinition has parts tagged with attachment names:

\- `"weapon\_hand"` — right arm end point

\- `"off\_hand"` — left arm end point

\- `"head"` — top of head

\- `"body"` — center of torso

\- `"back"` — behind body (for cloaks)



The equipment renderer:

1\. Finds the attachment point part in the base creature definition

2\. Uses that part's offset as the equipment's origin

3\. Draws the equipment parts at that position



\### Rendering Integration



When rasterizing an entity's texture (TextureCache), if the entity has EquipmentSlots, composite the equipment parts onto the base creature texture:



1\. Rasterize base creature definition

2\. For each equipped slot: look up equipment definition, find attachment offset, draw equipment parts at that offset

3\. Cache the composited result



Cache key becomes: `"{creatureType}\_{weapon}\_{shield}\_{helmet}\_{armor}\_{cloak}"`. When equipment changes, invalidate and re-rasterize.



\### Armor Color Tinting



Armor can tint or replace the body part color:

```json

{

&#x20; "name": "leather\_armor",

&#x20; "equipSlot": "armor",

&#x20; "bodyTint": "#8D6E63",

&#x20; "parts": \[

&#x20;   {

&#x20;     "name": "shoulder\_left",

&#x20;     "shape": "ellipse",

&#x20;     "params": { "radiusX": 3, "radiusY": 2 },

&#x20;     "fill": "#6D4C41",

&#x20;     "offset": { "x": -6, "y": -2 },

&#x20;     "zLayer": 5,

&#x20;     "tags": \["armor\_piece"]

&#x20;   }

&#x20; ],

&#x20; "attachPoint": "body"

}

```



When `bodyTint` is present, all parts tagged `"body"` in the base creature have their fill color lerped toward the tint color by 60%.



\### Starter Equipment Definitions



\- iron\_sword (line blade + rect guard)

\- wooden\_shield (ellipse with cross pattern)

\- leather\_helmet (arc on head)

\- leather\_armor (shoulder pads + body tint)

\- steel\_sword (similar to iron, different color/thicker)

\- wooden\_staff (tall line)

\- cloak (large triangle/arc behind body)



\### Acceptance Criteria

\- Player can have visible equipment (weapon appears at hand position)

\- Equipping/changing items updates the visual immediately

\- Multiple equipment slots render simultaneously (sword + shield + helmet)

\- Armor tints the body color

\- Equipment definitions load from JSON

\- Texture cache correctly invalidates on equipment change

\- Equipment doesn't clip badly with base creature shapes



\---



\## Phase 9: Terrain Decals and Polish



\### Goal

Visual polish on terrain: blood stains persist where combat happened, scorch marks from fire, water puddles from rain, moss on old walls, cracks on damaged tiles. Plus floor variation and edge blending for a richer look.



\### Decal System



```csharp

Decal

├── Position (Point, tile coordinates)

├── Type (enum: Blood, Scorch, Puddle, Crack, Moss, Ice)

├── Opacity (float, 0–1, can fade over time)

├── Rotation (float, random per instance for variety)

├── Size (float, 0.5–1.5, slight variation)

├── Age (float, seconds since placed)

└── FadeRate (float, opacity decrease per second — 0 for permanent)

```



```csharp

DecalManager

├── Decals (List<Decal>)

├── DecalTextures (Dictionary<DecalType, Texture2D>)

│

├── AddDecal(type, position, opacity, fadeRate)

├── Update(deltaTime) — age decals, fade, remove fully transparent ones

└── Draw(spriteBatch, camera) — draw all visible decals on terrain layer

```



\### Decal Textures



Generated procedurally at startup (like creature textures):

\- \*\*Blood\*\*: irregular splat shape (randomized polygon), dark red

\- \*\*Scorch\*\*: dark circle with rough edges, black-brown

\- \*\*Puddle\*\*: smooth ellipse, blue-gray, semi-transparent

\- \*\*Crack\*\*: thin branching lines, dark

\- \*\*Moss\*\*: cluster of small green circles

\- \*\*Ice\*\*: pale blue semi-transparent overlay, crystalline edge



\### Decal Triggers



\- Blood: placed at combat damage location after DamageAnimation

\- Scorch: placed where fire/explosion/lava damage occurs

\- Puddle: appear on floor tiles during rain, fade after rain stops

\- Crack: placed at map generation or when walls are destroyed

\- Moss: placed at map generation on walls near water

\- Ice: placed when freeze effects occur



\### Floor Variation



Applied during terrain rendering (TerrainRenderer enhancement):

\- Per-tile color jitter: hash(x, y, seed) → slight brightness ±5% and hue shift ±2°

\- Scattered small detail sprites: tiny dots, scratches, pebbles — 1-in-7 chance per tile

&#x20; - Generated as tiny procedural textures (2-4px shapes)

\- Edge darkening: floor tiles adjacent to walls get a subtle shadow along the wall edge (2px darker strip)



\### Wall Polish



\- Auto-tiling: walls check their 4 or 8 neighbors to select the correct tile variant

&#x20; - For colored rects: vary shade based on neighbor configuration (corners slightly darker, straight walls uniform)

&#x20; - Wall top highlights already implemented in Phase 1 — verify they still look good with the new lighting

\- Moss overlay on walls near water tiles (within 2 tiles of water)



\### Acceptance Criteria

\- Blood splatters appear at combat locations and persist

\- Decals fade over time (blood fades slowly, puddles fade after rain)

\- Floor tiles have visible variation (no two adjacent tiles look identical)

\- Walls adjacent to floors have subtle shadow/depth effect

\- Scorch marks appear from fire damage

\- Rain produces small puddles on floor tiles

\- Decal textures are procedurally generated (no external art files needed)

\- Performance: 200+ decals on screen without frame drop



\---



\## Phase 10: UI Layer Foundation



\### Goal

Basic UI overlay rendered in screen space (not affected by camera). Health bar, a message log, and a minimap. This is the foundation — full UI comes with game systems later.



\### UI Architecture



```csharp

UIRenderer

├── SpriteBatch (separate from world SpriteBatch, drawn last)

├── Font (SpriteFont, loaded from content — a basic monospace font)

├── Components\[]

│   ├── HealthBar

│   ├── MessageLog

│   └── Minimap

│

├── Draw(GameTime) — draw all UI components in screen space

└── HandleInput(input) — for future interactive UI elements

```



All UI draws in screen space — no camera transform. Positioned relative to screen edges.



\### Health Bar (placeholder)



Top-left corner. Simple colored bar:

\- Background: dark rect

\- Fill: red rect proportional to health/maxHealth

\- Text overlay: "HP: 45/100"

\- Reads from player entity's Health component (or placeholder values for now)



\### Message Log



Bottom of screen, spanning most of the width. Shows last 5-8 game messages in a semi-transparent dark panel:

\- "You hit the goblin for 5 damage."

\- "The rat squeaks."

\- Messages fade over time (newest = full opacity, oldest = dim)



```csharp

MessageLog

├── Messages (Queue<LogMessage>, max 50)

├── VisibleCount (int = 6)

│

├── Add(string text, Color color)

├── Draw(spriteBatch, font, screenRect)

```



\### Minimap



Top-right corner. A small (150×150px) representation of the explored dungeon:

\- Each tile = 1-2 pixels

\- Walls = light gray pixel

\- Floor = dark pixel

\- Player = bright green pixel

\- Visible creatures = colored dots (red for hostile)

\- Fog of war: unexplored tiles are black, explored but not visible are dim



```csharp

Minimap

├── MinimapRT (RenderTarget2D, 150×150)

├── ExploredTiles (bool\[,] — tracked as player moves)

│

├── UpdateExplored(visibleTileRect) — mark newly visible tiles as explored

├── Render(map, entities, player, lightingSystem)

└── Draw(spriteBatch, screenPosition)

```



\### Font



MonoGame needs a SpriteFont. Options:

\- Use MonoGame Content Pipeline to compile a .spritefont file (standard approach)

\- Use a bitmap font loaded from a PNG (more portable)

\- Use FontStashSharp NuGet package for runtime TTF rendering (most flexible)



Recommendation: use FontStashSharp for runtime font rendering. Install the NuGet package. Load any TTF font (include a small open-license monospace font in the project, like JetBrains Mono or similar). This avoids the content pipeline for fonts entirely.



\### Acceptance Criteria

\- Health bar shows in top-left, correctly represents health value

\- Message log panel at bottom shows recent messages with color coding

\- Minimap in top-right shows walls, floors, player position

\- Minimap tracks explored tiles (explored but not visible areas shown dim)

\- UI is legible over both lit and dark areas (semi-transparent backgrounds)

\- UI elements don't interfere with each other or gameplay view

\- Font renders clearly at small sizes (12-14px)


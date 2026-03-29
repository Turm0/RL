# Pixel Sprite System — Implementation Spec for BodyGenerator

## Overview

Build a procedural pixel sprite system that assembles humanoid characters from small pixel-art body part templates. Parts connect via named joints. Poses reposition parts by applying offsets to joints. A compositor paints everything onto a pixel buffer with z-ordering, then an outline pass adds a 1px silhouette.

This replaces all existing skeleton/body generation code in the BodyGenerator project.

## Target Output

- Working resolution: **32x32 pixels**
- Display: scaled up with nearest-neighbor (e.g. 8x or 10x)
- Style: chunky pixel art, 1px outlines, 3-tone shading per material

## Architecture

```
YAML files --> Load and Parse --> SpriteDefinition
                                      |
                                 JointResolver (recursive)
                                      |
                                 SpriteCompositor (z-ordered)
                                      |
                                 OutlinePass
                                      |
                                 PixelBuffer --> Texture2D
```

## Core Data Types

```csharp
// How to color a pixel -- resolved against a palette at render time
public enum ColorRole
{
    SkinBase,
    SkinShadow,
    SkinHighlight,
    ClothBase,
    ClothShadow,
    ClothHighlight,
    LegBase,
    LegShadow,
    Belt,
    Boot,
    Eye,
    Outline
}

// A single pixel in a body part template
public struct TemplatePixel
{
    public int X;          // relative to part origin (top-left of bounding box)
    public int Y;
    public ColorRole Role;
}

// A body part: small pixel grid with an anchor and child joint points
public class BodyPartTemplate
{
    public string Id;                          // "torso", "head", "upper_arm_left", etc.
    public int Width;
    public int Height;
    public Point Anchor;                       // where THIS part attaches to its parent
    public Dictionary<string, Point> Joints;   // named points where CHILDREN attach
    public List<TemplatePixel> Pixels;         // the pixel data
}

// All parts for a body type
public class BodyTemplate
{
    public string Name;                        // "humanoid"
    public int SpriteSize;                     // 32
    public Dictionary<string, BodyPartTemplate> Parts;
    public BodyHierarchyNode Hierarchy;        // tree structure
}

// Tree node defining parent-child relationships
public class BodyHierarchyNode
{
    public string PartId;
    public string JointName;                   // which parent joint this attaches to
    public List<BodyHierarchyNode> Children;
}

// Pose: where to place root, draw order, joint offsets
public class PoseDefinition
{
    public string Name;
    public Point RootPosition;                 // where torso sits in the 32x32 grid
    public List<string> DrawOrder;             // back-to-front part names
    public Dictionary<string, Point> JointOffsets; // jointName -> pixel offset
}

// Creature: palette + body template ref + pose ref
public class CreatureDefinition
{
    public string Name;
    public string BodyTemplate;                // "humanoid"
    public string Pose;                        // "idle"
    public Dictionary<ColorRole, Color> Palette;
}

// The pixel buffer we composite onto
public class PixelBuffer
{
    public int Width;
    public int Height;
    public Color[] Pixels;                     // Color.Transparent = empty
    public int[] ZBuffer;                      // z-depth per pixel

    public void SetPixel(int x, int y, Color color, int z)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        int idx = y * Width + x;
        if (ZBuffer[idx] <= z)
        {
            Pixels[idx] = color;
            ZBuffer[idx] = z;
        }
    }

    public void Clear()
    {
        Array.Fill(Pixels, Color.Transparent);
        Array.Fill(ZBuffer, -1);
    }

    public Texture2D ToTexture2D(GraphicsDevice device)
    {
        var tex = new Texture2D(device, Width, Height);
        tex.SetData(Pixels);
        return tex;
    }
}
```

## Body Part Hierarchy

```
torso (root)
+-- head              <-- torso.joint["neck"]
+-- upper_arm_left    <-- torso.joint["shoulder_left"]
|   +-- lower_arm_left   <-- upper_arm_left.joint["elbow_left"]
|       +-- hand_left        <-- lower_arm_left.joint["wrist_left"]
+-- upper_arm_right   <-- torso.joint["shoulder_right"]
|   +-- lower_arm_right  <-- upper_arm_right.joint["elbow_right"]
|       +-- hand_right       <-- lower_arm_right.joint["wrist_right"]
+-- upper_leg_left    <-- torso.joint["hip_left"]
|   +-- lower_leg_left   <-- upper_leg_left.joint["knee_left"]
|       +-- foot_left        <-- lower_leg_left.joint["ankle_left"]
+-- upper_leg_right   <-- torso.joint["hip_right"]
    +-- lower_leg_right  <-- upper_leg_right.joint["knee_right"]
        +-- foot_right       <-- lower_leg_right.joint["ankle_right"]
```

## Pixel Template Character Legend

CRITICAL: case-sensitive mapping. Getting this wrong causes color bugs.

```
.  = transparent (no pixel)
h  = SkinBase
s  = SkinShadow
H  = SkinHighlight
E  = Eye
C  = ClothBase
S  = ClothShadow      (CAPITAL S = cloth, lowercase s = skin!)
G  = ClothHighlight
L  = LegBase
l  = LegShadow        (lowercase L)
B  = Boot
b  = Belt
O  = Outline
```

## YAML Files

### bodies/humanoid.yaml

```yaml
name: humanoid
sprite_size: 32

hierarchy:
  part: torso
  children:
    - part: head
      joint: neck
    - part: upper_arm_left
      joint: shoulder_left
      children:
        - part: lower_arm_left
          joint: elbow_left
          children:
            - part: hand_left
              joint: wrist_left
    - part: upper_arm_right
      joint: shoulder_right
      children:
        - part: lower_arm_right
          joint: elbow_right
          children:
            - part: hand_right
              joint: wrist_right
    - part: upper_leg_left
      joint: hip_left
      children:
        - part: lower_leg_left
          joint: knee_left
          children:
            - part: foot_left
              joint: ankle_left
    - part: upper_leg_right
      joint: hip_right
      children:
        - part: lower_leg_right
          joint: knee_right
          children:
            - part: foot_right
              joint: ankle_right

parts:
  torso:
    width: 8
    height: 7
    anchor: [4, 0]
    joints:
      neck: [4, 0]
      shoulder_left: [0, 1]
      shoulder_right: [7, 1]
      hip_left: [2, 6]
      hip_right: [5, 6]
    pixels: |
      ..CCCC..
      .CSCCSC.
      CCCCCCCC
      CCCCCCCC
      CGHCCGCC
      .CbbbCC.
      ..CCCC..

  head:
    width: 8
    height: 7
    anchor: [4, 6]
    joints: {}
    pixels: |
      .ssssss.
      sHhhhhhs
      shhhhhhs
      shEhhEhs
      shEhhEhs
      .shhhhs.
      ..ssss..

  upper_arm_left:
    width: 3
    height: 4
    anchor: [2, 0]
    joints:
      elbow_left: [1, 3]
    pixels: |
      .CC
      CCC
      CSC
      .Cs

  lower_arm_left:
    width: 3
    height: 4
    anchor: [1, 0]
    joints:
      wrist_left: [1, 3]
    pixels: |
      .Cs
      .hC
      .hh
      .hs

  upper_arm_right:
    width: 3
    height: 4
    anchor: [0, 0]
    joints:
      elbow_right: [1, 3]
    pixels: |
      CC.
      CCC
      CSC
      sC.

  lower_arm_right:
    width: 3
    height: 4
    anchor: [1, 0]
    joints:
      wrist_right: [1, 3]
    pixels: |
      sC.
      Ch.
      hh.
      sh.

  upper_leg_left:
    width: 3
    height: 4
    anchor: [1, 0]
    joints:
      knee_left: [1, 3]
    pixels: |
      .LL
      LLL
      LlL
      .Ll

  lower_leg_left:
    width: 3
    height: 4
    anchor: [1, 0]
    joints:
      ankle_left: [1, 3]
    pixels: |
      .Ll
      .LL
      .BB
      .BB

  upper_leg_right:
    width: 3
    height: 4
    anchor: [1, 0]
    joints:
      knee_right: [1, 3]
    pixels: |
      LL.
      LLL
      LlL
      lL.

  lower_leg_right:
    width: 3
    height: 4
    anchor: [1, 0]
    joints:
      ankle_right: [1, 3]
    pixels: |
      lL.
      LL.
      BB.
      BB.

  hand_left:
    width: 2
    height: 2
    anchor: [1, 0]
    joints: {}
    pixels: |
      hh
      hs

  hand_right:
    width: 2
    height: 2
    anchor: [0, 0]
    joints: {}
    pixels: |
      hh
      sh

  foot_left:
    width: 3
    height: 2
    anchor: [1, 0]
    joints: {}
    pixels: |
      .BB
      BBB

  foot_right:
    width: 3
    height: 2
    anchor: [1, 0]
    joints: {}
    pixels: |
      BB.
      BBB
```

### poses/idle.yaml

```yaml
name: idle
root_position: [12, 6]

draw_order:
  - upper_arm_left
  - lower_arm_left
  - hand_left
  - upper_arm_right
  - lower_arm_right
  - hand_right
  - upper_leg_left
  - lower_leg_left
  - foot_left
  - upper_leg_right
  - lower_leg_right
  - foot_right
  - torso
  - head

joint_offsets: {}
```

### poses/walk.yaml

```yaml
name: walk
root_position: [12, 5]

draw_order:
  - upper_leg_left
  - lower_leg_left
  - foot_left
  - upper_arm_right
  - lower_arm_right
  - hand_right
  - torso
  - head
  - upper_leg_right
  - lower_leg_right
  - foot_right
  - upper_arm_left
  - lower_arm_left
  - hand_left

joint_offsets:
  shoulder_left: [0, -1]
  elbow_left: [1, 0]
  shoulder_right: [0, 0]
  elbow_right: [-1, 0]
  hip_left: [-1, 0]
  hip_right: [1, 0]
  knee_right: [0, -1]
```

### poses/attack.yaml

```yaml
name: attack
root_position: [11, 6]

draw_order:
  - upper_arm_left
  - lower_arm_left
  - hand_left
  - upper_leg_left
  - lower_leg_left
  - foot_left
  - upper_leg_right
  - lower_leg_right
  - foot_right
  - torso
  - head
  - upper_arm_right
  - lower_arm_right
  - hand_right

joint_offsets:
  shoulder_right: [1, -2]
  elbow_right: [2, -1]
  wrist_right: [1, 0]
  hip_right: [1, 0]
  hip_left: [-1, 0]
```

### poses/guard.yaml

```yaml
name: guard
root_position: [12, 6]

draw_order:
  - upper_arm_left
  - lower_arm_left
  - hand_left
  - upper_leg_left
  - lower_leg_left
  - foot_left
  - upper_leg_right
  - lower_leg_right
  - foot_right
  - torso
  - head
  - upper_arm_right
  - lower_arm_right
  - hand_right

joint_offsets:
  shoulder_left: [-1, -1]
  elbow_left: [-1, 0]
  shoulder_right: [1, -1]
  elbow_right: [1, 0]
  hip_left: [-1, 0]
  hip_right: [1, 0]
```

### creatures/human_ranger.yaml

```yaml
name: Human Ranger
body_template: humanoid
pose: idle

palette:
  skin_base: "#E8B878"
  skin_shadow: "#C89458"
  skin_highlight: "#F4D4A0"
  cloth_base: "#2A6A2A"
  cloth_shadow: "#1A4A1A"
  cloth_highlight: "#3A8A3A"
  leg_base: "#6A5A3A"
  leg_shadow: "#4A3A2A"
  belt: "#8B6914"
  boot: "#4A3A2A"
  eye: "#000000"
  outline: "#1A1A2A"
```

## Assembly Algorithm

```
Step 1: Load body template, pose, and creature definition from YAML
Step 2: Build palette lookup: ColorRole -> Color from creature definition

Step 3: Resolve joint positions recursively from root:
  function ResolveJoints(partId, parentWorldJoint, hierarchy):
    part = bodyTemplate.Parts[partId]
    worldX = parentWorldJoint.X - part.Anchor.X
    worldY = parentWorldJoint.Y - part.Anchor.Y
    store position for partId = (worldX, worldY)

    for each child in hierarchy.Children:
      jointLocal = part.Joints[child.JointName]
      jointWorldX = worldX + jointLocal.X
      jointWorldY = worldY + jointLocal.Y

      // Apply pose offset if defined for this joint
      if pose.JointOffsets has child.JointName:
        offset = pose.JointOffsets[child.JointName]
        jointWorldX += offset.X
        jointWorldY += offset.Y

      ResolveJoints(child.PartId, (jointWorldX, jointWorldY), child)

  Call with: ResolveJoints("torso", pose.RootPosition, hierarchy.Root)

Step 4: Composite in draw order:
  for each partName in pose.DrawOrder:
    pos = resolved positions[partName]
    z = index in draw order
    for each pixel in part.Pixels:
      color = palette[pixel.Role]
      buffer.SetPixel(pos.X + pixel.X, pos.Y + pixel.Y, color, z)

Step 5: Outline pass:
  for each pixel in buffer where pixel is NOT transparent:
    check 4 neighbors (up, down, left, right)
    if ANY neighbor is transparent or out of bounds:
      mark that neighbor position for outline

  for each marked outline position:
    if that position is currently transparent:
      buffer.SetPixel(x, y, palette[Outline], z=999)
```

## File Structure to Create

```
BodyGenerator/
  Core/
    PixelBuffer.cs           -- pixel grid with z-buffered SetPixel
    ColorRole.cs             -- enum
    TemplatePixel.cs         -- struct
    BodyPartTemplate.cs      -- part data model
    BodyTemplate.cs          -- collection of parts + hierarchy
    PoseDefinition.cs        -- root pos, draw order, offsets
    CreatureDefinition.cs    -- palette + refs
    BodyHierarchyNode.cs     -- tree node
  Pipeline/
    YamlLoader.cs            -- loads all YAML files, returns typed objects
    TemplateParser.cs        -- parses pixel strings (".CCs." to TemplatePixel[])
    JointResolver.cs         -- recursive joint position calculation
    SpriteCompositor.cs      -- paints parts onto PixelBuffer in draw order
    OutlinePass.cs           -- adds 1px dark outline around silhouette
    SpriteGenerator.cs       -- top-level: CreatureDefinition to Texture2D
  Content/
    Sprites/
      bodies/
        humanoid.yaml
      poses/
        idle.yaml
        walk.yaml
        attack.yaml
        guard.yaml
      creatures/
        human_ranger.yaml
  TestHarness/
    Game1.cs                 -- MonoGame window to display sprites
```

## Test Harness Requirements

- MonoGame window, dark background
- Render the sprite at 8x or 10x scale using nearest-neighbor
- Set SamplerState.PointClamp on SpriteBatch for crisp scaling
- Display the 32x32 sprite scaled up to about 256x256 or 320x320
- Keyboard controls:
  - 1/2/3/4 to switch poses (idle/walk/attack/guard)
  - O to toggle outline pass on/off
  - G to toggle pixel grid overlay
- Show all 4 poses side by side at smaller scale (5x) below the main view
- Show current pose name on screen

## Dependencies

- YamlDotNet (NuGet) for YAML parsing
- MonoGame (already in project)
- No ImageSharp, no System.Drawing, no vector libraries

## What NOT to Build

- No hair, head shapes, or species variations yet
- No equipment or weapons
- No animation interpolation or tweening
- No sprite sheet export
- No clothing overlay system (cloth colors are baked into part templates via ColorRole)

## Critical Notes

1. The pixel character map is CASE SENSITIVE: lowercase s = SkinShadow, CAPITAL S = ClothShadow. This caused bugs in prototyping. Triple-check the TemplateParser.
2. All arms draw BEFORE torso and head in idle/guard poses. This prevents body parts bleeding through on top of the head.
3. The outline pass only writes to TRANSPARENT pixels. It never overwrites existing sprite pixels.
4. Sprites must be rendered with SpriteBatch using SamplerState.PointClamp for crisp nearest-neighbor scaling. No filtering.
5. The validated interactive prototype is available as a separate artifact in this conversation for visual reference.
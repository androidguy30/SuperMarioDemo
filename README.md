# Super Mario Demo - Unity WebGL Player

A classic Super Mario Bros-inspired platformer demo with a complete Unity project and a playable HTML5 web demo.

## Project Structure

```
SuperMarioDemo/
├── index.html                          # Main landing page (opens the web demo)
├── WebDemo/
│   └── index.html                      # Playable HTML5 Canvas game
└── UnityProject/
    ├── Assets/
    │   ├── Scripts/
    │   │   ├── PlayerController.cs     # Mario movement, jumping, physics
    │   │   ├── GameManager.cs          # Score, lives, game state, audio
    │   │   ├── EnemyController.cs      # Goomba & Koopa AI
    │   │   ├── LevelGenerator.cs       # Procedural level building
    │   │   ├── CameraFollow.cs         # Side-scrolling camera
    │   │   ├── BlockBase.cs            # Base class for interactive blocks
    │   │   ├── QuestionBlock.cs        # ? blocks with coins/mushrooms
    │   │   ├── BrickBlock.cs           # Breakable brick blocks
    │   │   ├── Flagpole.cs             # End-of-level flag sequence
    │   │   ├── KillZone.cs             # Pit death trigger
    │   │   ├── CoinPickup.cs           # Collectible coins
    │   │   └── ParallaxBackground.cs   # Parallax scrolling backgrounds
    │   ├── Scenes/                     # Unity scene files
    │   ├── Prefabs/                    # Reusable game object prefabs
    │   ├── Materials/                  # Rendering materials
    │   ├── Sprites/                    # 2D sprite assets (add your own)
    │   └── WebGLTemplates/
    │       └── MarioTemplate/          # Custom Unity WebGL build template
    └── ProjectSettings/                # Unity project configuration
```

## Play the Web Demo

Open `index.html` in any modern browser - no server required.

### Controls

| Action | Keys |
|--------|------|
| Move | Arrow Keys or A/D |
| Jump | Up Arrow, W, or Space |
| Run | Hold Shift |
| Start | Enter |

## Unity Project Setup

### Requirements

- Unity 2021.3 LTS or newer (2022.3+ recommended)
- WebGL Build Support module installed

### Steps

1. Open Unity Hub
2. Click **Open** and select the `UnityProject/` folder
3. Wait for Unity to import the project
4. Open `Assets/Scenes/MainScene`

### Setting Up the Scene

Since Unity scene files require the editor to create properly, you'll need to set up the scene manually:

1. **Create GameObjects:**
   - Empty GameObject "GameManager" → attach `GameManager.cs`
   - Empty GameObject "LevelGenerator" → attach `LevelGenerator.cs`
   - Create a 2D Camera → attach `CameraFollow.cs`
   - Empty GameObject "KillZone" at Y=-20 → add BoxCollider2D (trigger), attach `KillZone.cs`

2. **Create Prefabs** (in `Assets/Prefabs/`):
   - **Player**: Sprite + Rigidbody2D (freeze rotation Z) + BoxCollider2D + `PlayerController.cs`
     - Child "GroundCheck" at feet position
     - Child "HeadCheck" at head position
     - Tag: "Player", Layer: "Player"
   - **Ground**: Sprite + BoxCollider2D, Layer: "Ground"
   - **Brick**: Sprite + BoxCollider2D + `BrickBlock.cs`, Layer: "Ground"
   - **QuestionBlock**: Sprite + BoxCollider2D + `QuestionBlock.cs`, Layer: "Ground"
   - **Goomba**: Sprite + Rigidbody2D + BoxCollider2D + `EnemyController.cs` (type=Goomba)
     - Child "GroundCheck", Child "WallCheck"
     - Tag: "Enemy", Layer: "Enemies"
   - **Koopa**: Same as Goomba but with EnemyType=Koopa
   - **Pipe**: Sprite + BoxCollider2D, Layer: "Ground"
   - **Flagpole**: Sprite + BoxCollider2D (trigger) + `Flagpole.cs`

3. **Assign prefabs** to LevelGenerator's inspector fields

4. **Layer setup** (Edit > Project Settings > Tags and Layers):
   - Layer 8: Ground
   - Layer 9: Enemies
   - Layer 10: Player

### Building for WebGL

1. File → Build Settings
2. Select **WebGL** platform, click **Switch Platform**
3. Player Settings:
   - Resolution: 800x480
   - WebGL Template: MarioTemplate
   - Compression Format: Gzip or Brotli
4. Click **Build**
5. Host the output folder on any web server

## Gameplay Features

- **Player physics**: Acceleration, deceleration, variable jump height, coyote time, jump buffering
- **Enemies**: Goombas (stomp to defeat) and Koopas (stomp for shell, kick shell)
- **Blocks**: Question blocks drop coins, brick blocks break on hit
- **Level**: World 1-1 inspired layout with pipes, staircases, gaps, and flagpole
- **Camera**: Classic one-way scrolling camera with smooth follow
- **HUD**: Score, coins, lives, world indicator, and timer
- **States**: Title screen, gameplay, death, game over, course clear

## License

This is a fan-made demo for educational purposes. Super Mario is a trademark of Nintendo.
All code in this repository is available under the MIT License.

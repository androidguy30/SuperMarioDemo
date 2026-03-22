# Super Mario Demo - Educational Game SDK Integration

A Super Mario platformer that integrates the [SAT Drill SDK](https://learnerlabs.app) to deliver educational content during gameplay. Players hit purple "educational blocks" to trigger quiz drills — answering correctly earns bonus points.

This project serves as a reference implementation for game developers integrating the `<sat-drill>` Web Component into any HTML5 or WebGL game.

## Quick Start

```bash
git clone https://github.com/androidguy30/SuperMarioDemo.git
cd SuperMarioDemo
cp .env.example .env
# Edit .env — add your API key (get one from developers@learnerlabs.app)
python3 -m http.server 8080
# Open http://localhost:8080
```

Hit a purple block in-game or click **Test Drill** in the sidebar to trigger a drill.

## How It Works

```
Player hits purple block
        │
        ▼
Game pauses + input disabled
        │
        ▼
┌─────────────────────┐
│   <sat-drill>       │  ← Web Component renders quiz overlay
│   SDK fetches       │     directly on top of the game canvas
│   question from API │
└─────────────────────┘
        │
        ▼
Player answers (or skips)
        │
        ▼
Score bonus awarded (accuracy × 500)
        │
        ▼
Game resumes + 30s cooldown
```

The SDK handles all quiz UI, question fetching, answer validation, and progress tracking. Your game just needs to pause, show the overlay, and resume.

## Integration Guide

### 1. Add the SDK

```html
<script type="module" src="vendor/sat-drill.js"></script>
```

### 2. Add the overlay HTML

```html
<div id="drill-overlay" style="position:fixed;inset:0;z-index:100000;display:none;
     align-items:center;justify-content:center;background:rgba(0,0,0,0.7);">
    <sat-drill
        id="sat-drill-el"
        domain="Math"
        difficulty="mixed"
        question-count="1"
        show-timer="false"
        theme-mode="dark"
    ></sat-drill>
</div>
```

### 3. Wire it up

```javascript
const drillBridge = {
    overlay: document.getElementById('drill-overlay'),
    drillEl: document.getElementById('sat-drill-el'),
    cooldownUntil: 0,

    init(apiKey, apiUrl) {
        this.drillEl.setAttribute('api-key', apiKey);
        this.drillEl.setAttribute('api-url', apiUrl);

        // Listen for SDK events
        this.drillEl.addEventListener('drill-completed', (e) => this.onComplete(e));
        this.drillEl.addEventListener('drill-abandoned', () => this.dismiss());
        this.drillEl.addEventListener('drill-error', () => this.dismiss());
        this.drillEl.addEventListener('drill-close', () => this.dismiss());
    },

    show() {
        if (Date.now() < this.cooldownUntil) return;
        pauseGame();               // Your game's pause function
        this.overlay.style.display = 'flex';
    },

    onComplete(e) {
        const { accuracy = 0 } = e.detail || {};
        const bonus = Math.round(accuracy * 500);
        if (bonus > 0) addScore(bonus);  // Your game's score function
        this.cooldownUntil = Date.now() + 30000;
        this.dismiss();
    },

    dismiss() {
        this.overlay.style.display = 'none';
        resumeGame();              // Your game's resume function
    }
};

// Load API key from .env at runtime
fetch('.env').then(r => r.text()).then(text => {
    const env = {};
    text.split('\n').forEach(line => {
        const m = line.match(/^\s*([^#=]+?)\s*=\s*(.*?)\s*$/);
        if (m) env[m[1]] = m[2];
    });
    drillBridge.init(env.DRILL_API_KEY || '', env.DRILL_API_URL || 'https://learnerlabs.app');
});
```

### 4. Trigger on game events

```javascript
// When player hits an educational block, checkpoint, or any trigger:
drillBridge.show();
```

That's it. The SDK handles everything else.

## SDK Reference

### `<sat-drill>` Attributes

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `api-key` | string | — | **Required.** Your LearnerLabs API key |
| `api-url` | string | `https://learnerlabs.app` | API base URL |
| `domain` | string | — | `"Math"` or `"Reading and Writing"` |
| `skill` | string | — | Specific skill filter |
| `difficulty` | string | `"mixed"` | `"easy"`, `"medium"`, `"hard"`, or `"mixed"` |
| `question-count` | number | `5` | Questions per drill (1-20) |
| `time-limit` | number | — | Time limit in seconds |
| `show-timer` | boolean | `true` | Show countdown timer |
| `show-progress` | boolean | `true` | Show progress bar |
| `show-results` | boolean | `true` | Show results screen |
| `theme-mode` | string | `"light"` | `"light"` or `"dark"` |
| `theme-primary` | string | `"#4F46E5"` | Primary color hex |
| `theme-accent` | string | `"#10B981"` | Accent color hex |

### SDK Events

| Event | Detail | When |
|-------|--------|------|
| `drill-started` | `{ sessionId, questionCount, domain }` | Drill begins |
| `question-answered` | `{ questionIndex, isCorrect, timeSpent }` | Each answer |
| `drill-completed` | `{ score, accuracy, correct, total, timeSpent }` | All questions answered |
| `drill-abandoned` | `{ questionsAnswered, questionsTotal }` | User quit early |
| `drill-error` | `{ code, message }` | API or SDK error |
| `drill-close` | — | Overlay closed |

### Key Integration Requirements

1. **Pause your game loop** when the drill shows — the SDK renders HTML on top of your canvas
2. **Disable game input** (keyboard/touch/gamepad) so keypresses don't affect the game behind the overlay
3. **Resume cleanly** on `drill-completed`, `drill-abandoned`, `drill-error`, and `drill-close`
4. **Add a cooldown** (30s recommended) to prevent rapid re-triggering
5. **Never hardcode API keys** — load from `.env` or your config system

## Project Structure

```
SuperMarioDemo/
├── index.html                    # Parent page with sidebar dashboard
├── .env.example                  # Environment template (copy to .env)
├── .gitignore                    # Excludes .env from version control
├── WebDemo/
│   ├── index.html               # Game engine (Canvas 2D) + drillBridge
│   ├── vendor/
│   │   ├── sat-drill.js         # SAT Drill Web Component
│   │   ├── headless.mjs         # Headless API client (for Node.js/SSR)
│   │   └── types.d.ts           # TypeScript definitions
│   └── examples/
│       └── quick-start.html     # Minimal SDK example
├── docs/sdk/
│   ├── DEVELOPER_GUIDE.md       # Full SDK developer guide
│   ├── REST_API_REFERENCE.md    # REST API docs
│   └── GAME_ENGINE_EXAMPLES.md  # Phaser, Three.js, Pixi, Unity, Godot examples
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

## Architecture

The game runs inside an iframe. The parent page hosts a Learning Dashboard sidebar.

```
Parent (index.html)                 Iframe (WebDemo/index.html)
┌─────────────────────┐            ┌──────────────────────────┐
│ Learning Dashboard  │            │ Game Engine (Canvas 2D)  │
│ ├── Progress bar    │  postMsg   │ ├── GameEventBus         │
│ ├── Score           │◄──────────►│ ├── DrillBridge          │
│ ├── Quiz history    │            │ │   └── <sat-drill>      │
│ └── Test Drill btn  │            │ └── ParentBridge         │
└─────────────────────┘            └──────────────────────────┘
```

**PostMessage protocol** (game → parent):
- `EDUCATIONAL_BLOCK` — purple block was hit
- `DRILL_COMPLETED` — drill finished with `{ accuracy, correct, total, bonus }`
- `DRILL_DISMISSED` — drill skipped or errored
- `STATE_CHANGE` — game state changed (playing, paused, dead, win)

**PostMessage protocol** (parent → game):
- `TRIGGER_DRILL` — manually trigger a drill (test button)
- `PAUSE_GAME` / `RESUME_GAME` — external pause control

## Controls

| Action | Keyboard | Touch |
|--------|----------|-------|
| Move | Arrow Keys / A, D | D-Pad |
| Jump | Up / W / Space | A button |
| Run | Shift | B button |
| Start | Enter | Start button |

## Gameplay Features

- **Player physics**: Acceleration, deceleration, variable jump height, coyote time, jump buffering
- **Enemies**: Goombas (stomp to defeat) and Koopas (stomp for shell, kick shell)
- **Blocks**: Question blocks drop coins, brick blocks break on hit, educational blocks trigger drills
- **Level**: World 1-1 inspired layout with pipes, staircases, gaps, and flagpole
- **Camera**: Classic one-way scrolling camera with smooth follow
- **HUD**: Score, coins, lives, world indicator, and timer
- **States**: Title screen, gameplay, paused, death, game over, course clear

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
   - **Ground**: Sprite + BoxCollider2D, Layer: "Ground"
   - **Brick**: Sprite + BoxCollider2D + `BrickBlock.cs`, Layer: "Ground"
   - **QuestionBlock**: Sprite + BoxCollider2D + `QuestionBlock.cs`, Layer: "Ground"
   - **Goomba**: Sprite + Rigidbody2D + BoxCollider2D + `EnemyController.cs` (type=Goomba)
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

## API Keys

Get a key from **developers@learnerlabs.app**. Keys come in two types:

| Type | Prefix | Origin Check | Use |
|------|--------|-------------|-----|
| Test | `pk_test_` | Relaxed | Local dev, staging |
| Live | `pk_live_` | Strict | Production |

## Further Reading

- [SDK Developer Guide](docs/sdk/DEVELOPER_GUIDE.md) — authentication, rate limits, error handling
- [REST API Reference](docs/sdk/REST_API_REFERENCE.md) — raw API endpoints
- [Game Engine Examples](docs/sdk/GAME_ENGINE_EXAMPLES.md) — Phaser, Three.js, Pixi, Unity WebGL, Godot

## License

Game code is available under the MIT License. Super Mario is a trademark of Nintendo.
The SAT Drill SDK is provided by LearnerLabs under its own license (see `WebDemo/vendor/LICENSE`).

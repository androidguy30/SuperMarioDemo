# Game Integration Prompt for SAT Drill SDK by LearnerLabs

Use this prompt when asking an AI assistant (Claude, ChatGPT, Copilot, etc.) to integrate the SAT Drill SDK into an existing game.

---

## The Prompt

Copy everything below the line and paste it into your AI assistant, replacing the placeholders in `[brackets]` with your actual values.

---

```
You are integrating the SAT Drill SDK from LearnerLabs into an existing web-based game. The SDK provides embeddable SAT practice questions via a `<sat-drill>` web component. When a player triggers a learning event in the game, the game pauses, a quiz overlay appears with a real SAT question, and after the player answers, the game resumes and awards bonus points based on accuracy.

## What You Have

1. **The game**: [DESCRIBE YOUR GAME HERE — e.g., "A Phaser 3 platformer where the player collects coins and avoids enemies", "A Three.js 3D world where the player explores islands", "A vanilla JS canvas game", etc.]

2. **The game's HTML file**: [PATH TO YOUR GAME'S INDEX.HTML]

3. **The trigger event**: [DESCRIBE WHEN THE QUIZ SHOULD APPEAR — e.g., "When the player hits a special block", "When the player lands on a quiz tile", "Every 60 seconds", "When the player opens a treasure chest", etc.]

4. **The SDK file**: `vendor/sat-drill.js` (~341 KB web component, already copied into the game's directory)

5. **API credentials**: Stored in a `.env` file at the game project root:
   ```
   DRILL_API_KEY=pk_test_YOUR_KEY_HERE
   DRILL_API_URL=http://localhost:3000
   ```

## SDK Reference

### The `<sat-drill>` Web Component

A Shadow DOM-isolated custom element. Add it to HTML, set attributes, and listen for events. It handles all quiz UI internally (question rendering, answer selection, feedback, LaTeX math, results).

**HTML (no api-key or api-url hardcoded — loaded from .env at runtime):**
```html
<script type="module" src="vendor/sat-drill.js"></script>

<div id="drill-overlay">
  <sat-drill
    id="sat-drill-el"
    domain="Math"
    difficulty="mixed"
    question-count="1"
    show-timer="false"
    show-progress="false"
    theme-mode="dark"
  ></sat-drill>
</div>
```

**CSS for the overlay (must be above all game layers):**
```css
#drill-overlay {
  position: fixed;
  inset: 0;
  z-index: 100000;
  display: none;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(4px);
}
#drill-overlay sat-drill {
  width: min(480px, 90vw);
  max-height: 85vh;
}
```

### Attributes

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `api-key` | string | — | **Required.** API key (set via JS from .env, never hardcode) |
| `api-url` | string | auto | API base URL (set via JS from .env) |
| `domain` | string | — | `"Math"` or `"Reading and Writing"` (high-level, auto-expands to sub-domains server-side). Or specific: `"Algebra"`, `"Advanced Math"`, `"Problem-Solving and Data Analysis"`, `"Geometry and Trigonometry"`, `"Information and Ideas"`, `"Craft and Structure"`, `"Standard English Conventions"`, `"Expression of Ideas"` |
| `skill` | string | — | Specific skill (e.g., `"Linear equations in 1 variable"`) |
| `difficulty` | string | `"mixed"` | `"easy"`, `"medium"`, `"hard"`, or `"mixed"` |
| `question-count` | number | `5` | Questions per drill (1–20). Use `1` for single-question game challenges |
| `show-timer` | boolean | `false` | Show timer |
| `show-progress` | boolean | `true` | Show progress bar |
| `show-results` | boolean | `true` | Show results screen |
| `theme-mode` | string | `"light"` | `"light"` or `"dark"` |
| `theme-primary` | hex | `#4F46E5` | Primary color |

### Events (CustomEvents on the `<sat-drill>` element)

| Event | Key Payload Fields | Game Action |
|-------|-------------------|-------------|
| `drill-started` | `sessionId`, `questionCount`, `domain` | Optional: show "get ready" animation |
| `question-answered` | `questionIndex`, `isCorrect`, `timeSpent`, `difficulty` | Optional: show per-question feedback in game |
| `drill-completed` | `score`, `accuracy`, `timeSpent`, `xpAwarded` | **Award bonus points**, resume game |
| `drill-abandoned` | `questionsAnswered`, `questionsTotal`, `reason` | Resume game, no bonus |
| `drill-error` | `code`, `message` | Resume game gracefully |

### Error Codes

| Code | Meaning |
|------|---------|
| `INVALID_API_KEY` | Key missing, invalid, or expired |
| `RATE_LIMITED` | Too many requests |
| `ORIGIN_NOT_ALLOWED` | Domain not in allowed origins (use `pk_test_` for dev) |
| `INVALID_INPUT` | Bad config (missing domain/skill) |

## Integration Pattern: DrillBridge

Create a `drillBridge` object that:
1. **Loads API key from `.env`** at runtime (fetch `.env`, parse key=value lines)
2. **Sets `api-key` and `api-url`** attributes on the `<sat-drill>` element via JS
3. **Listens for the game's trigger event** (whatever triggers a quiz)
4. **Pauses the game** when showing a drill (pause game loop, stop input)
5. **Shows the overlay** (`overlay.style.display = 'flex'`)
6. **Optionally randomizes domain** between "Math" and "Reading and Writing"
7. **Listens for SDK completion events** to award points and resume
8. **Implements a cooldown** (e.g., 30 seconds) to prevent rapid re-triggering
9. **Resumes the game** after drill completes, is abandoned, or errors

### Reference Implementation (adapt to your game):

```javascript
const drillBridge = {
  overlay: null,
  drillEl: null,
  cooldownUntil: 0,
  COOLDOWN_MS: 30000, // 30s cooldown after completing a drill

  init(apiKey, apiUrl) {
    this.overlay = document.getElementById('drill-overlay');
    this.drillEl = document.getElementById('sat-drill-el');
    if (!this.drillEl) return;

    this.drillEl.setAttribute('api-key', apiKey);
    this.drillEl.setAttribute('api-url', apiUrl);

    // Wire SDK events
    this.drillEl.addEventListener('drill-completed', (e) => this.onComplete(e));
    this.drillEl.addEventListener('drill-abandoned', () => this.onDismiss());
    this.drillEl.addEventListener('drill-error', () => this.onDismiss());
  },

  show() {
    if (Date.now() < this.cooldownUntil) return; // cooldown active
    // TODO: pause your game loop here
    this.overlay.style.display = 'flex';
    // Randomize domain for variety
    const domains = ['Math', 'Reading and Writing'];
    this.drillEl.setAttribute('domain', domains[Math.floor(Math.random() * 2)]);
  },

  onComplete(e) {
    const accuracy = e.detail?.accuracy ?? 0;
    const bonus = Math.round(accuracy * 500); // 0-500 points based on accuracy
    // TODO: add bonus to your game's score here
    this.cooldownUntil = Date.now() + this.COOLDOWN_MS;
    this.dismiss();
  },

  onDismiss() {
    this.cooldownUntil = Date.now() + 15000; // shorter cooldown for abandon/error
    this.dismiss();
  },

  dismiss() {
    this.overlay.style.display = 'none';
    // TODO: resume your game loop here
  }
};

// Load API key from .env (never hardcode!)
fetch('.env').then(r => r.ok ? r.text() : '').then(text => {
  const env = {};
  text.split('\n').forEach(line => {
    const match = line.match(/^\s*([^#=]+?)\s*=\s*(.*?)\s*$/);
    if (match) env[match[1]] = match[2];
  });
  drillBridge.init(
    env.DRILL_API_KEY || '',
    env.DRILL_API_URL || 'http://localhost:3000'
  );
}).catch(() => console.warn('No .env found — drill SDK will not work'));
```

## Your Task

1. Read the game's HTML/JS to understand its structure:
   - How the game loop works (requestAnimationFrame, Phaser scene, etc.)
   - How to pause/resume the game
   - Where the trigger event occurs
   - How the scoring system works

2. Add the SDK integration:
   - Add the `<script>` tag for `vendor/sat-drill.js`
   - Add the `#drill-overlay` HTML and CSS
   - Add the `drillBridge` object adapted to the game's architecture
   - Wire the game's trigger event to `drillBridge.show()`
   - Wire `drillBridge.onComplete()` to the game's scoring system
   - Wire `drillBridge.dismiss()` to the game's resume function
   - Load API key from `.env` (never hardcode in source)

3. Ensure:
   - Overlay z-index is higher than ALL game layers (canvas, controls, UI)
   - Game input is disabled while the drill is showing
   - Game resumes cleanly after ALL SDK outcomes (complete, abandon, error)
   - No API keys appear in committed source code
   - `.env` is in `.gitignore`

## Important Notes

- The `<sat-drill>` component uses Shadow DOM — it won't conflict with your game's CSS
- For local dev, the API URL should be `http://localhost:3000` (the SAT ACE backend)
- For production, change to `https://learnerlabs.app`
- Test keys (`pk_test_*`) skip origin validation — work from any localhost port
- The component handles all quiz UI internally — you only need the overlay container
- `question-count="1"` is recommended for games (single question per trigger, minimal disruption)
- Domain `"Math"` and `"Reading and Writing"` are automatically expanded server-side into all sub-domains
```

---

## Placeholders to Replace

| Placeholder | Replace With |
|-------------|-------------|
| `[DESCRIBE YOUR GAME HERE]` | Brief description of your game engine, genre, and tech stack |
| `[PATH TO YOUR GAME'S INDEX.HTML]` | Absolute path to the game's main HTML file |
| `[DESCRIBE WHEN THE QUIZ SHOULD APPEAR]` | The in-game event that triggers a SAT question |

## Example (filled in)

> "A Phaser 3 platformer (`/Users/dev/my-game/index.html`) where the player collects coins. When the player hits a star block (in `handleStarBlock()` on line 450), a SAT question should appear. The game uses `this.scene.pause('GameScene')` to pause."

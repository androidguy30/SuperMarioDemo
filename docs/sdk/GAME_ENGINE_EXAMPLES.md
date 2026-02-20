# SAT Drill SDK Game Engine Integration Examples

**Audience**: Game developers integrating SAT drill questions into web-based games.

**Last Updated**: 2026-02-19

This document provides integration patterns for popular game engines. Each section includes an architecture overview, a working code example, and engine-specific tips.

---

## Table of Contents

1. [Integration Patterns Overview](#integration-patterns-overview)
2. [Phaser 3](#phaser-3)
3. [Three.js](#threejs)
4. [p5.js](#p5js)
5. [Godot (Web Export)](#godot-web-export)
6. [Pixi.js](#pixijs)
7. [Unity WebGL](#unity-webgl)
8. [General Tips](#general-tips)

---

## Integration Patterns Overview

There are two fundamental approaches to integrating the SDK with a game engine:

| Pattern | How It Works | Effort | Immersion |
|---|---|---|---|
| **HTML Overlay** | Game renders on canvas; SDK renders on a DOM layer on top | Low | Medium |
| **Headless API** | Game calls the REST API directly; renders questions using the engine's native rendering | High | Full |

A third hybrid exists for Three.js specifically:

| Pattern | How It Works | Effort | Immersion |
|---|---|---|---|
| **CSS3DRenderer** | Places the `<sat-drill>` DOM element in 3D space via CSS3DRenderer | Medium | High |

**Recommendation**: Start with the HTML overlay pattern. It takes ~30 minutes to integrate and works with any engine. Move to headless only if you need fully in-engine rendering.

---

## Phaser 3

### Architecture: DOM Overlay

Phaser renders to a canvas element. The SDK renders to a DOM overlay positioned on top of the canvas. When the player triggers a quiz event, the game scene pauses, the overlay appears, and the SDK takes over. When the SDK emits a completion event, the overlay hides and the game resumes.

### Code Example

```javascript
// QuizScene.js -- A Phaser scene that shows an SAT drill overlay

import Phaser from 'phaser';

export class QuizScene extends Phaser.Scene {
  constructor() {
    super({ key: 'QuizScene' });
  }

  init(data) {
    // Data passed from the game scene
    this.difficulty = data.difficulty || 'medium';
    this.domain = data.domain || 'Math';
    this.onResult = data.onResult; // callback: (isCorrect) => void
  }

  create() {
    // Pause the game scene (keeps it visible behind the overlay)
    this.scene.pause('GameScene');

    // Get or create the overlay elements
    this.overlay = document.getElementById('drill-overlay');
    this.drill = document.querySelector('sat-drill');

    // Configure the drill for this challenge
    this.drill.setAttribute('difficulty', this.difficulty);
    this.drill.setAttribute('domain', this.domain);
    this.drill.setAttribute('question-count', '1');

    // Show the overlay
    this.overlay.classList.remove('hidden');

    // Listen for the answer
    this.handleAnswer = (e) => {
      const { isCorrect } = e.detail;

      // Hide overlay
      this.overlay.classList.add('hidden');

      // Resume the game scene
      this.scene.resume('GameScene');

      // Pass result back to the game
      if (this.onResult) {
        this.onResult(isCorrect);
      }

      // Stop this scene
      this.scene.stop('QuizScene');
    };

    this.handleError = (e) => {
      console.error('Drill error:', e.detail.code);
      this.overlay.classList.add('hidden');
      this.scene.resume('GameScene');

      // Give a free pass on error
      if (this.onResult) {
        this.onResult(true);
      }
      this.scene.stop('QuizScene');
    };

    this.drill.addEventListener('question-answered', this.handleAnswer);
    this.drill.addEventListener('drill-error', this.handleError);
  }

  shutdown() {
    // Clean up listeners when the scene is destroyed
    if (this.drill) {
      this.drill.removeEventListener('question-answered', this.handleAnswer);
      this.drill.removeEventListener('drill-error', this.handleError);
    }
  }
}

// Usage from your GameScene:
// this.scene.launch('QuizScene', {
//   difficulty: 'hard',
//   domain: 'Math',
//   onResult: (correct) => {
//     if (correct) this.player.powerUp();
//     else this.player.takeDamage();
//   }
// });
```

### HTML Setup

```html
<div id="phaser-game"></div>

<div id="drill-overlay" class="hidden">
  <div class="overlay-backdrop"></div>
  <div class="overlay-content">
    <sat-drill
      api-key="pk_test_YOUR_KEY"
      show-results="false"
      show-progress="false"
      theme-mode="dark"
    ></sat-drill>
  </div>
</div>

<script src="https://sdk.learnerlabs.app/drill.js"></script>
<script type="module" src="game.js"></script>
```

### Phaser-Specific Tips

- Use `this.scene.launch()` (not `this.scene.start()`) so the game scene stays visible behind the overlay
- Use `this.scene.pause('GameScene')` to freeze the game loop while the drill is active
- Always clean up event listeners in `shutdown()` to prevent memory leaks across scene restarts
- For the headless API approach, use `this.add.text()` for question text and `this.add.rectangle()` for option buttons

---

## Three.js

### Architecture Options

Three.js offers three integration approaches:

1. **HTML Overlay** (simplest): Place a DOM element on top of the WebGL canvas
2. **CSS3DRenderer** (hybrid): Place the `<sat-drill>` element in 3D space
3. **API-Only** (full immersion): Call the REST API and render questions as 3D meshes/textures

The pirate-adventure game in this repository uses a Three.js integration as a working reference implementation. See `src/systems/drillBridge.js` for a production example.

### Code Example: HTML Overlay

```javascript
// drill-overlay.js -- Three.js + SDK overlay integration

import * as THREE from 'three';

class DrillOverlay {
  constructor(renderer, scene, camera) {
    this.renderer = renderer;
    this.scene = scene;
    this.camera = camera;
    this.isActive = false;
    this.animationId = null;

    // Get DOM elements
    this.overlay = document.getElementById('drill-overlay');
    this.drill = document.querySelector('sat-drill');

    // Bind event handlers
    this.drill.addEventListener('question-answered', (e) => {
      this.hide();
      this.onResult?.(e.detail.isCorrect);
    });

    this.drill.addEventListener('drill-error', () => {
      this.hide();
      this.onResult?.(true); // Free pass on error
    });
  }

  show({ difficulty = 'medium', domain = 'Math', onResult }) {
    this.onResult = onResult;
    this.isActive = true;

    // Configure drill
    this.drill.setAttribute('difficulty', difficulty);
    this.drill.setAttribute('domain', domain);
    this.drill.setAttribute('question-count', '1');

    // Show overlay
    this.overlay.classList.remove('hidden');

    // Stop the game animation loop
    if (this.animationId) {
      cancelAnimationFrame(this.animationId);
      this.animationId = null;
    }
  }

  hide() {
    this.isActive = false;
    this.overlay.classList.add('hidden');

    // Resume the game animation loop
    this.resumeGameLoop();
  }

  resumeGameLoop() {
    const animate = () => {
      this.animationId = requestAnimationFrame(animate);
      // Your game update logic here
      this.renderer.render(this.scene, this.camera);
    };
    animate();
  }
}

// Usage:
// const drillOverlay = new DrillOverlay(renderer, scene, camera);
//
// // When player reaches a challenge point:
// drillOverlay.show({
//   difficulty: 'hard',
//   domain: 'Math',
//   onResult: (correct) => {
//     if (correct) unlockDoor();
//     else spawnEnemies();
//   }
// });
```

### Code Example: CSS3DRenderer

```javascript
// drill-3d.js -- Place <sat-drill> in 3D space via CSS3DRenderer

import { CSS3DRenderer, CSS3DObject } from 'three/examples/jsm/renderers/CSS3DRenderer.js';

class Drill3D {
  constructor(webglRenderer) {
    // Create a CSS3DRenderer that overlays the WebGL renderer
    this.cssRenderer = new CSS3DRenderer();
    this.cssRenderer.setSize(window.innerWidth, window.innerHeight);
    this.cssRenderer.domElement.style.position = 'absolute';
    this.cssRenderer.domElement.style.top = '0';
    this.cssRenderer.domElement.style.pointerEvents = 'none';
    document.body.appendChild(this.cssRenderer.domElement);

    // Create a container div for the drill
    this.container = document.createElement('div');
    this.container.innerHTML = `
      <sat-drill
        api-key="pk_test_YOUR_KEY"
        question-count="1"
        show-results="false"
        theme-mode="dark"
        style="width: 400px; pointer-events: auto;"
      ></sat-drill>
    `;

    // Wrap in CSS3DObject and position in 3D space
    this.cssObject = new CSS3DObject(this.container);
    this.cssObject.position.set(0, 150, -300); // In front of the player
    this.cssObject.visible = false;
  }

  show(scene, camera) {
    scene.add(this.cssObject);
    this.cssObject.visible = true;
    // Make the drill face the camera
    this.cssObject.lookAt(camera.position);
  }

  hide(scene) {
    this.cssObject.visible = false;
    scene.remove(this.cssObject);
  }

  render(scene, camera) {
    this.cssRenderer.render(scene, camera);
  }
}
```

### Three.js-Specific Tips

- The CSS3DRenderer must be layered on top of the WebGL renderer with `position: absolute` and `pointer-events: none`
- Set `pointer-events: auto` on the `<sat-drill>` element so clicks reach it
- When using CSS3DRenderer, call both `webglRenderer.render()` and `cssRenderer.render()` in your animation loop
- For the pirate-adventure integration pattern, see the `src/systems/` directory in this repository

---

## p5.js

### Architecture: DOM Overlay or In-Canvas

p5.js supports two approaches:

1. **DOM Overlay**: Use `createElement()` to add the `<sat-drill>` element as a DOM sibling
2. **In-Canvas**: Use the headless API and render questions with p5's `text()`, `rect()`, and drawing functions

### Code Example: DOM Overlay

```javascript
// sketch.js -- p5.js with SDK DOM overlay

let drill;
let overlay;
let gameState = 'playing'; // 'playing' | 'quiz' | 'paused'
let quizResult = null;

function setup() {
  createCanvas(800, 600);

  // Create the overlay container
  overlay = createDiv('');
  overlay.id('drill-overlay');
  overlay.class('hidden');
  overlay.style('position', 'fixed');
  overlay.style('inset', '0');
  overlay.style('z-index', '100');
  overlay.style('display', 'flex');
  overlay.style('align-items', 'center');
  overlay.style('justify-content', 'center');
  overlay.style('background', 'rgba(0,0,0,0.7)');

  // The sat-drill element must be added via innerHTML
  // (p5 createElement does not support custom elements directly)
  overlay.elt.innerHTML = `
    <sat-drill
      api-key="pk_test_YOUR_KEY"
      domain="Math"
      question-count="1"
      show-results="false"
      show-progress="false"
      theme-mode="dark"
    ></sat-drill>
  `;

  drill = overlay.elt.querySelector('sat-drill');

  drill.addEventListener('question-answered', (e) => {
    quizResult = e.detail.isCorrect;
    hideQuiz();
  });

  drill.addEventListener('drill-error', () => {
    quizResult = true; // Free pass
    hideQuiz();
  });
}

function draw() {
  if (gameState === 'quiz') return; // Freeze game during quiz

  background(30);

  // Draw your game here
  fill(255);
  textSize(24);
  text('Press Q to trigger a quiz question', 100, 300);

  if (quizResult !== null) {
    fill(quizResult ? '#22c55e' : '#ef4444');
    textSize(32);
    text(quizResult ? 'Correct!' : 'Wrong!', 300, 200);
  }
}

function keyPressed() {
  if (key === 'q' || key === 'Q') {
    showQuiz('medium');
  }
}

function showQuiz(difficulty) {
  gameState = 'quiz';
  quizResult = null;
  drill.setAttribute('difficulty', difficulty);
  overlay.removeClass('hidden');
  overlay.style('display', 'flex');
}

function hideQuiz() {
  gameState = 'playing';
  overlay.addClass('hidden');
  overlay.style('display', 'none');
}
```

### Code Example: Headless In-Canvas

```javascript
// sketch-headless.js -- p5.js rendering questions natively on canvas

let drillHandle = null;
let currentQuestion = null;
let feedback = null;
let selectedOption = -1;
let options = [];

function setup() {
  createCanvas(800, 600);
  textFont('Georgia');
}

function draw() {
  background(30, 30, 50);

  if (!currentQuestion && !feedback) {
    // Idle state
    fill(255);
    textSize(20);
    textAlign(CENTER);
    text('Press SPACE to start a drill', width / 2, height / 2);
    return;
  }

  if (feedback) {
    drawFeedback();
    return;
  }

  if (currentQuestion) {
    drawQuestion();
  }
}

function drawQuestion() {
  const q = currentQuestion;

  // Question text
  fill(255);
  textSize(18);
  textAlign(LEFT);
  text(q.text, 50, 60, width - 100);

  // Options
  options = q.options;
  for (let i = 0; i < options.length; i++) {
    const y = 250 + i * 70;
    const isHovered = mouseY > y && mouseY < y + 50 && mouseX > 50 && mouseX < width - 50;

    // Option background
    fill(isHovered ? 80 : 50, isHovered ? 80 : 50, isHovered ? 120 : 80);
    stroke(100, 100, 180);
    rect(50, y, width - 100, 50, 8);

    // Option text
    fill(255);
    noStroke();
    textSize(16);
    text(`${options[i].key}. ${options[i].text}`, 70, y + 32);
  }
}

function drawFeedback() {
  textAlign(CENTER);
  textSize(28);
  fill(feedback.isCorrect ? '#22c55e' : '#ef4444');
  text(feedback.isCorrect ? 'Correct!' : 'Incorrect', width / 2, height / 2 - 40);

  fill(200);
  textSize(14);
  text(`Answer: ${feedback.correctAnswer}`, width / 2, height / 2 + 20);
  text('Press SPACE to continue', width / 2, height / 2 + 60);
}

function mousePressed() {
  if (!currentQuestion || feedback) return;

  for (let i = 0; i < options.length; i++) {
    const y = 250 + i * 70;
    if (mouseY > y && mouseY < y + 50 && mouseX > 50 && mouseX < width - 50) {
      drillHandle.submitAnswer(options[i].key);
      break;
    }
  }
}

function keyPressed() {
  if (key === ' ') {
    if (feedback) {
      feedback = null;
      drillHandle.advance();
    } else if (!currentQuestion) {
      startDrill();
    }
  }
}

async function startDrill() {
  const { startDrill } = await import('@learnerlabs/sat-drill');

  drillHandle = startDrill({
    apiKey: 'pk_test_YOUR_KEY',
    apiUrl: 'https://learnerlabs.app',
    domain: 'Math',
    questionCount: 3,
    onQuestion: (q) => {
      currentQuestion = q;
      feedback = null;
    },
    onFeedback: (fb) => {
      feedback = fb;
      currentQuestion = null;
    },
    onComplete: (results) => {
      currentQuestion = null;
      feedback = null;
      console.log('Drill complete! Score:', results.score);
    },
    onError: (err) => {
      console.error('Drill error:', err.code);
      currentQuestion = null;
    },
  });
}
```

### p5.js-Specific Tips

- p5's `createElement()` does not support custom elements. Use `elt.innerHTML` to inject `<sat-drill>`
- For the headless approach, use `textWrap(WORD)` and `text(str, x, y, maxWidth)` for word-wrapped question text
- LaTeX rendering in-canvas requires pre-rendering to an offscreen canvas or using a library like MathJax to generate SVG
- Call `drillHandle.destroy()` in p5's `remove()` function to clean up

---

## Godot (Web Export)

### Architecture: JavaScript Bridge

Godot's web export runs in a browser via Emscripten/WebAssembly. Communication with the SDK happens through `JavaScriptBridge.eval()` (Godot 4) or `JavaScript.eval()` (Godot 3). The SDK runs in the HTML shell, and Godot sends/receives data through JavaScript globals.

### Code Example

**HTML Shell Customization** (`export_template.html`):

```html
<!-- Add to your Godot HTML export template, before </body> -->
<script src="https://sdk.learnerlabs.app/drill.js"></script>

<div id="drill-overlay" style="display:none; position:fixed; inset:0; z-index:100;
  background:rgba(0,0,0,0.8); justify-content:center; align-items:center;">
  <sat-drill
    id="godot-drill"
    api-key="pk_test_YOUR_KEY"
    question-count="1"
    show-results="false"
    show-progress="false"
    theme-mode="dark"
  ></sat-drill>
</div>

<script>
  // Bridge between Godot and the SDK
  window.SATDrillBridge = {
    pendingCallback: null,

    show(difficulty, domain) {
      const drill = document.getElementById('godot-drill');
      const overlay = document.getElementById('drill-overlay');

      drill.setAttribute('difficulty', difficulty);
      drill.setAttribute('domain', domain);

      overlay.style.display = 'flex';
    },

    hide() {
      document.getElementById('drill-overlay').style.display = 'none';
    },

    getResult() {
      // Called by Godot to poll for results
      const result = this._lastResult;
      this._lastResult = null;
      return result;
    },

    _lastResult: null,
  };

  // Listen for SDK events and store results for Godot to poll
  const drill = document.getElementById('godot-drill');

  drill.addEventListener('question-answered', (e) => {
    window.SATDrillBridge._lastResult = JSON.stringify({
      type: 'answered',
      isCorrect: e.detail.isCorrect,
      timeSpent: e.detail.timeSpent,
    });
    window.SATDrillBridge.hide();
  });

  drill.addEventListener('drill-error', (e) => {
    window.SATDrillBridge._lastResult = JSON.stringify({
      type: 'error',
      code: e.detail.code,
    });
    window.SATDrillBridge.hide();
  });
</script>
```

**GDScript** (`drill_manager.gd`):

```gdscript
# drill_manager.gd -- Godot 4.x
extends Node

signal quiz_completed(is_correct: bool)
signal quiz_error(error_code: String)

var _polling := false

func show_quiz(difficulty: String = "medium", domain: String = "Math") -> void:
    if OS.has_feature("web"):
        JavaScriptBridge.eval(
            "window.SATDrillBridge.show('%s', '%s')" % [difficulty, domain]
        )
        _polling = true

func _process(_delta: float) -> void:
    if not _polling:
        return

    var result_json = JavaScriptBridge.eval(
        "window.SATDrillBridge.getResult()"
    )

    if result_json == null:
        return  # No result yet

    _polling = false

    var result = JSON.parse_string(result_json)
    if result == null:
        quiz_error.emit("PARSE_ERROR")
        return

    if result["type"] == "answered":
        quiz_completed.emit(result["isCorrect"])
    elif result["type"] == "error":
        quiz_error.emit(result.get("code", "UNKNOWN"))

# Usage from another node:
# drill_manager.show_quiz("hard", "Math")
# drill_manager.quiz_completed.connect(func(correct):
#     if correct:
#         player.gain_power()
#     else:
#         player.take_damage()
# )
```

### Godot-Specific Tips

- Godot 4 uses `JavaScriptBridge.eval()`, Godot 3 uses `JavaScript.eval()`
- Always check `OS.has_feature("web")` before calling JavaScript bridge functions
- Use a polling pattern (`_process`) rather than callbacks, because JavaScript cannot directly call GDScript functions without registration
- For Godot 4.x, you can also use `JavaScriptBridge.create_callback()` for direct callback registration, avoiding the polling pattern
- Sanitize strings passed to `eval()` to prevent injection. Use `%s` formatting with known-safe enum values.
- The overlay z-index must be higher than Godot's canvas

---

## Pixi.js

### Architecture: DOM Overlay

Pixi.js renders to a canvas element, similar to Phaser. The SDK renders to a DOM sibling of the canvas. The overlay pattern is identical to the general approach.

### Code Example

```javascript
// drill-pixi.js -- Pixi.js + SDK integration

import * as PIXI from 'pixi.js';

class DrillManager {
  constructor(app) {
    this.app = app;
    this.overlay = document.getElementById('drill-overlay');
    this.drill = document.querySelector('sat-drill');
    this.resolveQuiz = null;

    // Set up event listeners once
    this.drill.addEventListener('question-answered', (e) => {
      this.hideOverlay();
      if (this.resolveQuiz) {
        this.resolveQuiz({
          answered: true,
          isCorrect: e.detail.isCorrect,
          timeSpent: e.detail.timeSpent,
        });
        this.resolveQuiz = null;
      }
    });

    this.drill.addEventListener('drill-error', (e) => {
      this.hideOverlay();
      if (this.resolveQuiz) {
        this.resolveQuiz({
          answered: false,
          isCorrect: true, // Free pass on error
          error: e.detail.code,
        });
        this.resolveQuiz = null;
      }
    });
  }

  /**
   * Show a quiz and return a promise that resolves with the result.
   * The game loop can await this to pause until the quiz is done.
   */
  showQuiz({ difficulty = 'medium', domain = 'Math' } = {}) {
    return new Promise((resolve) => {
      this.resolveQuiz = resolve;

      // Configure drill
      this.drill.setAttribute('difficulty', difficulty);
      this.drill.setAttribute('domain', domain);
      this.drill.setAttribute('question-count', '1');

      // Pause Pixi ticker
      this.app.ticker.stop();

      // Show overlay
      this.showOverlay();
    });
  }

  showOverlay() {
    this.overlay.style.display = 'flex';
  }

  hideOverlay() {
    this.overlay.style.display = 'none';
    // Resume Pixi ticker
    this.app.ticker.start();
  }

  destroy() {
    this.drill.removeEventListener('question-answered', this.handleAnswer);
    this.drill.removeEventListener('drill-error', this.handleError);
  }
}

// Usage in your game:
//
// const app = new PIXI.Application({ width: 800, height: 600 });
// const drillManager = new DrillManager(app);
//
// // When player hits a challenge tile:
// async function onChallengeTile(player, tile) {
//   const result = await drillManager.showQuiz({
//     difficulty: tile.difficulty,
//     domain: 'Math',
//   });
//
//   if (result.isCorrect) {
//     player.collectReward(tile);
//   } else {
//     player.loseLife();
//   }
// }
```

### HTML Setup

```html
<div id="game-container">
  <!-- Pixi renders here -->
</div>

<div id="drill-overlay" style="display:none; position:fixed; inset:0; z-index:100;
  background:rgba(0,0,0,0.7); display:flex; align-items:center; justify-content:center;">
  <sat-drill
    api-key="pk_test_YOUR_KEY"
    show-results="false"
    theme-mode="dark"
  ></sat-drill>
</div>

<script src="https://sdk.learnerlabs.app/drill.js"></script>
```

### Pixi.js-Specific Tips

- Use `app.ticker.stop()` and `app.ticker.start()` to pause/resume the game loop
- The promise-based `showQuiz()` pattern works well with async game logic
- For in-engine rendering, Pixi's `PIXI.Text` and `PIXI.Graphics` can render question text and option buttons, paired with the headless API
- Clean up the DrillManager when destroying the Pixi application

---

## Unity WebGL

### Architecture: jslib Bridge

Unity WebGL runs in a browser via Emscripten. Communication between C# and JavaScript happens through `.jslib` plugin files. C# calls JavaScript functions defined in the `.jslib`, and JavaScript calls back to C# via `SendMessage()`.

### Code Example

**JavaScript Plugin** (`Assets/Plugins/WebGL/SATDrill.jslib`):

```javascript
// SATDrill.jslib -- Unity WebGL plugin for SAT Drill SDK

mergeInto(LibraryManager.library, {

  SATDrill_Init: function (apiKeyPtr) {
    var apiKey = UTF8ToString(apiKeyPtr);

    // Create overlay if it doesn't exist
    if (!document.getElementById('unity-drill-overlay')) {
      var overlay = document.createElement('div');
      overlay.id = 'unity-drill-overlay';
      overlay.style.cssText =
        'display:none;position:fixed;inset:0;z-index:100;' +
        'background:rgba(0,0,0,0.8);align-items:center;justify-content:center;';

      var drill = document.createElement('sat-drill');
      drill.id = 'unity-drill';
      drill.setAttribute('api-key', apiKey);
      drill.setAttribute('question-count', '1');
      drill.setAttribute('show-results', 'false');
      drill.setAttribute('show-progress', 'false');
      drill.setAttribute('theme-mode', 'dark');
      drill.style.width = '500px';
      drill.style.maxWidth = '90vw';

      overlay.appendChild(drill);
      document.body.appendChild(overlay);

      // Listen for SDK events
      drill.addEventListener('question-answered', function (e) {
        overlay.style.display = 'none';
        // Send result back to Unity
        var gameObj = window._satDrillGameObject || 'DrillManager';
        SendMessage(gameObj, 'OnQuizResult', e.detail.isCorrect ? '1' : '0');
      });

      drill.addEventListener('drill-error', function (e) {
        overlay.style.display = 'none';
        var gameObj = window._satDrillGameObject || 'DrillManager';
        SendMessage(gameObj, 'OnQuizError', e.detail.code);
      });
    }
  },

  SATDrill_Show: function (difficultyPtr, domainPtr, gameObjectPtr) {
    var difficulty = UTF8ToString(difficultyPtr);
    var domain = UTF8ToString(domainPtr);
    var gameObject = UTF8ToString(gameObjectPtr);

    window._satDrillGameObject = gameObject;

    var drill = document.getElementById('unity-drill');
    var overlay = document.getElementById('unity-drill-overlay');

    drill.setAttribute('difficulty', difficulty);
    drill.setAttribute('domain', domain);

    overlay.style.display = 'flex';
  },

  SATDrill_Hide: function () {
    var overlay = document.getElementById('unity-drill-overlay');
    if (overlay) overlay.style.display = 'none';
  },

});
```

**C# MonoBehaviour** (`Assets/Scripts/DrillManager.cs`):

```csharp
// DrillManager.cs -- Unity C# component for SAT Drill SDK

using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class DrillManager : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void SATDrill_Init(string apiKey);

    [DllImport("__Internal")]
    private static extern void SATDrill_Show(
        string difficulty, string domain, string gameObject);

    [DllImport("__Internal")]
    private static extern void SATDrill_Hide();

    [Header("Configuration")]
    [SerializeField] private string apiKey = "pk_test_YOUR_KEY";

    public event Action<bool> OnQuizCompleted;
    public event Action<string> OnQuizFailed;

    private bool _initialized = false;

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SATDrill_Init(apiKey);
        _initialized = true;
#else
        Debug.LogWarning("SAT Drill SDK only works in WebGL builds.");
#endif
    }

    /// <summary>
    /// Show a quiz question. Results arrive via OnQuizCompleted event.
    /// </summary>
    public void ShowQuiz(string difficulty = "medium", string domain = "Math")
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!_initialized) return;
        Time.timeScale = 0f; // Pause Unity
        SATDrill_Show(difficulty, domain, gameObject.name);
#else
        // In editor, simulate a correct answer for testing
        Debug.Log("[DrillManager] Simulating correct answer (editor mode)");
        OnQuizCompleted?.Invoke(true);
#endif
    }

    /// <summary>
    /// Called from JavaScript via SendMessage when quiz is answered.
    /// Parameter: "1" for correct, "0" for incorrect.
    /// </summary>
    public void OnQuizResult(string result)
    {
        Time.timeScale = 1f; // Resume Unity
        bool isCorrect = result == "1";
        OnQuizCompleted?.Invoke(isCorrect);
    }

    /// <summary>
    /// Called from JavaScript via SendMessage on SDK error.
    /// </summary>
    public void OnQuizError(string errorCode)
    {
        Time.timeScale = 1f; // Resume Unity
        Debug.LogWarning($"SAT Drill error: {errorCode}");
        OnQuizFailed?.Invoke(errorCode);
        // Optionally give a free pass:
        // OnQuizCompleted?.Invoke(true);
    }
}
```

**Usage from other scripts**:

```csharp
// PlayerController.cs
public class PlayerController : MonoBehaviour
{
    [SerializeField] private DrillManager drillManager;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("QuizTrigger"))
        {
            drillManager.OnQuizCompleted += HandleQuizResult;
            drillManager.ShowQuiz("hard", "Math");
        }
    }

    void HandleQuizResult(bool isCorrect)
    {
        drillManager.OnQuizCompleted -= HandleQuizResult;

        if (isCorrect)
        {
            Debug.Log("Player answered correctly!");
            // Open door, give reward, etc.
        }
        else
        {
            Debug.Log("Player answered incorrectly.");
            // Spawn enemies, lose health, etc.
        }
    }
}
```

### Unity WebGL Build Setup

1. Add `drill.js` to your WebGL template. In `ProjectSettings/Player/WebGL/Template`, customize the HTML template to include:
   ```html
   <script src="https://sdk.learnerlabs.app/drill.js"></script>
   ```

2. Place `SATDrill.jslib` in `Assets/Plugins/WebGL/`

3. Place `DrillManager.cs` in your scripts folder and attach to a GameObject named `DrillManager`

### Unity-Specific Tips

- `SendMessage()` only accepts a single string parameter. Encode complex data as JSON or delimited strings.
- Use `Time.timeScale = 0f` to pause the Unity game loop while the quiz is active
- Always wrap `DllImport` calls in `#if UNITY_WEBGL && !UNITY_EDITOR` to prevent build errors
- In the Unity Editor, simulate quiz results for testing without building to WebGL
- The `.jslib` file must be in `Assets/Plugins/WebGL/` for Unity to include it in the build
- The `gameObject.name` passed to `SATDrill_Show` must match the GameObject name in the scene for `SendMessage()` to work

---

## General Tips

### Pausing the Game Loop

Every engine integration should pause the game loop while a quiz is active. This prevents:
- Input being consumed by both the game and the quiz
- Game state advancing while the player is reading a question
- Animation/physics updates wasting CPU during the quiz

| Engine | Pause Method | Resume Method |
|---|---|---|
| Phaser | `this.scene.pause('GameScene')` | `this.scene.resume('GameScene')` |
| Three.js | `cancelAnimationFrame(id)` | Restart the `animate()` loop |
| p5.js | Set a state flag, return early from `draw()` | Clear the flag |
| Godot | `get_tree().paused = true` | `get_tree().paused = false` |
| Pixi.js | `app.ticker.stop()` | `app.ticker.start()` |
| Unity | `Time.timeScale = 0f` | `Time.timeScale = 1f` |

### Always Handle Errors Gracefully

Never let an SDK error block the game. Every integration should listen for `drill-error` and provide a fallback:

```javascript
drill.addEventListener('drill-error', (e) => {
  console.error('Drill error:', e.detail.code);
  hideQuiz();
  givePlayerFreePass(); // Never punish the player for an SDK error
});
```

### Clean Up Resources

Always destroy drill handles when leaving the game context to prevent memory leaks:

```javascript
// Web component: remove event listeners
drill.removeEventListener('question-answered', handler);

// Headless API: call destroy()
drillHandle.destroy();
```

### Preload the SDK

Load the SDK script during your game's loading screen, not when the first quiz triggers:

```javascript
// During loading screen
const script = document.createElement('script');
script.src = 'https://sdk.learnerlabs.app/drill.js';
document.head.appendChild(script);
```

### Test with `pk_test_` Keys

Always use test keys during development. They allow any origin, so you can test on `localhost` without configuring allowed origins.

---

## Related Documentation

- [Developer Guide](./DEVELOPER_GUIDE.md) -- Account setup, security, and troubleshooting
- [REST API Reference](./REST_API_REFERENCE.md) -- Complete endpoint documentation for the headless/API-only approach
- [Snakes & Ladders Guide](./INTEGRATION_GUIDE_SNAKES_AND_LADDERS.md) -- Full worked example with a complete game
- [SDK README](../../packages/sdk/README.md) -- Web component and headless API reference

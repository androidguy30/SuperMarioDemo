---
title: "feat: Add API-Driven Educational Content Integration"
type: feat
date: 2026-02-19
---

# feat: Add API-Driven Educational Content Integration

## Overview

Transform the Super Mario HTML5 Canvas game into an educational gaming platform that fetches quizzes, learning material, and videos from an external API server. Content appears both **alongside the game** (sidebar panel on desktop) and **triggered in-game** (when hitting special "educational" blocks). The game must work across mobile, tablet, and desktop.

**Current state:** The game is a self-contained ~2,000-line vanilla JS/Canvas game with zero network requests, no event system, no pause state, and no cross-iframe communication.

## Problem Statement / Motivation

The existing game is purely entertainment with no educational value. Converting it into a learning platform requires:

1. An API integration layer to fetch dynamic educational content
2. A game event system to trigger content at the right moments
3. A pause mechanism so students can interact with content without dying
4. A responsive layout that works on mobile (touch), tablet, and desktop
5. An input mode system that switches between game controls and content interaction

## Proposed Solution

### Architecture

```
+------------------------------------------------------------------+
|  index.html (Parent Page)                                        |
|                                                                  |
|  +-------------------------+  +---------------------------+     |
|  | Game Canvas              |  | Content Sidebar (desktop) |     |
|  | (iframe: WebDemo/)       |  | - Learning Objectives     |     |
|  |                          |  | - Progress Tracker        |     |
|  |  [postMessage] <---------|--| - Quiz History            |     |
|  |                          |  |                           |     |
|  +--------------------------+  +---------------------------+     |
|         |                              |                         |
|  [Quiz/Video Overlay - DOM]    [GameBridge.js]                   |
|  (positioned over iframe)            |                           |
|                              [ContentManager.js]                 |
|                                      |                           |
+--------------------------------------+---------------------------+
                                       |
                                [BFF Proxy / API Server]
                                       |
                                [Content Database]
```

**Key architectural decisions:**

1. **Keep HTML5 Canvas/JS game** -- Unity WebGL does NOT support mobile browsers. The Canvas game already works cross-platform.
2. **Preserve iframe architecture** -- Add `postMessage` bridge for game-to-parent communication.
3. **DOM overlays for educational content** -- HTML/CSS positioned over the canvas, not rendered in-canvas. This enables rich content (forms, videos, formatted text).
4. **BFF proxy for API security** -- API keys stay server-side; the browser talks to a lightweight proxy.
5. **Graceful degradation** -- If the API is down, the game works normally without educational content.

### Implementation Phases

#### Phase 1: Game Engine Foundation

Add the infrastructure needed inside `WebDemo/index.html` before any API work.

**1a. Event System**

Add a lightweight event emitter to the game engine. Currently all game events are inline in the game loop with no way to hook into them.

```javascript
// WebDemo/index.html -- new EventBus class
class GameEventBus {
  constructor() { this.listeners = new Map(); }
  on(event, cb) {
    if (!this.listeners.has(event)) this.listeners.set(event, []);
    this.listeners.get(event).push(cb);
    return () => { /* unsubscribe */ };
  }
  emit(event, data) {
    (this.listeners.get(event) || []).forEach(cb => cb(data));
  }
}
const gameEvents = new GameEventBus();
```

Emit events at these existing locations in `WebDemo/index.html`:
- Coin from question block: line 1489-1491
- Brick break: line 1493-1497
- Goomba stomp: line 1696
- Koopa stomp: line 1733
- Flag reached: line 1624
- Player death: line 1799
- Game state changes: line 1964-2014

**1b. Pause State**

Add `paused` to the game state machine (currently at line 543: `loading, title, playing, dead, gameover, win`).

```javascript
// New state: 'paused'
// In gameLoop switch (line 1964):
case 'paused':
  render();           // Keep rendering frozen frame
  drawPauseOverlay(); // Semi-transparent overlay
  break;
```

When paused: freeze physics (`updatePlaying()` does not run), stop enemy movement, but continue rendering so the game is visible behind overlays.

**1c. Input Mode System**

Create an input mode state machine to resolve the conflict between game controls and content interaction.

```javascript
// Modes: 'game' | 'content'
let inputMode = 'game';

function setInputMode(mode) {
  inputMode = mode;
  if (mode === 'content') {
    // Hide touch controls on mobile
    document.querySelector('.touch-controls')?.classList.add('hidden');
    // Stop capturing game keys
  } else {
    document.querySelector('.touch-controls')?.classList.remove('hidden');
  }
}
```

Key conflict points to resolve:
- `Space`, `ArrowUp/Down/Left/Right` have `preventDefault()` at lines 556-561
- Touch controls overlay at `z-index: 10000` (line 157)
- `Enter` key used for game start/retry

**1d. Educational Tile Type**

Add a new tile type (e.g., `10 = educational_block`) visually distinct from normal question blocks.

- Render with a different color (green/purple with book/star icon) in `drawTile()` (lines 1109-1215)
- Add to `isSolid()` check (line 808)
- Place 5-8 educational blocks at strategic positions in `buildLevel()` (line 729)
- When hit from below: emit `gameEvents.emit('educationalBlock', { blockId, position })` instead of spawning a coin

**1e. PostMessage Bridge (inside iframe)**

```javascript
// WebDemo/index.html -- ParentBridge
class ParentBridge {
  constructor() {
    this.origin = window.location.origin;
    window.addEventListener('message', (e) => {
      if (e.origin !== this.origin) return;
      this.handleMessage(e.data);
    });
  }
  notify(type, payload) {
    window.parent.postMessage({ type, payload }, this.origin);
  }
  handleMessage({ type, payload }) {
    switch (type) {
      case 'PAUSE_GAME': gameState = 'paused'; break;
      case 'RESUME_GAME': gameState = 'playing'; break;
      case 'QUIZ_COMPLETED':
        // Grant reward based on quiz result
        if (payload.passed) score += 500;
        gameState = 'playing';
        setInputMode('game');
        break;
    }
  }
}
```

**Success criteria:**
- [x] `gameEvents.emit()` fires on coin, brick, stomp, flag, death events
- [x] Game can be paused/resumed without breaking state
- [x] Input mode switches cleanly between game and content
- [x] Educational blocks render visually distinct and emit events when hit
- [ ] PostMessage sends/receives messages between iframe and parent

---

#### Phase 2: Parent Page & Content UI

Restructure `index.html` to support the content sidebar and overlay system.

**2a. Responsive Layout (CSS Grid)**

```css
/* Desktop: game + sidebar */
.game-layout {
  display: grid;
  grid-template-columns: 1fr 360px;
  gap: 16px;
  max-width: 1400px;
  height: 100vh;
}

/* Tablet (<=1024px): stack vertically */
@media (max-width: 1024px) {
  .game-layout { grid-template-columns: 1fr; }
}

/* Mobile (<=600px): full-screen game, no sidebar */
@media (max-width: 600px) {
  .game-layout { padding: 0; }
  .content-sidebar { display: none; } /* content via overlay only */
}
```

**2b. Content Sidebar (Desktop/Tablet)**

Sidebar sections:
- **Learning Objectives** -- fetched from API on page load
- **Progress Tracker** -- updates as student completes quizzes
- **Quiz History** -- shows past answers with correct/incorrect indicators
- **Current Topic** -- displays learning material related to the next trigger

**2c. Quiz Overlay System**

HTML/CSS overlay positioned absolutely over the game iframe wrapper. Appears when an educational block is hit.

```html
<div class="game-canvas-wrapper">
  <iframe id="game-iframe" src="WebDemo/index.html"></iframe>
  <div class="quiz-overlay" id="quiz-overlay">
    <div class="quiz-backdrop"></div>
    <div class="quiz-panel">
      <div class="quiz-question"></div>
      <div class="quiz-choices"></div>
      <div class="quiz-feedback" hidden></div>
      <button class="quiz-submit" disabled>Submit</button>
      <button class="quiz-skip">Skip</button>
    </div>
  </div>
</div>
```

Quiz interaction:
- Game pauses when quiz appears
- Touch controls hidden on mobile
- User taps/clicks answer choices
- Submit button confirms selection
- Feedback shown (correct/incorrect with explanation)
- Skip allowed after 3 seconds
- On dismiss: game resumes, reward granted if correct (+500 score)

**2d. Video Overlay**

```html
<div class="video-overlay" id="video-overlay">
  <div class="video-backdrop"></div>
  <div class="video-panel">
    <video controls preload="metadata" playsinline muted>
      <!-- Source set dynamically -->
    </video>
    <button class="video-close">Continue Playing</button>
  </div>
</div>
```

Video considerations:
- Start muted (autoplay policy compliance on mobile)
- `playsinline` attribute for iOS (prevents fullscreen takeover)
- Tap-to-unmute button visible
- "Continue Playing" button to dismiss (not auto-dismiss)
- YouTube/Vimeo embeds via iframe if URL matches those domains

**2e. PostMessage Bridge (parent side)**

```javascript
// index.html -- GameBridge
class GameBridge {
  constructor(iframeId) {
    this.iframe = document.getElementById(iframeId);
    this.origin = window.location.origin;
    this.handlers = new Map();
    window.addEventListener('message', (e) => {
      if (e.origin !== this.origin) return;
      const { type, payload } = e.data;
      if (this.handlers.has(type)) this.handlers.get(type)(payload);
    });
  }
  on(type, cb) { this.handlers.set(type, cb); }
  send(type, payload) {
    this.iframe.contentWindow.postMessage({ type, payload }, this.origin);
  }
}
```

**Success criteria:**
- [ ] Desktop shows game + sidebar side-by-side
- [ ] Tablet stacks game above sidebar
- [ ] Mobile shows full-screen game (sidebar hidden, content via overlay only)
- [ ] Quiz overlay appears over game, accepts input, shows feedback
- [ ] Video overlay plays muted by default, has unmute + close buttons
- [ ] PostMessage bridge works bidirectionally

---

#### Phase 3: API Integration

**3a. API Contract Definition**

Define the expected endpoints:

```
GET  /api/content/objectives?level=1-1
GET  /api/content/quiz/:triggerId
POST /api/content/quiz/:triggerId/submit  { answers: [...] }
GET  /api/content/material/:triggerId
GET  /api/content/video/:triggerId
GET  /api/progress/:sessionId
POST /api/progress/:sessionId  { quizResults: [...] }
```

Expected quiz response:
```json
{
  "id": "quiz-123",
  "type": "quiz",
  "category": "Math",
  "questions": [
    {
      "text": "What is 3 + 5?",
      "choices": ["6", "7", "8", "9"],
      "correctIndex": 2,
      "feedbackCorrect": "That's right!",
      "feedbackIncorrect": "3 + 5 = 8. Try counting on your fingers."
    }
  ]
}
```

**3b. Content Manager (fetch + cache + error handling)**

```javascript
// js/content-manager.js
class ContentManager {
  constructor(apiBaseUrl) {
    this.baseUrl = apiBaseUrl;
    this.cache = new Map();
    this.triggerIndex = 0; // Sequential content assignment
  }

  async fetchContent(triggerId) {
    if (this.cache.has(triggerId)) return this.cache.get(triggerId);

    try {
      const res = await fetch(`${this.baseUrl}/content/quiz/${triggerId}`, {
        signal: AbortSignal.timeout(8000)
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      this.cache.set(triggerId, data);
      return data;
    } catch (err) {
      console.warn('Content fetch failed:', err.message);
      return null; // Graceful degradation: game continues
    }
  }

  prefetch(triggerId) {
    this.fetchContent(triggerId); // Fire and forget, cached for later
  }
}
```

**3c. Prefetching Strategy**

When the player approaches an educational block (within 200px / ~6 tiles), prefetch the content:

```javascript
// In updatePlaying(), check proximity to educational blocks
function checkPrefetchZones(playerX) {
  for (const block of educationalBlocks) {
    if (!block.prefetched && Math.abs(playerX - block.x) < 200) {
      block.prefetched = true;
      contentManager.prefetch(block.triggerId);
    }
  }
}
```

**3d. Graceful Degradation**

If the API is unreachable:
- Game starts normally
- Educational blocks still appear but behave like normal question blocks (spawn coins)
- A small non-intrusive notice appears: "Learning content unavailable"
- No retries during gameplay (avoids performance impact)

**3e. BFF Proxy Server (decide before implementing Phase 3)**

**Decision required:** If the content API requires authentication (API key/token), a proxy is **mandatory** -- API keys must never be in client-side code. If the API is public, skip this step and call the API directly from the browser.

If the content API requires authentication:

```javascript
// server/proxy.js (Express)
app.get('/api/content/:type/:id', async (req, res) => {
  const upstream = await fetch(
    `${process.env.CONTENT_API_BASE}/${req.params.type}/${req.params.id}`,
    { headers: { Authorization: `Bearer ${process.env.API_KEY}` } }
  );
  const data = await upstream.json();
  res.json(data);
});
```

**Success criteria:**
- [ ] Content fetches successfully from API with proper error handling
- [ ] Failed API calls do not break the game
- [ ] Content is cached to avoid duplicate requests
- [ ] Prefetching loads content before player reaches trigger
- [ ] Sidebar updates with learning objectives on page load

---

#### Phase 4: Content Trigger Orchestration

Wire everything together: game events -> API fetch -> content display -> game resume.

**4a. Trigger Flow**

```
Player hits educational block
  -> gameEvents.emit('educationalBlock', { blockId })
  -> ParentBridge.notify('EDUCATIONAL_TRIGGER', { blockId })
  -> GameBridge receives message
  -> ContentManager.fetchContent(blockId)
  -> If content available:
       -> GameBridge.send('PAUSE_GAME')
       -> Show quiz/video/material overlay
       -> User interacts with content
       -> On dismiss: GameBridge.send('QUIZ_COMPLETED', { passed, score })
       -> Game resumes
  -> If content unavailable:
       -> Block behaves as normal question block (spawns coin)
```

**4b. Trigger Throttling**

- Only educational blocks trigger content (not every question block, brick, or stomp)
- Maximum 1 content trigger per 30 seconds (cooldown timer)
- If player hits an educational block during cooldown, it behaves as a normal question block
- 5-8 educational blocks per level (manageable content density)

**4c. Content-to-Block Mapping**

Content served sequentially by hit order per session:
- First block hit (regardless of position) -> API returns content item 1
- Second block hit -> API returns content item 2
- Blocks hit again after death/reset show same content (already cached)
- Player can skip blocks by jumping over them; skipped blocks are not pre-assigned content

**4d. Quiz Answer Handling**

- Correct answer: +500 score, green feedback, 1.5s delay, resume
- Incorrect answer: show correct answer, no penalty, 2s delay, resume
- Skip: no score change, resume immediately

**4e. Content Exhaustion**

When all content has been shown:
- API returns `{ "exhausted": true }`
- Remaining educational blocks become normal question blocks
- Sidebar shows "All content completed!" message

**Success criteria:**
- [ ] Full loop works: hit block -> pause -> quiz -> answer -> resume
- [ ] Throttling prevents content spam
- [ ] Correct/incorrect answers give appropriate feedback and rewards
- [ ] Content exhaustion handled gracefully
- [ ] Death/level reset does not break content sequencing

---

#### Phase 5: Polish & Cross-Platform Testing

**5a. Mobile-Specific Fixes**
- Touch controls hide/show when content overlays appear/dismiss
- Quiz overlay is scrollable if content exceeds viewport height
- Video uses `playsinline` and starts muted
- Orientation change during content display reflows the overlay layout
- "Skip" button positioned away from touch control areas

**5b. Accessibility**
- ARIA labels on quiz choices (`role="radiogroup"`)
- Focus management: focus moves to overlay on show, returns to game on dismiss
- Focus trap inside quiz overlay (Tab cycles through choices + submit)
- Color contrast minimum 4.5:1 for all text on overlay backgrounds
- Video captions/subtitles if provided by API

**5c. Win Screen Summary**
- Show educational performance alongside game score on the "COURSE CLEAR" screen
- "You answered 6/8 correctly" summary
- Update sidebar with final results

**5d. Performance**
- API calls never block the game loop (`requestAnimationFrame`)
- Prefetching prevents visible loading delays
- Video elements created lazily (not pre-loaded)
- Old overlays cleaned up from DOM after dismiss

**Success criteria:**
- [ ] Works on iOS Safari (mobile)
- [ ] Works on Chrome Android (mobile)
- [ ] Works on Chrome/Firefox/Safari (desktop)
- [ ] Orientation changes don't break overlays
- [ ] Keyboard navigation works for quiz on desktop
- [ ] Touch interaction works for quiz on mobile
- [ ] No console errors or warnings
- [ ] Game maintains 60fps during normal gameplay

---

## Technical Considerations

### Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `WebDemo/index.html` | Modify | Add EventBus, pause state, input modes, educational tile, ParentBridge |
| `index.html` | Modify | Restructure layout (CSS Grid), add sidebar, overlays, GameBridge, ContentManager |
| `server/proxy.js` | Create (if API requires auth) | BFF proxy for API key protection |
| `server/package.json` | Create (if API requires auth) | Express + cors + rate-limit dependencies |

### API Dependency

This plan assumes an external API server exists that serves educational content. The API contract is defined in Phase 3a. **If no API server exists yet, a mock API (static JSON files served by the BFF proxy) can be used for development.**

### Security

- Never embed API keys in client-side JavaScript
- Validate `postMessage` origins on both sides of the iframe bridge
- Sanitize API response content before inserting into DOM (prevent XSS)
- Rate-limit API proxy to prevent abuse

### Performance

- API calls are fire-and-forget with cache (never block the game loop)
- Prefetch content 200px before the player reaches a trigger
- Video elements are lazy-loaded (created only when needed)
- Quiz overlay uses CSS transitions (GPU-accelerated) for smooth show/hide

---

## Acceptance Criteria

### Functional Requirements

- [ ] Educational blocks appear in the game level, visually distinct from normal blocks
- [ ] Hitting an educational block pauses the game and shows a quiz overlay
- [ ] Quiz overlay accepts answers via click (desktop) or tap (mobile)
- [ ] Correct answers grant +500 score with positive feedback
- [ ] Incorrect answers show the correct answer without penalty
- [ ] Quiz can be skipped after 3 seconds
- [ ] Video content plays muted by default with unmute option
- [ ] Desktop shows a sidebar with learning objectives and progress
- [ ] Mobile/tablet shows content via full-screen overlay only
- [ ] Game works normally if the API is unreachable (graceful degradation)
- [ ] Content is prefetched as player approaches educational blocks

### Non-Functional Requirements

- [ ] Game maintains 60fps during gameplay (no API-related jank)
- [ ] Works on iOS Safari, Chrome Android, Chrome/Firefox/Safari desktop
- [ ] Quiz overlay is keyboard-navigable (Tab + Enter)
- [ ] Color contrast meets WCAG 2.1 AA (4.5:1 minimum)
- [ ] API responses cached to avoid duplicate requests
- [ ] PostMessage validates origin on both sides

### Quality Gates

- [ ] All game states transition correctly (including new `paused` state)
- [ ] Touch controls hide/show correctly during content display
- [ ] No console errors in any browser
- [ ] Educational content does not interrupt death/win sequences
- [ ] Content exhaustion handled without errors

---

## Open Questions Requiring Decision

| # | Question | Default Assumption | Impact |
|---|----------|-------------------|--------|
| 1 | What is the actual API server URL and auth method? | Mock API with static JSON for development | Blocks Phase 3. If auth required, BFF proxy becomes mandatory (see 3e). |
| 2 | Should we merge the iframe into a single page? | Keep iframe, add postMessage | Simplifies if merged, but changes project structure |
| 3 | What quiz formats are needed? (multiple choice, true/false, free text) | Multiple choice only | Affects quiz UI complexity |
| 4 | Should progress persist across sessions? | No, fresh each session | Requires server-side storage if yes |
| 5 | What video hosting is used? (YouTube, self-hosted, Vimeo) | Self-hosted MP4 via API | Affects video embed implementation |
| 6 | How many educational blocks per level? | 5-8 | Affects content density and pacing |

---

## Dependencies & Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| API server not ready | High | Blocks Phase 3+ | Use mock API (static JSON) for development |
| Video autoplay blocked on mobile | High | Videos won't play | Start muted + tap-to-unmute |
| Touch control conflicts with overlays | Medium | Users can't interact with quizzes | Hide touch controls during content display |
| Content interrupts kill game flow | Medium | Frustrating UX | Throttle to 1 trigger per 30s, only educational blocks |
| CORS failures | Medium | API calls silently fail | BFF proxy on same origin, or proper CORS headers |

---

## References & Research

### Internal References

- Game state machine: `WebDemo/index.html:543`
- Game loop: `WebDemo/index.html:1952-2017`
- Coin from block trigger: `WebDemo/index.html:1489-1491`
- Brick break trigger: `WebDemo/index.html:1493-1497`
- Goomba stomp trigger: `WebDemo/index.html:1694-1700`
- Flag reached trigger: `WebDemo/index.html:1624-1634`
- Player death: `WebDemo/index.html:1799-1805`
- Touch controls init: `WebDemo/index.html:642-702`
- Device detection: `WebDemo/index.html:564-608`
- Iframe embed: `index.html:336`
- Level builder: `WebDemo/index.html:729-799`
- Tile renderer: `WebDemo/index.html:1109-1215`
- Input capture: `WebDemo/index.html:555-562`
- Related PR: #1 (touch controls + mobile support)

### External References

- [MDN postMessage API](https://developer.mozilla.org/en-US/docs/Web/API/Window/postMessage)
- [MDN CORS Guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CORS)
- [MDN Canvas API](https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API)
- [Unity WebGL Mobile Limitation](https://docs.unity3d.com/Manual/webgl-browsercompatibility.html) (reason for keeping Canvas JS)
- [BFF Pattern for API Key Protection](https://blog.gitguardian.com/stop-leaking-api-keys-the-backend-for-frontend-bff-pattern-explained/)
- [WCAG 2.1 Accessibility Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)

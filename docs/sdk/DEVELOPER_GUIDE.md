# SAT Drill SDK Developer Guide

**Audience**: External developers integrating the SAT Drill SDK into their applications.

**Last Updated**: 2026-02-19

---

## Table of Contents

1. [Account Setup](#account-setup)
2. [Authentication](#authentication)
3. [Origin Validation](#origin-validation)
4. [Rate Limiting](#rate-limiting)
5. [Content and Domains](#content-and-domains)
6. [Security Best Practices](#security-best-practices)
7. [Error Handling](#error-handling)
8. [Performance](#performance)
9. [Troubleshooting](#troubleshooting)

---

## Account Setup

### Getting API Keys

The SDK is currently in **beta**. To get started:

1. Email **developers@learnerlabs.app** with your app name and expected domain
2. We will provision a tenant account and generate your API keys
3. You will receive both a test key and a live key

A self-service developer portal is planned for general availability.

### Test Keys vs Live Keys

| | Test Key | Live Key |
|---|---|---|
| **Prefix** | `pk_test_` | `pk_live_` |
| **Origin validation** | Any origin allowed | Strict — must match `allowed_origins[]` |
| **Rate limiting** | Enforced (same tiers) | Enforced |
| **Question bank** | Full access | Full access |
| **Use for** | Local development, CI/CD, staging | Production deployments |

### Key Format

API keys follow the format: `{prefix}{random_hex}`

- **Prefix**: 8 characters (`pk_test_` or `pk_live_`)
- **Key prefix** (used for lookup): first 12 characters of the full key
- **Random portion**: 32 bytes of cryptographic randomness, hex-encoded (64 characters)
- **Total length**: 72 characters

Example: `pk_test_a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef12345678`

Keys are hashed with SHA-256 before storage. The raw key is shown exactly once at creation time. If you lose it, a new key must be generated.

---

## Authentication

### API Key Header

Every request to the SDK API must include the `x-api-key` header:

```
x-api-key: pk_test_YOUR_KEY_HERE
```

This header authenticates your application and associates all activity with your tenant account.

### Session Token Header

After creating a drill session, you receive a `sessionToken` in the response. All subsequent requests for that session must include the `x-sdk-session` header:

```
x-sdk-session: SESSION_TOKEN_FROM_CREATE
```

Session tokens are cryptographically bound to the API key that created them. A session token from tenant A cannot be used with tenant B's API key. This prevents cross-tenant session hijacking.

### Header Summary

| Endpoint | `x-api-key` | `x-sdk-session` |
|---|---|---|
| `POST /create` | Required | Not needed (creates the session) |
| `POST /answer` | Required | Required |
| `POST /complete` | Required | Required |
| `POST /abandon` | Required | Required |
| `GET /session` | Required | Required |

---

## Origin Validation

The SDK validates the `Origin` HTTP header to prevent unauthorized use of your API key from domains you have not approved.

### Test Keys

Test keys (`pk_test_*`) skip origin validation entirely. Any origin is allowed, including `localhost`, `127.0.0.1`, file URLs, and any domain. Use test keys for local development and CI/CD.

### Live Keys

Live keys (`pk_live_*`) enforce strict origin validation:

- The `Origin` header must exactly match one of the origins configured in your `allowed_origins[]` list
- If no `allowed_origins` are configured, all requests are denied (deny-by-default)
- If the `Origin` header is missing, the request is denied
- **No wildcard patterns** are supported. Every origin must be an exact match.
- Comparison is case-sensitive and includes the protocol: `https://example.com` and `http://example.com` are different origins

### Configuring Origins

Contact the LearnerLabs team to configure allowed origins for your live key. Provide the full origin string including protocol:

```
https://my-game.example.com
https://staging.example.com
https://my-game.itch.io
```

### Special Cases

| Scenario | How to Handle |
|---|---|
| `localhost` development | Use a `pk_test_*` key |
| `file://` protocol (local HTML) | Add `"null"` to `allowed_origins` (browsers send `Origin: null` for file URLs) |
| React Native / Capacitor | Add `"null"` to `allowed_origins` (native WebView sends `Origin: null`) |
| Server-to-server (no browser) | `Origin` header is not sent by non-browser clients; requires special configuration — contact us |
| Multiple subdomains | Add each subdomain individually (no wildcard) |

---

## Rate Limiting

### Tier-Based Limits

Rate limits are enforced per API key using a sliding window algorithm:

| Tier | Requests per Minute | Sessions per Day |
|---|---|---|
| **Free** | 30 | 100 |
| **Pro** | 100 | 1,000 |

All new keys start on the free tier. Contact us to upgrade to pro.

### Rate Limit Headers

Every response includes rate limit headers:

```
X-RateLimit-Limit: 30
X-RateLimit-Remaining: 27
X-RateLimit-Reset: 1707840060
```

| Header | Description |
|---|---|
| `X-RateLimit-Limit` | Maximum requests allowed in the current window |
| `X-RateLimit-Remaining` | Requests remaining in the current window |
| `X-RateLimit-Reset` | Unix timestamp (seconds) when the window resets |

### When Rate Limited

When you exceed the limit, the API returns a `429` response:

```json
{
  "error": {
    "code": "RATE_LIMITED",
    "message": "Rate limit exceeded"
  }
}
```

The response includes a `Retry-After` header with the number of seconds to wait before retrying.

### SDK Auto-Retry

The `<sat-drill>` web component and the headless API automatically handle rate limiting:

1. On a 429 response, the SDK reads the `Retry-After` header
2. It waits the specified duration
3. It retries the request (up to 3 total attempts)
4. If all retries fail, a `drill-error` event is dispatched with code `RATE_LIMITED`

If you call the REST API directly, you must implement retry logic yourself.

### Daily Session Limits

In addition to per-minute rate limits, the API enforces a daily session creation cap. If you exceed it, the `POST /create` endpoint returns:

```json
{
  "error": {
    "code": "DAILY_LIMIT_EXCEEDED",
    "message": "Daily session limit of 100 exceeded"
  }
}
```

This limit resets at midnight UTC.

---

## Content and Domains

### SAT Domains

The SAT is divided into two test sections, each with specific domains:

**Math**

| Domain | Example Skills |
|---|---|
| Algebra | Linear equations in 1 variable, Linear functions, Systems of 2 linear equations |
| Advanced Math | Quadratic equations, Polynomial functions, Exponential functions |
| Problem-Solving and Data Analysis | Ratios and proportions, Percentages, Probability |
| Geometry and Trigonometry | Area and volume, Triangles, Circles |

**Reading and Writing**

| Domain | Example Skills |
|---|---|
| Craft and Structure | Words in context, Text structure and purpose, Cross-text connections |
| Information and Ideas | Central ideas, Command of evidence (textual), Command of evidence (quantitative) |
| Standard English Conventions | Boundaries, Form structure and sense |
| Expression of Ideas | Rhetorical synthesis, Transitions |

### Difficulty Levels

| Value | Description |
|---|---|
| `easy` | Foundational concepts, single-step problems |
| `medium` | Multi-step problems, moderate complexity |
| `hard` | Advanced concepts, multi-step reasoning |
| `mixed` | Adaptive selection across all difficulty levels (default) |

### Question Types

| Type | Code | Description |
|---|---|---|
| Multiple Choice | `MCQ` | Four options (A, B, C, D). One correct answer. |
| Student-Produced Response | `SPR` | Free-response. Student enters a numeric or algebraic answer. |

### LaTeX Notation

Question text and explanations may contain LaTeX math notation. Examples:

- Inline: `\(x^2 + 3x - 4 = 0\)`
- Display: `\[\frac{-b \pm \sqrt{b^2 - 4ac}}{2a}\]`

Check the `has_latex` field on each question to determine if LaTeX rendering is needed.

**Web component**: LaTeX is rendered automatically using temml (included in the bundle).

**Headless API**: You must provide your own LaTeX renderer. Recommended libraries:
- [KaTeX](https://katex.org/) -- fast, lightweight
- [MathJax](https://www.mathjax.org/) -- full LaTeX support
- For game engines: pre-render LaTeX to canvas textures

---

## Security Best Practices

### Protect Your API Keys

**Never** embed API keys directly in source code that is committed to git:

```javascript
// BAD - key is in source control
const drill = document.querySelector('sat-drill');
drill.setAttribute('api-key', 'pk_live_a1b2c3d4e5f6...');

// GOOD - key comes from environment variable at build time
drill.setAttribute('api-key', import.meta.env.VITE_SDK_API_KEY);
```

Recommended environment variable names by framework:

| Framework | Variable Name | Access |
|---|---|---|
| Vite | `VITE_SDK_API_KEY` | `import.meta.env.VITE_SDK_API_KEY` |
| Create React App | `REACT_APP_DRILL_KEY` | `process.env.REACT_APP_DRILL_KEY` |
| Next.js | `NEXT_PUBLIC_DRILL_KEY` | `process.env.NEXT_PUBLIC_DRILL_KEY` |
| Angular | In `environment.ts` | `environment.drillApiKey` |
| Vanilla JS | Injected at deploy time | Server-side template or config endpoint |

Note: Client-side API keys are visible in network requests by design. Origin validation is the primary protection against key misuse. Never use a live key without configuring `allowed_origins`.

### Content Security Policy (CSP)

If your application uses CSP headers, add the following directives:

```
script-src 'self' https://sdk.learnerlabs.app;
connect-src 'self' https://learnerlabs.app;
```

| Directive | Purpose |
|---|---|
| `script-src https://sdk.learnerlabs.app` | Allows loading the `drill.js` bundle from our CDN |
| `connect-src https://learnerlabs.app` | Allows XHR/fetch calls to the API |

If you self-host `drill.js`, replace `sdk.learnerlabs.app` with your own CDN domain.

### What the SDK Never Exposes

The SDK uses a whitelist-based field mapper (`toSDKQuestion`). API responses physically cannot contain:

| Field | Why It Is Protected |
|---|---|
| `correct_answer` | Prevents answer extraction before submission |
| `irt_a`, `irt_b`, `irt_c` | IRT calibration parameters are proprietary |
| `answer_explanation` | Internal-only field |
| `estimated_time` | Internal timing metadata |

The `explanation` and `hints` fields ARE included in SDK responses to support learning-oriented experiences. Explanations are returned after answer submission (via the `/answer` endpoint). Hints are returned with the question for games that want to offer hint mechanics.

---

## Error Handling

### Error Response Format

All errors follow a consistent envelope:

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Optional human-readable description"
  }
}
```

The `code` field is a stable string constant suitable for programmatic handling. The `message` field is optional and should not be parsed programmatically.

### Error Code Reference

| Code | HTTP Status | Description | Recommended Action |
|---|---|---|---|
| `INVALID_API_KEY` | 401 | API key is missing, malformed, or not found | Check the `x-api-key` header. Verify the key prefix (`pk_test_` or `pk_live_`). |
| `KEY_EXPIRED` | 401 | API key has passed its expiration date | Generate a new API key from the developer portal. |
| `TENANT_SUSPENDED` | 403 | Your tenant account has been suspended | Contact support at developers@learnerlabs.app. |
| `ORIGIN_NOT_ALLOWED` | 403 | Request origin not in `allowed_origins[]` | Add your domain to allowed origins, or use a test key for development. |
| `RATE_LIMITED` | 429 | Too many requests per minute | Wait for `Retry-After` seconds and retry. Consider upgrading to pro tier. |
| `DAILY_LIMIT_EXCEEDED` | 429 | Daily session creation cap reached | Wait until midnight UTC. Consider upgrading to pro tier. |
| `INVALID_INPUT` | 400 | Request body failed validation | Check the `details` array for specific validation errors. |
| `SESSION_NOT_FOUND` | 404 | Session token is invalid or session does not exist | Verify the `x-sdk-session` header. Create a new session if needed. |
| `QUESTION_NOT_FOUND` | 404 | Question ID is not part of the current session | Verify the `questionId` matches a question from the session. |
| `SESSION_EXPIRED` | 410 | Session has timed out (24-hour TTL) or is no longer active | Create a new session. |
| `CONFLICT` | 409 | Session is already completed or abandoned | Do not retry. The session is in a terminal state. |
| `SERVICE_UNAVAILABLE` | 503 | Backend is temporarily unavailable | Retry with exponential backoff. If persistent, contact support. |
| `NETWORK_ERROR` | -- | Client-side: connection lost or DNS failure | Check network connectivity. SDK retries automatically. |
| `UNKNOWN_ERROR` | varies | Unexpected error | Check `message` for details. Report to support if recurring. |

### Graceful Degradation in Games

When integrating the SDK into a game, never let an SDK error block the game flow. Recommended patterns:

```javascript
drill.addEventListener('drill-error', (e) => {
  const { code } = e.detail;

  switch (code) {
    case 'RATE_LIMITED':
      showMessage('Too many questions! Take a short break.');
      givePlayerFreePass();
      break;

    case 'SERVICE_UNAVAILABLE':
    case 'NETWORK_ERROR':
      showMessage('Question unavailable -- free pass!');
      givePlayerFreePass();
      break;

    case 'INVALID_API_KEY':
    case 'KEY_EXPIRED':
      // Disable drills for the rest of the session
      disableDrillChallenges();
      showMessage('Quiz feature temporarily unavailable.');
      break;

    default:
      givePlayerFreePass();
      break;
  }
});
```

The key principle: always provide an alternative path so the player can continue. Never display a raw error message or a blank screen.

### Network Resilience

The SDK (both web component and headless API) retries failed requests automatically:

| Attempt | Delay |
|---|---|
| 1st retry | ~1 second |
| 2nd retry | ~2 seconds |
| 3rd retry | ~4 seconds |

After 3 failed attempts, a `drill-error` event is dispatched with the appropriate error code. The retry mechanism uses exponential backoff with jitter to avoid thundering herd effects.

---

## Performance

### Preconnect Hint

Add a preconnect hint to your HTML `<head>` to speed up the first API call:

```html
<link rel="preconnect" href="https://learnerlabs.app">
```

This initiates DNS resolution and TLS negotiation before the SDK makes its first request, saving ~100-300ms on the initial drill load.

### Bundle Sizes

| Integration Method | Raw Size | Gzipped |
|---|---|---|
| Web component (`drill.js`) | ~45 KB | ~13 KB |
| Headless API (`headless.mjs`) | ~15 KB | ~5 KB |
| Themed headless (`themed.mjs`) | ~55 KB | ~19 KB |

### Instance Limits

A maximum of **5 concurrent `<sat-drill>` instances** per page is recommended. Each instance maintains its own session, timer, and API connection. Beyond 5, you may experience degraded performance.

### Preloading During Loading Screens

For games with loading screens, preload the SDK bundle while other assets load:

```html
<!-- In your <head> -->
<link rel="preload" href="https://sdk.learnerlabs.app/drill.js" as="script">
<link rel="preconnect" href="https://learnerlabs.app">
```

Or load it dynamically during your game's loading phase:

```javascript
// During game loading screen
async function preloadSDK() {
  const script = document.createElement('script');
  script.src = 'https://sdk.learnerlabs.app/drill.js';
  script.async = true;
  document.head.appendChild(script);

  return new Promise((resolve) => {
    script.onload = resolve;
  });
}
```

### SRI (Subresource Integrity)

For production deployments, use SRI hashes to verify the SDK bundle has not been tampered with:

```html
<script
  src="https://sdk.learnerlabs.app/drill.js"
  integrity="sha384-HASH_PROVIDED_PER_VERSION"
  crossorigin="anonymous"
></script>
```

SRI hashes are published per version in the release notes.

---

## Troubleshooting

### Common Issues

#### CORS Errors in Browser Console

**Symptom**: `Access to fetch at 'https://learnerlabs.app/...' has been blocked by CORS policy`

**Cause**: Your domain is not in the API key's `allowed_origins` list.

**Fix**:
- For development: use a `pk_test_*` key (allows any origin)
- For production: contact us to add your domain to `allowed_origins`
- Verify you are using the full origin including protocol: `https://example.com` not just `example.com`

#### 401 Unauthorized

**Symptom**: Every request returns `{ "error": { "code": "INVALID_API_KEY" } }`

**Causes**:
- Missing `x-api-key` header
- Key prefix is wrong (check for `pk_test_` or `pk_live_`)
- Key was revoked or never activated
- Typo in the key (copy-paste the full 72-character key)

**Fix**: Verify the key in a curl command:
```bash
curl -v https://learnerlabs.app/api/sdk/v1/drills/create \
  -H "x-api-key: pk_test_YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{"domain": "Math", "questionCount": 1}'
```

#### Blank Web Component

**Symptom**: `<sat-drill>` renders as an empty element with no visible content.

**Causes**:
- The `drill.js` script failed to load (check the Network tab)
- The `api-key` attribute is missing
- CSP headers are blocking the script or API calls
- The element is hidden by CSS (check `display`, `visibility`, `opacity`)

**Fix**: Check the browser console for errors. Ensure the script tag is present and loaded:
```javascript
// Check if the custom element is registered
console.log(customElements.get('sat-drill'));
// Should log the class constructor, not undefined
```

#### LaTeX Not Rendering (Headless Mode)

**Symptom**: Question text shows raw LaTeX like `\frac{1}{2}` instead of rendered math.

**Cause**: The headless API does not include a LaTeX renderer. You must provide your own.

**Fix**: Check `question.hasLatex` and render accordingly:
```javascript
import katex from 'katex';

function renderText(text, hasLatex) {
  if (!hasLatex) return text;
  // Replace LaTeX delimiters and render
  return text.replace(/\\\((.*?)\\\)/g, (_, tex) => {
    return katex.renderToString(tex, { throwOnError: false });
  });
}
```

#### Session Expired After Navigating Away

**Symptom**: Returning to a drill after navigating away produces `SESSION_EXPIRED`.

**Cause**: Sessions have a 24-hour TTL. If the session was created more than 24 hours ago, it has expired.

**Fix**: Create a new session. Use the `GET /session` endpoint to check session status before resuming:
```javascript
const response = await fetch('/api/sdk/v1/drills/session', {
  headers: {
    'x-api-key': apiKey,
    'x-sdk-session': savedToken,
  },
});

if (response.ok) {
  const data = await response.json();
  if (data.session.status === 'in_progress') {
    // Safe to resume
  } else {
    // Create new session
  }
} else {
  // Create new session
}
```

### Debug Mode

Enable debug logging in the browser console to see detailed SDK activity:

```javascript
localStorage.setItem('SAT_DRILL_DEBUG', 'true');
```

Then reload the page. The SDK will log:
- API request/response details (with sensitive fields redacted)
- State machine transitions
- Timer events
- Error details with stack traces

To disable:
```javascript
localStorage.removeItem('SAT_DRILL_DEBUG');
```

Debug mode is a client-side setting and has no effect on API behavior or rate limits.

---

## Related Documentation

- [SDK README](../../packages/sdk/README.md) -- Quick start and API reference
- [REST API Reference](./REST_API_REFERENCE.md) -- Complete REST endpoint documentation
- [Game Engine Examples](./GAME_ENGINE_EXAMPLES.md) -- Integration patterns for Phaser, Three.js, Unity, and more
- [Snakes & Ladders Guide](./INTEGRATION_GUIDE_SNAKES_AND_LADDERS.md) -- Full worked example
- [Pre-Launch Checklist](./PRE_LAUNCH_CHECKLIST.md) -- Internal launch readiness tracking

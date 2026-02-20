# SAT Drill SDK REST API Reference

**Base URL**: `https://learnerlabs.app/api/sdk/v1/drills`

**Last Updated**: 2026-02-19

Use this reference when calling the API directly without the `<sat-drill>` web component or headless SDK client. All endpoints accept and return JSON.

---

## Table of Contents

1. [Authentication](#authentication)
2. [Endpoints](#endpoints)
   - [POST /create](#post-create)
   - [POST /answer](#post-answer)
   - [POST /complete](#post-complete)
   - [POST /abandon](#post-abandon)
   - [GET /session](#get-session)
3. [Error Responses](#error-responses)
4. [Rate Limiting](#rate-limiting)
5. [Session Lifecycle](#session-lifecycle)

---

## Authentication

All requests require authentication via the `x-api-key` header. Session-scoped requests additionally require the `x-sdk-session` header.

### Required Headers

| Header | Required | Description |
|---|---|---|
| `x-api-key` | All requests | Your API key (`pk_test_*` or `pk_live_*`) |
| `x-sdk-session` | Session requests | Session token returned by `POST /create` |
| `Content-Type` | POST requests | Must be `application/json` |

### Example

```
POST /api/sdk/v1/drills/answer HTTP/1.1
Host: learnerlabs.app
Content-Type: application/json
x-api-key: pk_test_a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef12345678
x-sdk-session: eyJhbGciOiJIUzI1NiJ9...
```

---

## Endpoints

### POST /create

Creates a new drill session. Returns a set of questions and a session token for subsequent requests.

**Authentication**: `x-api-key` only (no session token needed -- this creates the session).

#### Request Body

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `domain` | string | Conditional | -- | SAT domain: `"Math"` or `"Reading and Writing"` |
| `skill` | string | Conditional | -- | Specific skill name (e.g., `"Linear equations in 1 variable"`) |
| `section` | string | Conditional | -- | Section identifier |
| `difficulty` | string | No | `"mixed"` | One of: `"easy"`, `"medium"`, `"hard"`, `"mixed"` |
| `questionCount` | integer | No | `5` | Number of questions (1--20) |
| `timeLimit` | integer | No | -- | Time limit in seconds (60--3600). Omit for untimed. |

At least one of `domain`, `skill`, or `section` is required. If `skill` is provided, it takes precedence over `domain`.

#### Response Body (200 OK)

```json
{
  "sessionId": "uuid-string",
  "sessionToken": "opaque-token-string",
  "questions": [
    {
      "question_id": "q_abc123",
      "question_type": "MCQ",
      "question_text": "If 2x + 5 = 13, what is the value of x?",
      "question_prompt": null,
      "passage_text": null,
      "options": {
        "A": "3",
        "B": "4",
        "C": "5",
        "D": "9"
      },
      "domain": "Math",
      "skill": "Linear equations in 1 variable",
      "difficulty": "easy",
      "has_latex": false,
      "has_image": false,
      "image_url": null,
      "table_data": null,
      "figure_data": null,
      "graph_data": null,
      "hints": ["Try isolating x by subtracting 5 from both sides."],
      "explanation": null
    }
  ],
  "questionCount": 5
}
```

**Response Fields**

| Field | Type | Description |
|---|---|---|
| `sessionId` | string (UUID) | Unique session identifier |
| `sessionToken` | string | Opaque token for the `x-sdk-session` header. Bound to your API key. |
| `questions` | SDKQuestion[] | Array of question objects (see below) |
| `questionCount` | integer | Total number of questions in the session |

**SDKQuestion Object**

| Field | Type | Description |
|---|---|---|
| `question_id` | string | Unique question identifier |
| `question_type` | `"MCQ"` or `"SPR"` | Multiple choice or student-produced response |
| `question_text` | string | The question text. May contain LaTeX notation. |
| `question_prompt` | string or null | Optional prompt text displayed above the question |
| `passage_text` | string or null | Reading passage text (for R&W questions) |
| `options` | object | Map of option keys to option text (e.g., `{"A": "...", "B": "..."}`) . Empty for SPR questions. |
| `domain` | string | SAT domain name |
| `skill` | string | Specific skill name |
| `difficulty` | string | `"easy"`, `"medium"`, or `"hard"` |
| `has_latex` | boolean | `true` if `question_text` contains LaTeX notation |
| `has_image` | boolean | `true` if the question has an accompanying image |
| `image_url` | string or null | URL of the question image (if `has_image` is true) |
| `table_data` | object or null | Structured table data for table-based questions |
| `figure_data` | object or null | Structured figure data |
| `graph_data` | object or null | Structured graph data |
| `hints` | string[] or null | Array of hint texts. You control reveal timing. |
| `explanation` | null | Always `null` in the create response. Returned after answer submission. |

Note: The `correct_answer` field is never included in SDK responses. Correct answers are revealed only after submission via `POST /answer`.

#### curl Example

```bash
curl -X POST https://learnerlabs.app/api/sdk/v1/drills/create \
  -H "x-api-key: pk_test_YOUR_KEY_HERE" \
  -H "Content-Type: application/json" \
  -d '{
    "domain": "Math",
    "difficulty": "medium",
    "questionCount": 3
  }'
```

#### Error Responses

| HTTP Status | Error Code | Cause |
|---|---|---|
| 400 | `INVALID_INPUT` | Validation failed (missing domain/skill/section, bad questionCount, etc.) |
| 401 | `INVALID_API_KEY` | Missing or invalid API key |
| 403 | `ORIGIN_NOT_ALLOWED` | Origin header not in allowed origins (live keys only) |
| 429 | `RATE_LIMITED` | Per-minute request limit exceeded |
| 429 | `DAILY_LIMIT_EXCEEDED` | Daily session creation cap exceeded |
| 503 | `SERVICE_UNAVAILABLE` | Backend temporarily unavailable |

---

### POST /answer

Submit an answer for a question in the current session. Returns whether the answer is correct, the correct answer, and an explanation.

**Authentication**: `x-api-key` + `x-sdk-session`

#### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| `questionId` | string | Yes | The `question_id` from the session's questions array |
| `selectedAnswer` | string | Yes | The selected answer. For MCQ: option key (e.g., `"B"`). For SPR: the numeric/text answer (e.g., `"42"` or `"3/4"`). Max 64 characters. |
| `timeSpentSeconds` | integer | Yes | Time spent on this question in seconds (0--3600) |

#### Response Body (200 OK)

```json
{
  "isCorrect": true,
  "isFirstAttempt": true,
  "correctAnswer": "B",
  "explanation": "Subtract 5 from both sides: 2x = 8. Divide by 2: x = 4."
}
```

**Response Fields**

| Field | Type | Description |
|---|---|---|
| `isCorrect` | boolean | Whether the submitted answer is correct |
| `isFirstAttempt` | boolean | Whether this is the first answer submission for this question |
| `correctAnswer` | string | The correct answer (e.g., `"B"` for MCQ, `"4"` for SPR) |
| `explanation` | string | Detailed explanation of the solution. May contain LaTeX. |

#### curl Example

```bash
curl -X POST https://learnerlabs.app/api/sdk/v1/drills/answer \
  -H "x-api-key: pk_test_YOUR_KEY_HERE" \
  -H "x-sdk-session: SESSION_TOKEN_HERE" \
  -H "Content-Type: application/json" \
  -d '{
    "questionId": "q_abc123",
    "selectedAnswer": "B",
    "timeSpentSeconds": 25
  }'
```

#### Error Responses

| HTTP Status | Error Code | Cause |
|---|---|---|
| 400 | `INVALID_INPUT` | Missing or invalid fields, or `x-sdk-session` header missing |
| 401 | `INVALID_API_KEY` | Missing or invalid API key |
| 404 | `SESSION_NOT_FOUND` | Session token invalid or session does not belong to this API key |
| 404 | `QUESTION_NOT_FOUND` | `questionId` is not part of this session |
| 410 | `SESSION_EXPIRED` | Session has timed out or is no longer active |
| 503 | `SERVICE_UNAVAILABLE` | Backend temporarily unavailable |

---

### POST /complete

Mark the session as completed and retrieve the score summary. Call this after all questions have been answered, or when you want to end the session early with a partial score.

**Authentication**: `x-api-key` + `x-sdk-session`

#### Request Body

Empty object or empty body:

```json
{}
```

#### Response Body (200 OK)

```json
{
  "success": true,
  "scoreSummary": {
    "totalQuestions": 5,
    "correctAnswers": 3,
    "incorrectAnswers": 2,
    "accuracy": 0.6,
    "totalTime": 142,
    "averageTime": 28.4,
    "domainBreakdown": {
      "Algebra": { "correct": 2, "total": 3 },
      "Geometry and Trigonometry": { "correct": 1, "total": 2 }
    }
  }
}
```

**Response Fields**

| Field | Type | Description |
|---|---|---|
| `success` | boolean | Always `true` on success |
| `scoreSummary` | object | Score summary object (see below) |

**ScoreSummary Object**

| Field | Type | Description |
|---|---|---|
| `totalQuestions` | integer | Total questions in the session |
| `correctAnswers` | integer | Number of correct answers |
| `incorrectAnswers` | integer | Number of incorrect answers |
| `accuracy` | number | Decimal accuracy (0.0 -- 1.0) |
| `totalTime` | number | Total time spent in seconds |
| `averageTime` | number | Average time per question in seconds |
| `domainBreakdown` | object | Correct/total counts grouped by domain |

#### curl Example

```bash
curl -X POST https://learnerlabs.app/api/sdk/v1/drills/complete \
  -H "x-api-key: pk_test_YOUR_KEY_HERE" \
  -H "x-sdk-session: SESSION_TOKEN_HERE" \
  -H "Content-Type: application/json" \
  -d '{}'
```

#### Error Responses

| HTTP Status | Error Code | Cause |
|---|---|---|
| 400 | `INVALID_INPUT` | Missing `x-sdk-session` header |
| 401 | `INVALID_API_KEY` | Missing or invalid API key |
| 404 | `SESSION_NOT_FOUND` | Session token invalid or does not belong to this API key |
| 409 | `CONFLICT` | Session is already completed or abandoned |
| 410 | `SESSION_EXPIRED` | Session has timed out |
| 503 | `SERVICE_UNAVAILABLE` | Backend temporarily unavailable |

---

### POST /abandon

Abandon the session. Returns a partial score summary of any questions answered before abandonment.

**Authentication**: `x-api-key` + `x-sdk-session`

#### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| `reason` | string | No | Optional reason for abandonment (max 200 characters) |

The body can be empty or omitted entirely.

#### Response Body (200 OK)

```json
{
  "success": true,
  "scoreSummary": {
    "totalQuestions": 5,
    "correctAnswers": 1,
    "incorrectAnswers": 1,
    "accuracy": 0.5,
    "totalTime": 45,
    "averageTime": 22.5,
    "domainBreakdown": {
      "Algebra": { "correct": 1, "total": 2 }
    }
  }
}
```

The response format is identical to `POST /complete`. The `scoreSummary` reflects only the questions that were answered before abandonment.

#### curl Example

```bash
curl -X POST https://learnerlabs.app/api/sdk/v1/drills/abandon \
  -H "x-api-key: pk_test_YOUR_KEY_HERE" \
  -H "x-sdk-session: SESSION_TOKEN_HERE" \
  -H "Content-Type: application/json" \
  -d '{"reason": "Player quit the game"}'
```

#### Error Responses

| HTTP Status | Error Code | Cause |
|---|---|---|
| 400 | `INVALID_INPUT` | Missing `x-sdk-session` header |
| 401 | `INVALID_API_KEY` | Missing or invalid API key |
| 404 | `SESSION_NOT_FOUND` | Session token invalid or does not belong to this API key |
| 409 | `CONFLICT` | Session is already completed or abandoned |
| 503 | `SERVICE_UNAVAILABLE` | Backend temporarily unavailable |

---

### GET /session

Fetch the current state of a session, including all questions and any answers already submitted. Use this for session resume support (e.g., if the player navigates away and returns).

**Authentication**: `x-api-key` + `x-sdk-session`

This endpoint allows fetching sessions in any status (not just `in_progress`), making it useful for retrieving completed session results.

#### Request

No request body. The session is identified by the `x-sdk-session` header.

#### Response Body (200 OK)

```json
{
  "session": {
    "id": "uuid-string",
    "config": {
      "domain": "Math",
      "difficulty": "mixed",
      "questionCount": 5
    },
    "status": "in_progress",
    "questionCount": 5,
    "scoreSummary": null,
    "createdAt": "2026-02-19T10:30:00.000Z",
    "completedAt": null,
    "expiresAt": "2026-02-20T10:30:00.000Z"
  },
  "questions": [
    {
      "question_id": "q_abc123",
      "question_type": "MCQ",
      "question_text": "If 2x + 5 = 13, what is the value of x?",
      "options": { "A": "3", "B": "4", "C": "5", "D": "9" },
      "domain": "Math",
      "skill": "Linear equations in 1 variable",
      "difficulty": "easy",
      "has_latex": false,
      "has_image": false,
      "question_order": 0,
      "selected_answer": "B",
      "is_correct": true,
      "time_spent_seconds": 25
    },
    {
      "question_id": "q_def456",
      "question_type": "MCQ",
      "question_text": "What is the slope of the line y = 3x - 7?",
      "options": { "A": "-7", "B": "-3", "C": "3", "D": "7" },
      "domain": "Math",
      "skill": "Linear functions",
      "difficulty": "easy",
      "has_latex": false,
      "has_image": false,
      "question_order": 1,
      "selected_answer": null,
      "is_correct": null,
      "time_spent_seconds": null
    }
  ]
}
```

**Session Object**

| Field | Type | Description |
|---|---|---|
| `id` | string (UUID) | Session identifier |
| `config` | object | The configuration used to create the session |
| `status` | string | `"in_progress"`, `"completed"`, or `"abandoned"` |
| `questionCount` | integer | Total questions in the session |
| `scoreSummary` | object or null | Score summary (populated after completion/abandonment) |
| `createdAt` | string (ISO 8601) | When the session was created |
| `completedAt` | string or null | When the session was completed (null if still in progress) |
| `expiresAt` | string (ISO 8601) | When the session expires (24-hour TTL) |

**Questions Array**

Each question includes all the fields from `SDKQuestion` (see [POST /create](#post-create)) plus the following per-question state fields:

| Field | Type | Description |
|---|---|---|
| `question_order` | integer | 0-based position in the session |
| `selected_answer` | string or null | The answer submitted by the user (`null` if unanswered) |
| `is_correct` | boolean or null | Whether the answer was correct (`null` if unanswered) |
| `time_spent_seconds` | integer or null | Time spent on this question (`null` if unanswered) |

#### curl Example

```bash
curl https://learnerlabs.app/api/sdk/v1/drills/session \
  -H "x-api-key: pk_test_YOUR_KEY_HERE" \
  -H "x-sdk-session: SESSION_TOKEN_HERE"
```

#### Error Responses

| HTTP Status | Error Code | Cause |
|---|---|---|
| 400 | `INVALID_INPUT` | Missing `x-sdk-session` header |
| 401 | `INVALID_API_KEY` | Missing or invalid API key |
| 404 | `SESSION_NOT_FOUND` | Session token invalid or does not belong to this API key |
| 503 | `SERVICE_UNAVAILABLE` | Backend temporarily unavailable |

---

## Error Responses

### Standard Error Envelope

All error responses use the same JSON structure:

```json
{
  "error": {
    "code": "ERROR_CODE"
  }
}
```

Some errors include additional fields:

```json
{
  "error": {
    "code": "INVALID_INPUT",
    "message": "Human-readable description",
    "details": [
      {
        "code": "too_small",
        "minimum": 1,
        "type": "number",
        "path": ["questionCount"],
        "message": "Number must be greater than or equal to 1"
      }
    ]
  }
}
```

### Complete Error Code Table

| Code | HTTP Status | Description |
|---|---|---|
| `INVALID_API_KEY` | 401 | API key is missing, malformed, or not found in the database |
| `KEY_EXPIRED` | 401 | API key has passed its expiration date |
| `TENANT_SUSPENDED` | 403 | The tenant account associated with this API key is suspended |
| `ORIGIN_NOT_ALLOWED` | 403 | The request `Origin` header does not match any configured allowed origin |
| `RATE_LIMITED` | 429 | Per-minute request rate limit exceeded |
| `DAILY_LIMIT_EXCEEDED` | 429 | Daily session creation limit exceeded |
| `INVALID_INPUT` | 400 | Request body failed Zod schema validation |
| `SESSION_NOT_FOUND` | 404 | Session token is invalid, expired, or belongs to a different API key |
| `QUESTION_NOT_FOUND` | 404 | The specified `questionId` is not part of the current session |
| `SESSION_EXPIRED` | 410 | Session has exceeded its 24-hour TTL or is no longer in `in_progress` status |
| `CONFLICT` | 409 | Session is already in a terminal state (completed or abandoned) |
| `SERVICE_UNAVAILABLE` | 503 | Backend is temporarily unavailable (database, Redis, or internal error) |

### Error Handling Best Practices

1. **Always check the `code` field**, not the HTTP status code, for programmatic error handling. Multiple error codes may share the same HTTP status (e.g., 429).

2. **Do not parse the `message` field**. It is for human debugging only and may change between API versions.

3. **Retry only on 429 and 503**. All other errors indicate a client-side issue that will not resolve on retry.

4. **Use the `Retry-After` header** on 429 responses to determine when to retry.

---

## Rate Limiting

### Response Headers

Every successful response includes rate limit headers:

```
X-RateLimit-Limit: 30
X-RateLimit-Remaining: 27
X-RateLimit-Reset: 1707840060
```

| Header | Type | Description |
|---|---|---|
| `X-RateLimit-Limit` | integer | Maximum requests allowed per minute for your tier |
| `X-RateLimit-Remaining` | integer | Requests remaining in the current 60-second window |
| `X-RateLimit-Reset` | integer | Unix timestamp (seconds) when the current window resets |

### 429 Response

When rate limited, the response includes:

```
HTTP/1.1 429 Too Many Requests
Retry-After: 12
X-RateLimit-Limit: 30
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1707840060
Content-Type: application/json

{
  "error": {
    "code": "RATE_LIMITED",
    "message": "Rate limit exceeded"
  }
}
```

| Header | Description |
|---|---|
| `Retry-After` | Seconds to wait before retrying |

### Implementation Recommendations

```javascript
async function sdkFetch(url, options, maxRetries = 3) {
  for (let attempt = 0; attempt < maxRetries; attempt++) {
    const response = await fetch(url, options);

    if (response.status === 429) {
      const retryAfter = parseInt(response.headers.get('Retry-After') || '1', 10);
      await new Promise(resolve => setTimeout(resolve, retryAfter * 1000));
      continue;
    }

    if (response.status === 503 && attempt < maxRetries - 1) {
      const delay = Math.pow(2, attempt) * 1000 + Math.random() * 1000;
      await new Promise(resolve => setTimeout(resolve, delay));
      continue;
    }

    return response;
  }

  throw new Error('Max retries exceeded');
}
```

---

## Session Lifecycle

A drill session progresses through a linear state machine:

```
POST /create          POST /answer (repeat)       POST /complete
    |                       |                          |
    v                       v                          v
 [created] ──> [in_progress] ──> [in_progress] ──> [completed]
                    |
                    |  POST /abandon
                    v
               [abandoned]
```

### State Transitions

| Current State | Action | Next State |
|---|---|---|
| (none) | `POST /create` | `in_progress` |
| `in_progress` | `POST /answer` | `in_progress` (no state change) |
| `in_progress` | `POST /complete` | `completed` |
| `in_progress` | `POST /abandon` | `abandoned` |
| `in_progress` | 24-hour TTL expires | `abandoned` (server-side cleanup) |
| `completed` | Any mutation | `409 CONFLICT` |
| `abandoned` | Any mutation | `409 CONFLICT` |

### Important Notes

- **Sessions are anonymous**: No user identity is associated with a session. Sessions are identified solely by the session token.
- **Sessions are bound to API keys**: A session token can only be used with the API key that created it.
- **24-hour TTL**: Sessions expire 24 hours after creation. Expired sessions are automatically transitioned to `abandoned`.
- **One session at a time**: While not enforced by the API, it is recommended to complete or abandon one session before creating another.
- **`GET /session` works in all states**: You can fetch session data regardless of status, making it suitable for retrieving final results.

---

## Related Documentation

- [Developer Guide](./DEVELOPER_GUIDE.md) -- Account setup, security, and troubleshooting
- [Game Engine Examples](./GAME_ENGINE_EXAMPLES.md) -- Integration patterns for Phaser, Three.js, Unity, and more
- [SDK README](../../packages/sdk/README.md) -- Web component and headless API reference

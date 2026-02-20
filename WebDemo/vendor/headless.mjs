class SDKApiError extends Error {
  constructor(status, errorCode, message) {
    super(message || `SDK API error: ${status} ${errorCode}`);
    this.status = status;
    this.errorCode = errorCode;
    this.name = "SDKApiError";
  }
}
class SDKApiClient {
  constructor(apiKey, baseUrl) {
    this.apiKey = apiKey;
    this.baseUrl = baseUrl;
    this.sessionToken = null;
  }
  setSessionToken(token) {
    this.sessionToken = token;
  }
  async request(endpoint, options = {}) {
    const headers = {
      "Content-Type": "application/json",
      "x-api-key": this.apiKey
    };
    if (this.sessionToken) {
      headers["x-sdk-session"] = this.sessionToken;
    }
    const url = `${this.baseUrl}/api/sdk/v1/drills${endpoint}`;
    const response = await fetch(url, {
      ...options,
      headers: { ...headers, ...options.headers }
    });
    if (!response.ok) {
      let errorCode = "UNKNOWN_ERROR";
      try {
        const body = await response.json();
        errorCode = body?.error?.code || errorCode;
      } catch {
      }
      throw new SDKApiError(response.status, errorCode);
    }
    return response.json();
  }
  async createDrill(config) {
    return this.request("/create", {
      method: "POST",
      body: JSON.stringify(config)
    });
  }
  async submitAnswer(questionId, selectedAnswer, timeSpentSeconds) {
    return this.request("/answer", {
      method: "POST",
      body: JSON.stringify({
        questionId,
        selectedAnswer,
        timeSpentSeconds
      })
    });
  }
  async completeDrill() {
    return this.request("/complete", {
      method: "POST",
      body: JSON.stringify({})
    });
  }
  async abandonDrill() {
    return this.request("/abandon", {
      method: "POST",
      body: JSON.stringify({})
    });
  }
}
const TRANSITIONS = {
  idle: ["loading"],
  loading: ["active", "error"],
  active: ["completing", "abandoned", "error"],
  completing: ["completed", "error"],
  completed: ["idle"],
  // "Try Again" resets to idle
  abandoned: ["idle"],
  // "Try Again" resets to idle
  error: ["idle"]
  // "Try Again" resets to idle
};
function transition(from, to) {
  if (TRANSITIONS[from]?.includes(to)) {
    return to;
  }
  if (to === "idle") return to;
  console.warn(`[sat-drill] Invalid state transition: ${from} -> ${to}`);
  return null;
}
class DrillController {
  constructor(config, callbacks) {
    this.config = config;
    this.callbacks = callbacks;
    this.drillState = "idle";
    this.questions = [];
    this.currentQuestionIndex = 0;
    this.selectedAnswer = null;
    this.answerResult = null;
    this.showingFeedback = false;
    this.completionResult = null;
    this.errorMessage = "";
    this.errorCode = "";
    this.timeRemaining = null;
    this.questionStartTime = 0;
    this.submitting = false;
    this.questionResults = [];
    this.hintsRevealed = 0;
    this.api = null;
    this.sessionId = "";
    this.timerInterval = null;
    this.autoAdvanceTimer = null;
    this.configLocked = false;
  }
  // ── Action dispatch table (for spec action bindings) ──────
  handleAction(action, params) {
    switch (action) {
      case "selectAnswer":
        if (!this.showingFeedback) {
          this.selectedAnswer = params?.answer != null ? String(params.answer) : null;
          this.callbacks.onStateChange();
        }
        break;
      case "submitAnswer":
        this.submitAnswer();
        break;
      case "tryAgain":
        this.tryAgain();
        break;
      case "revealHint":
        this.revealHint();
        break;
      case "abandon":
        this.abandonDrill(String(params?.reason ?? "user_exit"));
        break;
      case "close":
        this.callbacks.emitEvent("drill-close", {});
        break;
      default:
        console.warn(`[sat-drill] Unknown action: ${action}`);
    }
  }
  // ── Core drill flow ─────────────────────────────────────
  async startDrill() {
    if (!this.config.apiKey) {
      this.handleError("MISSING_API_KEY", "API key required. Add api-key attribute.");
      return;
    }
    this.transitionTo("loading");
    this.configLocked = true;
    this.api = new SDKApiClient(this.config.apiKey, this.config.apiUrl);
    try {
      const result = await this.api.createDrill({
        domain: this.config.domain,
        skill: this.config.skill,
        difficulty: this.config.difficulty,
        questionCount: this.config.questionCount,
        timeLimit: this.config.timeLimit
      });
      this.sessionId = result.sessionId;
      this.api.setSessionToken(result.sessionToken);
      this.questions = result.questions;
      this.currentQuestionIndex = 0;
      this.selectedAnswer = null;
      this.answerResult = null;
      this.showingFeedback = false;
      this.questionResults = [];
      this.questionStartTime = Date.now();
      this.transitionTo("active");
      if (this.config.timeLimit && this.config.showTimer) {
        this.startTimer(this.config.timeLimit);
      }
      this.callbacks.emitEvent("drill-started", {
        sessionId: this.sessionId,
        questionCount: this.questions.length,
        domain: this.config.domain,
        skill: this.config.skill
      });
    } catch (error) {
      this.handleApiError(error);
    }
  }
  async submitAnswer() {
    if (!this.api || this.selectedAnswer === null || this.submitting) return;
    this.submitting = true;
    this.callbacks.onStateChange();
    const question = this.questions[this.currentQuestionIndex];
    const timeSpent = Math.round((Date.now() - this.questionStartTime) / 1e3);
    try {
      const result = await this.fetchWithRetry(
        () => this.api.submitAnswer(question.question_id, this.selectedAnswer, timeSpent)
      );
      this.answerResult = result;
      this.showingFeedback = true;
      this.questionResults.push({
        isCorrect: result.isCorrect,
        timeSpent,
        skill: question.skill
      });
      this.callbacks.onStateChange();
      this.callbacks.emitEvent("question-answered", {
        questionIndex: this.currentQuestionIndex,
        isCorrect: result.isCorrect,
        timeSpent
      });
      if (this.config.autoAdvance !== false) {
        await new Promise((r) => {
          this.autoAdvanceTimer = setTimeout(() => {
            this.autoAdvanceTimer = null;
            r();
          }, 1500);
        });
        if (this.drillState === "active") {
          this.nextQuestion();
        }
      }
    } catch (error) {
      this.handleApiError(error);
    } finally {
      this.submitting = false;
      this.callbacks.onStateChange();
    }
  }
  /**
   * Select an answer and submit it in one step.
   * Used by the headless API to bridge the two-step select→submit internal pattern.
   * This is the headless equivalent of handleAction('selectAnswer') + handleAction('submitAnswer').
   * If middleware is added to handleAction, mirror it here.
   */
  selectAndSubmit(answer) {
    if (this.showingFeedback || this.submitting) return;
    this.selectedAnswer = answer;
    this.callbacks.onStateChange();
    this.submitAnswer();
  }
  /** Advance to the next question. Public for headless mode where autoAdvance is false. */
  nextQuestion() {
    if (this.currentQuestionIndex + 1 >= this.questions.length) {
      this.completeDrill();
      return;
    }
    this.currentQuestionIndex++;
    this.selectedAnswer = null;
    this.answerResult = null;
    this.showingFeedback = false;
    this.hintsRevealed = 0;
    this.questionStartTime = Date.now();
    this.callbacks.onStateChange();
  }
  async completeDrill(timedOut = false) {
    if (!this.api || this.drillState !== "active") return;
    this.transitionTo("completing");
    this.stopTimer();
    try {
      const result = await this.fetchWithRetry(() => this.api.completeDrill());
      this.completionResult = result;
      this.transitionTo("completed");
      this.callbacks.emitEvent("drill-completed", {
        score: result.scoreSummary?.accuracy ?? 0,
        accuracy: result.scoreSummary?.accuracy ?? 0,
        timeSpent: result.scoreSummary?.totalTimeSpent ?? 0,
        timedOut,
        results: this.questionResults
      });
    } catch (error) {
      this.handleApiError(error);
    }
  }
  async abandonDrill(reason = "user_exit") {
    if (!this.api) return;
    this.stopTimer();
    this.transitionTo("abandoned");
    try {
      await this.api.abandonDrill();
    } catch {
    }
    this.callbacks.emitEvent("drill-abandoned", {
      questionsAnswered: this.currentQuestionIndex,
      questionsTotal: this.questions.length,
      reason
    });
  }
  revealHint() {
    const q = this.currentQuestion;
    if (!q?.hints || this.hintsRevealed >= q.hints.length) return;
    this.hintsRevealed++;
    this.callbacks.onStateChange();
  }
  tryAgain() {
    this.drillState = "idle";
    this.questions = [];
    this.currentQuestionIndex = 0;
    this.selectedAnswer = null;
    this.answerResult = null;
    this.showingFeedback = false;
    this.hintsRevealed = 0;
    this.completionResult = null;
    this.errorMessage = "";
    this.errorCode = "";
    this.timeRemaining = null;
    this.configLocked = false;
    this.callbacks.onStateChange();
    this.startDrill();
  }
  // ── Timer ───────────────────────────────────────────────
  startTimer(seconds) {
    this.timeRemaining = seconds;
    this.timerInterval = setInterval(() => {
      if (this.timeRemaining !== null && this.timeRemaining > 0) {
        this.timeRemaining--;
        this.callbacks.onStateChange();
      } else {
        this.stopTimer();
        this.completeDrill(true);
      }
    }, 1e3);
  }
  stopTimer() {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
    if (this.autoAdvanceTimer) {
      clearTimeout(this.autoAdvanceTimer);
      this.autoAdvanceTimer = null;
    }
  }
  // ── Keyboard handling ─────────────────────────────────
  handleKeydown(e) {
    if (this.drillState !== "active" || this.showingFeedback) return;
    const question = this.questions[this.currentQuestionIndex];
    if (!question) return;
    if (question.question_type === "MCQ") {
      const optionKeys = Object.keys(question.options);
      const keyMap = {
        "1": 0,
        "2": 1,
        "3": 2,
        "4": 3,
        "a": 0,
        "b": 1,
        "c": 2,
        "d": 3,
        "A": 0,
        "B": 1,
        "C": 2,
        "D": 3
      };
      const index = keyMap[e.key];
      if (index !== void 0 && index < optionKeys.length) {
        this.selectedAnswer = optionKeys[index];
        this.callbacks.onStateChange();
      }
    }
    if (e.key === "Enter" && this.selectedAnswer !== null) {
      e.preventDefault();
      this.submitAnswer();
    }
    if (e.key === "Escape") {
      this.abandonDrill("user_exit");
    }
  }
  // ── Error handling ──────────────────────────────────────
  handleError(code, message) {
    this.errorCode = code;
    this.errorMessage = message;
    this.stopTimer();
    this.transitionTo("error");
    this.callbacks.emitEvent("drill-error", { code, message });
  }
  handleApiError(error) {
    if (error instanceof SDKApiError) {
      const messages = {
        INVALID_API_KEY: "Invalid API key. Check your key and try again.",
        KEY_EXPIRED: "API key expired. Generate a new key.",
        TENANT_SUSPENDED: "Account suspended. Contact support.",
        INVALID_INPUT: "No questions match your criteria. Try broader settings.",
        RATE_LIMITED: "Too many requests. Please wait and try again.",
        SESSION_NOT_FOUND: "Session not found.",
        QUESTION_NOT_FOUND: "Question not found in this session.",
        SESSION_EXPIRED: "Session expired.",
        SERVICE_UNAVAILABLE: "Service temporarily unavailable. Please try again later."
      };
      const code = error.errorCode || "UNKNOWN_ERROR";
      this.handleError(code, messages[code] || error.message || "An error occurred.");
    } else {
      this.handleError("NETWORK_ERROR", "Connection lost. Check your internet and try again.");
    }
  }
  async fetchWithRetry(fn, maxRetries = 3) {
    for (let i = 0; i < maxRetries; i++) {
      try {
        return await fn();
      } catch (error) {
        if (error instanceof SDKApiError && error.status >= 400 && error.status < 500) {
          throw error;
        }
        if (i === maxRetries - 1) throw error;
        await new Promise((r) => setTimeout(r, 1e3 * Math.pow(2, i)));
      }
    }
    throw new Error("Max retries exceeded");
  }
  // ── State machine ───────────────────────────────────────
  transitionTo(newState) {
    const allowed = transition(this.drillState, newState);
    if (allowed) {
      this.drillState = newState;
      this.callbacks.onStateChange();
    }
  }
  // ── Utilities ───────────────────────────────────────────
  get currentQuestion() {
    return this.questions[this.currentQuestionIndex];
  }
  formatTime(seconds) {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s.toString().padStart(2, "0")}`;
  }
}
function toHeadlessQuestion(ctrl) {
  const q = ctrl.currentQuestion;
  if (!q) return null;
  return {
    index: ctrl.currentQuestionIndex,
    total: ctrl.questions.length,
    type: q.question_type === "MCQ" ? "mcq" : "spr",
    text: q.question_text,
    options: Object.entries(q.options).map(([key, text]) => ({ key, text })),
    imageUrl: q.image_url ?? null,
    passageText: q.passage_text ?? null,
    difficulty: q.difficulty,
    domain: q.domain,
    skill: q.skill,
    hints: q.hints ?? [],
    hasLatex: q.has_latex
  };
}
function toHeadlessFeedback(ctrl) {
  const result = ctrl.answerResult;
  if (!result) return null;
  const q = ctrl.currentQuestion;
  return {
    isCorrect: result.isCorrect,
    correctAnswer: result.correctAnswer ?? "",
    explanation: result.explanation ?? q?.explanation ?? "",
    questionIndex: ctrl.currentQuestionIndex,
    timeSpent: Math.round((Date.now() - ctrl.questionStartTime) / 1e3)
  };
}
function toHeadlessResults(ctrl, timedOut) {
  const summary = ctrl.completionResult?.scoreSummary;
  const results = ctrl.questionResults;
  const correct = results.filter((r) => r.isCorrect).length;
  const total = results.length;
  return {
    score: total > 0 ? Math.round(correct / total * 100) : 0,
    accuracy: summary?.accuracy ?? (total > 0 ? correct / total : 0),
    totalTime: summary?.totalTimeSpent ?? results.reduce((acc, r) => acc + r.timeSpent, 0),
    timedOut,
    questions: results.map((r, i) => ({
      isCorrect: r.isCorrect,
      timeSpent: r.timeSpent,
      difficulty: ctrl.questions[i]?.difficulty ?? "medium"
    }))
  };
}
const deadHandle = {
  submitAnswer: () => false,
  advance: () => {
  },
  abandon: async () => {
  },
  destroy: () => {
  },
  get state() {
    return "error";
  },
  get timeRemaining() {
    return null;
  }
};
function startDrill(config) {
  if (!config.onQuestion || !config.onError) {
    throw new Error("startDrill requires onQuestion and onError callbacks");
  }
  if (!config.apiKey) {
    config.onError({ code: "MISSING_API_KEY", message: "apiKey is required" });
    return deadHandle;
  }
  if (!config.apiUrl) {
    config.onError({ code: "MISSING_API_URL", message: "apiUrl is required (no auto-detection in headless mode)" });
    return deadHandle;
  }
  let destroyed = false;
  let lastQuestionIndex = -1;
  let feedbackDelivered = false;
  let completedTimedOut = false;
  let callbacks = {
    onQuestion: config.onQuestion,
    onError: config.onError,
    onLoading: config.onLoading,
    onFeedback: config.onFeedback,
    onComplete: config.onComplete,
    onAbandoned: config.onAbandoned
  };
  const ctrl = new DrillController(
    {
      apiKey: config.apiKey,
      apiUrl: config.apiUrl,
      domain: config.domain,
      skill: config.skill,
      difficulty: config.difficulty ?? "mixed",
      questionCount: config.questionCount ?? 5,
      timeLimit: config.timeLimit,
      showTimer: !!config.timeLimit,
      autoAdvance: false
    },
    {
      onStateChange: () => {
        if (destroyed) return;
        if (ctrl.drillState === "loading") {
          callbacks.onLoading?.();
        }
        if (ctrl.drillState === "active" && !ctrl.showingFeedback && ctrl.currentQuestionIndex !== lastQuestionIndex) {
          lastQuestionIndex = ctrl.currentQuestionIndex;
          feedbackDelivered = false;
          const question = toHeadlessQuestion(ctrl);
          if (question) callbacks.onQuestion(question);
        }
        if (ctrl.drillState === "active" && ctrl.showingFeedback && !feedbackDelivered) {
          feedbackDelivered = true;
          const feedback = toHeadlessFeedback(ctrl);
          if (feedback) callbacks.onFeedback?.(feedback);
        }
        if (ctrl.drillState === "completed") {
          callbacks.onComplete?.(toHeadlessResults(ctrl, completedTimedOut));
        }
      },
      emitEvent: (name, detail) => {
        if (destroyed) return;
        if (name === "drill-error") {
          callbacks.onError({
            code: String(detail.code ?? "UNKNOWN_ERROR"),
            message: String(detail.message ?? "An error occurred")
          });
        }
        if (name === "drill-abandoned") {
          callbacks.onAbandoned?.({
            questionsAnswered: Number(detail.questionsAnswered ?? 0),
            questionsTotal: Number(detail.questionsTotal ?? 0),
            reason: String(detail.reason ?? "unknown")
          });
        }
        if (name === "drill-completed") {
          completedTimedOut = !!detail.timedOut;
        }
      }
    }
  );
  callbacks.onLoading?.();
  ctrl.startDrill();
  return {
    submitAnswer(answer) {
      if (destroyed) return false;
      if (ctrl.drillState !== "active" || ctrl.showingFeedback || ctrl.submitting) return false;
      ctrl.selectAndSubmit(answer);
      return true;
    },
    advance() {
      if (destroyed) return;
      if (!ctrl.showingFeedback) return;
      ctrl.nextQuestion();
    },
    async abandon(reason) {
      if (destroyed) return;
      await ctrl.abandonDrill((reason ?? "user_exit").slice(0, 200));
    },
    destroy() {
      if (destroyed) return;
      destroyed = true;
      ctrl.stopTimer();
      if (ctrl.drillState === "active" || ctrl.drillState === "loading") {
        ctrl.abandonDrill("destroyed").catch(() => {
        });
      }
      callbacks = {
        onQuestion: () => {
        },
        onError: () => {
        },
        onLoading: void 0,
        onFeedback: void 0,
        onComplete: void 0,
        onAbandoned: void 0
      };
    },
    get state() {
      return ctrl.drillState;
    },
    get timeRemaining() {
      return ctrl.timeRemaining;
    }
  };
}
export {
  startDrill
};

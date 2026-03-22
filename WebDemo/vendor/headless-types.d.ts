/**
 * Headless SDK Types — TypeScript interfaces for the callback-based drill API.
 *
 * Used by game developers and EdTech integrators who render their own UI.
 */
import type { DrillState } from './state-machine.js';
export type { DrillState };
export interface HeadlessConfig {
    /** API key for authentication (required). */
    apiKey: string;
    /** Base URL of the SAT ACE API (required — no auto-detection in headless mode). */
    apiUrl: string;
    /** Target domain: 'Math' or 'Reading and Writing'. */
    domain?: string;
    /** Specific skill name to drill. */
    skill?: string;
    /** Difficulty level. Defaults to 'mixed'. */
    difficulty?: 'easy' | 'medium' | 'hard' | 'mixed';
    /** Number of questions (1-20). Defaults to 5. */
    questionCount?: number;
    /** Time limit in seconds. Omit for untimed drills. */
    timeLimit?: number;
    /** Called when a new question is ready to display. */
    onQuestion: (question: HeadlessQuestion) => void;
    /** Called when an error occurs. */
    onError: (error: HeadlessError) => void;
    /** Called when the drill session is being created (show a loading indicator). */
    onLoading?: () => void;
    /** Called after an answer is submitted with correctness and explanation. */
    onFeedback?: (feedback: HeadlessFeedback) => void;
    /** Called when all questions are answered or time runs out. */
    onComplete?: (results: HeadlessResults) => void;
    /** Called when the drill is abandoned. */
    onAbandoned?: (summary: HeadlessAbandonSummary) => void;
}
export interface DrillHandle {
    /**
     * Submit an answer for the current question.
     * Returns false if the drill is not in active state or feedback is showing.
     */
    submitAnswer(answer: string): boolean;
    /**
     * Advance to the next question after viewing feedback.
     * No-op if feedback is not currently showing.
     */
    advance(): void;
    /**
     * Abandon the drill. The onAbandoned callback will fire.
     * @param reason Optional reason string (max 200 chars).
     */
    abandon(reason?: string): Promise<void>;
    /**
     * Clean up: stop timers, abandon the session, and nullify callbacks.
     * Call this on component unmount, page navigation, or level exit.
     */
    destroy(): void;
    /** Current state of the drill state machine. */
    readonly state: DrillState;
    /** Time remaining in seconds, or null for untimed drills. Read in your game loop. */
    readonly timeRemaining: number | null;
}
export interface HeadlessQuestion {
    /** 0-based question index. */
    index: number;
    /** Total questions in this session. */
    total: number;
    /** Question type: multiple choice or student-produced response. */
    type: 'mcq' | 'spr';
    /** Raw question text (may contain LaTeX — use question.hasLatex to check). */
    text: string;
    /** Answer options. Empty array for SPR questions. */
    options: Array<{
        key: string;
        text: string;
    }>;
    /** Image URL for the question, or null. */
    imageUrl: string | null;
    /** Reading passage text, or null. */
    passageText: string | null;
    /** Question difficulty. */
    difficulty: 'easy' | 'medium' | 'hard';
    /** Domain (e.g., 'Math', 'Reading and Writing'). */
    domain: string;
    /** Skill being tested. */
    skill: string;
    /** All available hints. Developer manages revelation timing. */
    hints: string[];
    /** True if the question text contains LaTeX math notation. */
    hasLatex: boolean;
}
export interface HeadlessFeedback {
    /** Whether the submitted answer was correct. */
    isCorrect: boolean;
    /** The correct answer (e.g., 'C' or '42'). */
    correctAnswer: string;
    /** Explanation text (raw, may contain LaTeX). */
    explanation: string;
    /** Which question this feedback is for (0-based). */
    questionIndex: number;
    /** Time spent on this question in seconds. */
    timeSpent: number;
}
export interface HeadlessResults {
    /** Score as a percentage (0-100), derived from accuracy. */
    score: number;
    /** Accuracy as a decimal (0.0-1.0). */
    accuracy: number;
    /** Total time spent in seconds. */
    totalTime: number;
    /** True if the drill ended because time ran out. */
    timedOut: boolean;
    /** Per-question results (array index matches question index). */
    questions: Array<{
        isCorrect: boolean;
        timeSpent: number;
        difficulty: 'easy' | 'medium' | 'hard';
    }>;
}
export interface HeadlessError {
    /** Error code (e.g., INVALID_API_KEY, RATE_LIMITED, NETWORK_ERROR). */
    code: string;
    /** Human-readable error message. */
    message: string;
}
export interface HeadlessAbandonSummary {
    /** Number of questions answered before abandoning. */
    questionsAnswered: number;
    /** Total questions in the session. */
    questionsTotal: number;
    /** Reason for abandoning (e.g., 'user_exit', 'level_exit', 'destroyed'). */
    reason: string;
}
//# sourceMappingURL=headless-types.d.ts.map
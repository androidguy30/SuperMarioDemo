/**
 * SDK Types — mirrors SDKQuestionDTO from the backend.
 * These types describe the API response shapes, NOT the database schema.
 */
/** Question payload from the SDK API (no sensitive fields). */
export interface SDKQuestion {
    question_id: string;
    question_type: 'MCQ' | 'SPR';
    question_text: string;
    question_prompt?: string;
    passage_text?: string;
    options: Record<string, string>;
    domain: string;
    skill: string;
    difficulty: string;
    has_latex: boolean;
    has_image: boolean;
    image_url?: string;
    table_data?: unknown;
    figure_data?: unknown;
    graph_data?: unknown;
    hints?: string[];
    explanation?: string;
}
/** Response from POST /api/sdk/v1/drills/create */
export interface CreateDrillResponse {
    sessionId: string;
    sessionToken: string;
    questions: SDKQuestion[];
    questionCount: number;
}
/** Request body for POST /api/sdk/v1/drills/create */
export interface CreateDrillConfig {
    domain?: string;
    skill?: string;
    difficulty?: string;
    questionCount?: number;
    timeLimit?: number;
}
/** Response from POST /api/sdk/v1/drills/answer */
export interface AnswerResponse {
    isCorrect: boolean;
    isFirstAttempt: boolean;
    correctAnswer?: string;
    explanation?: string;
}
/** Score summary from completion/abandon response. */
export interface ScoreSummary {
    totalQuestions: number;
    correctAnswers: number;
    incorrectAnswers: number;
    skippedQuestions: number;
    accuracy: number;
    avgTimePerQuestion: number;
    totalTimeSpent: number;
    questionsMarkedForReview: number;
}
/** Response from POST /api/sdk/v1/drills/complete */
export interface CompleteResponse {
    success: boolean;
    scoreSummary: ScoreSummary;
}
/** Response from POST /api/sdk/v1/drills/abandon */
export interface AbandonResponse {
    success: boolean;
    scoreSummary: ScoreSummary;
}
//# sourceMappingURL=types.d.ts.map
import http.server
import socketserver
import json
import uuid

PORT = 8081

class MockLearnerLabsHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'x-api-key, x-sdk-session, Content-Type, Authorization, Origin')
        super().end_headers()

    def do_OPTIONS(self):
        self.send_response(200)
        self.end_headers()

    def do_POST(self):
        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length) if content_length > 0 else b'{}'
        
        try:
            req_data = json.loads(post_data.decode('utf-8'))
        except:
            req_data = {}

        if self.path == '/api/sdk/v1/drills/create':
            # Mock /create
            response = {
                "sessionId": str(uuid.uuid4()),
                "sessionToken": "mock-token",
                "questions": [
                    {
                        "question_id": "q1",
                        "question_type": "MCQ",
                        "question_text": "If Mario has 3 coins and collects 5 more, how many coins does he have?",
                        "options": { "A": "5", "B": "8", "C": "10", "D": "15" },
                        "domain": "Math",
                        "skill": "Arithmetic",
                        "difficulty": "easy",
                        "has_latex": False,
                        "hints": ["3 + 5 = ?"]
                    }
                ],
                "questionCount": 1
            }
        elif self.path == '/api/sdk/v1/drills/answer':
            # Mock /answer
            is_correct = req_data.get("selectedAnswer") == "B"
            response = {
                "isCorrect": is_correct,
                "isFirstAttempt": True,
                "correctAnswer": "B",
                "explanation": "3 + 5 = 8"
            }
        elif self.path in ['/api/sdk/v1/drills/complete', '/api/sdk/v1/drills/abandon']:
            # Mock /complete or /abandon
            response = {
                "success": True,
                "scoreSummary": {
                    "totalQuestions": 1,
                    "correctAnswers": 1,
                    "incorrectAnswers": 0,
                    "accuracy": 1.0,
                    "totalTime": 10,
                    "averageTime": 10,
                    "domainBreakdown": {"Math": {"correct": 1, "total": 1}}
                }
            }
        else:
            self.send_response(404)
            self.end_headers()
            self.wfile.write(b'{"error":{"code":"NOT_FOUND"}}')
            return

        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(response).encode('utf-8'))

with socketserver.TCPServer(("", PORT), MockLearnerLabsHandler) as httpd:
    print(f"Mock LearnerLabs API running at http://localhost:{PORT}")
    httpd.serve_forever()

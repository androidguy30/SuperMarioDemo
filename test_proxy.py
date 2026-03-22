import urllib.request
import json

req = urllib.request.Request(
    "http://localhost:8081/api/sdk/v1/drills/create", 
    data=json.dumps({"domain": "Math", "difficulty": "medium", "questionCount": 3}).encode(),
    method="POST",
    headers={
        "Content-Type": "application/json",
        "x-api-key": "pk_test_785a8927e5b41afd12891fc73cb072fb7b6ecf00f9f512c3f4867fecdfcce968",
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    }
)

try:
    with urllib.request.urlopen(req) as response:
        print(response.read().decode())
except Exception as e:
    print(getattr(e, 'read', lambda: str(e))())

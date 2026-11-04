"""
Question server for the game's Question Generator screen (Groq AI).

Unity sends the pasted notes to POST /generate; this asks the model for multiple-choice questions
and answers with {"questions": [{"prompt", "correctAnswer", "wrongAnswers": [3], "difficulty"}]}.
Setup and running: see README.md next to this file.
"""

import json
import os
import sys

sys.dont_write_bytecode = True  # this folder is inside Unity's Assets: keep __pycache__ out of it

from dotenv import load_dotenv
from flask import Flask, jsonify, request
from groq import Groq

from question_format import clean_questions

load_dotenv(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".env"))

api_key = os.getenv("GROQ_API_KEY")
if not api_key:
    raise SystemExit("GROQ_API_KEY not found. Copy .env.example to .env and paste your Groq key into it.")

MODEL = os.getenv("GROQ_MODEL", "openai/gpt-oss-20b")
HOST = os.getenv("HOST", "127.0.0.1")  # 0.0.0.0 lets other computers on the network use it
PORT = int(os.getenv("PORT", "5000"))
MAX_NOTES = 12000  # characters of notes sent to the model
MAX_QUESTIONS = 20

client = Groq(api_key=api_key)
app = Flask(__name__)

SYSTEM_PROMPT = (
    "You write multiple-choice study questions from a student's notes. "
    "Only use facts that are in the notes. "
    "Reply with a JSON object and nothing else, shaped exactly like this: "
    '{"questions": [{"prompt": "...", "correctAnswer": "...", '
    '"wrongAnswers": ["...", "...", "..."], "difficulty": "Easy"}]}. '
    "Each question has exactly one correct answer and exactly three wrong answers that sound "
    "believable but are clearly wrong. Keep every answer under 60 characters. "
    "difficulty is Easy, Medium or Hard."
)


@app.route("/health", methods=["GET"])
def health():
    return jsonify({"ok": True, "model": MODEL})


@app.route("/generate", methods=["POST"])
def generate_questions():
    data = request.get_json(silent=True) or {}
    notes = str(data.get("notes", "")).strip()
    if not notes:
        return jsonify({"error": "no notes were sent"}), 400
    try:
        count = int(data.get("count", 10))
    except (TypeError, ValueError):
        count = 10
    count = max(1, min(count, MAX_QUESTIONS))

    try:
        response = client.chat.completions.create(
            model=MODEL,
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": f"Write {count} questions from these notes:\n\n{notes[:MAX_NOTES]}"},
            ],
            response_format={"type": "json_object"},
        )
        raw = json.loads(response.choices[0].message.content or "{}")
    except Exception as e:  # network trouble, a bad key, rate limits, or JSON the model mangled
        app.logger.exception("Groq request failed")
        return jsonify({"error": f"the AI request failed: {e}"}), 502

    questions = clean_questions(raw, count)
    if not questions:
        return jsonify({"error": "the AI didn't send back any usable questions"}), 502
    return jsonify({"questions": questions})


if __name__ == "__main__":
    print(f"Question server running on http://{HOST}:{PORT} (model: {MODEL})")
    app.run(host=HOST, port=PORT, debug=False)

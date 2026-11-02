


import os
import json

from flask import Flask, request, jsonify
from groq import Groq
from dotenv import load_dotenv

load_dotenv(os.path.join(os.path.dirname(__file__), ".env"))

api_key = os.getenv("GROQ_API_KEY")

if not api_key:
    raise ValueError("Groq API key not found. Check your .env file.")

client = Groq(api_key=api_key)

app = Flask(__name__)


@app.route("/generate", methods=["POST"])
def generate_questions():
    data = request.get_json()

    notes = data.get("notes", "")

    if not notes.strip():
        return jsonify({"error": "No notes provided"}), 400

    response = client.chat.completions.create(
        model="openai/gpt-oss-20b",

        messages=[
            {
                "role": "system",
                "content": (
                    "You are a study question generator. "
                    "Create exactly 5 questions from the notes. "
                    "Return a JSON object with a questions array. "
                    "Each question must have prompt, correctAnswer, 3 other incorrect answers,"
                    "and difficulty fields. "
                    "Difficulty must be Easy, Medium, or Hard."
                )
            },
            {
                "role": "user",
                "content": "Generate questions in JSON format using these notes:\n" + notes
            }
        ],

        response_format={
            "type": "json_object"
        }
    )

    result = json.loads(
        response.choices[0].message.content
    )

    return jsonify(result)


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5000, debug=True)

"""Checks and tidies the questions the model sends back (standard library only, so it's easy to test)."""

DIFFICULTIES = ("Easy", "Medium", "Hard")


def _text(value):
    """A string with its whitespace tidied, or "" for anything that isn't text or a number."""
    if isinstance(value, bool) or not isinstance(value, (str, int, float)):
        return ""
    return " ".join(str(value).split())


def clean_questions(raw, limit=20):
    """
    Keeps the questions that have a prompt, a correct answer and three different wrong answers (none
    of them the correct one), shaped the way the game reads them:
        {"prompt", "correctAnswer", "wrongAnswers": [3], "difficulty": "Easy" | "Medium" | "Hard"}
    Also accepts a few other spellings the model sometimes uses ("question", "incorrectAnswers"...).
    """
    if isinstance(raw, dict):
        items = raw.get("questions", [])
    elif isinstance(raw, list):
        items = raw
    else:
        items = []

    cleaned = []
    for item in items if isinstance(items, list) else []:
        if not isinstance(item, dict):
            continue
        prompt = _text(item.get("prompt") or item.get("question"))
        correct = _text(item.get("correctAnswer") or item.get("correct_answer") or item.get("answer"))
        wrong_raw = (item.get("wrongAnswers") or item.get("incorrectAnswers")
                     or item.get("wrong_answers") or item.get("incorrect_answers") or [])
        if not prompt or not correct or not isinstance(wrong_raw, list):
            continue

        wrong = []
        for answer in wrong_raw:
            answer = _text(answer)
            if answer and answer.lower() != correct.lower() and answer.lower() not in (w.lower() for w in wrong):
                wrong.append(answer)
        if len(wrong) < 3:
            continue

        difficulty = _text(item.get("difficulty")).capitalize()
        cleaned.append({
            "prompt": prompt,
            "correctAnswer": correct,
            "wrongAnswers": wrong[:3],
            "difficulty": difficulty if difficulty in DIFFICULTIES else "Medium",
        })
        if len(cleaned) >= limit:
            break
    return cleaned

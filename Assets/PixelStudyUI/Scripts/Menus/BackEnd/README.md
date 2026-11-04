# Question server (Groq AI)

The game's **Question Generator** screen sends your pasted notes to this server, which asks Groq's
AI (model `openai/gpt-oss-20b`) for multiple-choice questions: a prompt, the correct answer, three
wrong answers and a difficulty. The game saves them as the active question set, and the quiz after
each floor asks them.

## Set up (once)

1. Install Python 3.10 or newer.
2. In this folder: `pip install -r requirements.txt`
3. Copy `.env.example` to `.env` and paste your Groq API key (https://console.groq.com/keys) after
   `GROQ_API_KEY=`. **Never commit `.env`**: the `.gitignore` here keeps it out of Git.

## Run

```
python app.py
```

It prints `Question server running on http://127.0.0.1:5000`. Leave it running while you use the
Question Generator. Check it with http://127.0.0.1:5000/health in a browser.

## Playing on a different computer from the server

The game's **AI SERVER** box (Question Generator screen) says where the server is. The default,
`http://127.0.0.1:5000`, means "this computer".

1. On the server computer, add `HOST=0.0.0.0` to `.env`, run `python app.py`, and allow Python
   through the Windows firewall when asked.
2. Find that computer's address with `ipconfig` (IPv4 Address, e.g. `192.168.1.20`).
3. In the game, type `http://192.168.1.20:5000` in the AI SERVER box.

Anyone on that network can then use the server (and the Groq key's quota), so only do this on a
network you trust.

## If the server isn't running

The game says it couldn't reach the AI and makes simpler questions offline instead, so the screen
still works.

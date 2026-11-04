using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PixelStudy.Menus
{
    /// <summary>
    /// Talks to the question server (BackEnd/app.py, Groq AI): sends the pasted notes to POST /generate
    /// and gets back multiple-choice questions ({prompt, correctAnswer, wrongAnswers[3], difficulty}),
    /// which it turns into quiz questions for the QuestionBank.
    /// </summary>
    public static class AiQuestionClient
    {
        /// <summary>How many questions to ask the AI for (the quiz draws 3 per floor).</summary>
        public const int QuestionCount = 10;

        [Serializable]
        class RequestData
        {
            public string notes;
            public int count;
        }

        [Serializable]
        class GeneratedQuestion
        {
            public string prompt;
            public string correctAnswer;
            public string[] wrongAnswers;
            public string difficulty;
        }

        [Serializable]
        class Response
        {
            public GeneratedQuestion[] questions;
            public string error;
        }

        public static IEnumerator Generate(string server, string notes, Action<List<Question>> onComplete, Action<string> onError)
        {
            var url = BaseUrl(server) + "/generate";
            var body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RequestData { notes = notes, count = QuestionCount }));

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 90; // the model can take a while on long notes

                UnityWebRequestAsyncOperation sending;
                try
                {
                    sending = request.SendWebRequest();
                }
                catch (Exception e) // a malformed address
                {
                    onError?.Invoke(e.Message);
                    yield break;
                }
                yield return sending;

                Response response = null;
                try
                {
                    response = JsonUtility.FromJson<Response>(request.downloadHandler.text);
                }
                catch (ArgumentException)
                {
                    // not JSON (wrong address, or not our server)
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(!string.IsNullOrEmpty(response?.error) ? response.error : request.error);
                    yield break;
                }

                var questions = ToQuiz(response?.questions);
                if (questions.Count == 0)
                {
                    onError?.Invoke("the AI didn't send back any usable questions");
                    yield break;
                }
                onComplete?.Invoke(questions);
            }
        }

        /// <summary>"192.168.1.20:5000" -> "http://192.168.1.20:5000" (no trailing slash).</summary>
        static string BaseUrl(string server)
        {
            server = string.IsNullOrWhiteSpace(server) ? QuestionGeneratorController.DefaultServer : server.Trim();
            if (!server.Contains("://")) server = "http://" + server;
            return server.TrimEnd('/');
        }

        /// <summary>
        /// The correct answer plus three wrong ones, shuffled; correctIndex points at the correct one.
        /// Questions without a prompt or correct answer are skipped. If the AI gave fewer than three
        /// different wrong answers, the other questions' answers fill in.
        /// </summary>
        static List<Question> ToQuiz(GeneratedQuestion[] items)
        {
            var result = new List<Question>();
            if (items == null) return result;

            var valid = items.Where(q => q != null && !string.IsNullOrWhiteSpace(q.prompt) && !string.IsNullOrWhiteSpace(q.correctAnswer)).ToList();
            var otherAnswers = valid.Select(q => q.correctAnswer.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var q in valid)
            {
                var correct = q.correctAnswer.Trim();
                var wrong = (q.wrongAnswers ?? new string[0])
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .Select(w => w.Trim())
                    .Where(w => !string.Equals(w, correct, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();
                foreach (var other in otherAnswers.OrderBy(_ => UnityEngine.Random.value))
                {
                    if (wrong.Count >= 3) break;
                    if (!string.Equals(other, correct, StringComparison.OrdinalIgnoreCase) && !wrong.Contains(other, StringComparer.OrdinalIgnoreCase))
                        wrong.Add(other);
                }
                if (wrong.Count < 3) continue; // the quiz always shows four choices

                var answers = new List<string> { correct };
                answers.AddRange(wrong);
                var order = Enumerable.Range(0, answers.Count).OrderBy(_ => UnityEngine.Random.value).ToArray();
                result.Add(new Question
                {
                    prompt = q.prompt.Trim(),
                    answers = order.Select(i => answers[i]).ToArray(),
                    correctIndex = Array.IndexOf(order, 0),
                    difficulty = Difficulty(q.difficulty),
                });
            }
            return result;
        }

        static string Difficulty(string value)
        {
            foreach (var d in new[] { "Easy", "Medium", "Hard" })
                if (string.Equals(value?.Trim(), d, StringComparison.OrdinalIgnoreCase)) return d;
            return "Medium";
        }
    }
}

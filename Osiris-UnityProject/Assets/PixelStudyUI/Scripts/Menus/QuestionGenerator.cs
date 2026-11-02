
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace PixelStudy.Menus
{
    public static class QuestionGenerator
    {
        [Serializable]
        public class RequestData
        {
            public string notes;
        }

        [Serializable]
        public class GeneratedQuestion
        {
            public string prompt;
            public string correctAnswer;
            public string difficulty;
        }

        [Serializable]
        public class QuestionResponse
        {
            public GeneratedQuestion[] questions;
        }

        public static IEnumerator Generate(
            string notes,
            Action<List<Question>> onComplete,
            Action<string> onError)
        {
            string url = "http://127.0.0.1:5000/generate";

            RequestData requestData = new RequestData();
            requestData.notes = notes;

            string json = JsonUtility.ToJson(requestData);

            using (UnityWebRequest request = new UnityWebRequest(
                url, "POST"))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.error);
                    yield break;
                }

                QuestionResponse response =
                    JsonUtility.FromJson<QuestionResponse>(
                        request.downloadHandler.text);

                List<Question> questions = new List<Question>();

                foreach (GeneratedQuestion q in response.questions)
                {
                    questions.Add(new Question(
                        q.prompt,
                        q.correctAnswer,
                        q.difficulty
                    ));
                }

                onComplete?.Invoke(questions);
            }
        }
    }
}

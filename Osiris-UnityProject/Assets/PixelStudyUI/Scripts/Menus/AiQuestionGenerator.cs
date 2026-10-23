// using System.Collections;
// using System.Text;
// using UnityEngine;
// using UnityEngine.Networking;

// public class AiQuestionGenerator: MonoBehaviour
// {
//     public InputField playerInputField;
//     public text aiText;

//     //Log into OpenAi and putservice key hear
//     private string apiKey = "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";
//     //Make pop up login for openAi and a User input for API key
//     private string apiURL = "https.openai.com";

//     public void GenerateQuestion(string q)
//     {
//         StartCoroutine(SendQuizRequest(q));
//     }
    
//     private IEnumerator SendQuizRequest(string q)
//     {
//         string systemPrompt = "You are a quiz generator. Generate a multiple-choice question based on the user's topic. " +
//                               "You must reply ONLY with a JSON object matching this structure: " +
//                               "{\"questionText\":\"...\", \"options\":[\"...\",\"...\",\"...\"], \"correctAnswerIndex\":0}";
//         string payload = "{" + "\"model\": \"gpt-4o-mini\"," + "\"response_format\": { \"type\": \"json_object\" }," + 
//             "\"messages\": [" + "{\"role\": \"system\", \"content\": \"" + systemPrompt + "\"}," +
//              "{\"role\": \"user\", \"content\": \"Topic: " + topic + "\"}" + "]" + "}";

//         using (UnityWebRequest request = new UnityWebRequest(apiURL, "POST"))
//         {
//             byte[] raw = Encoding.UTF8.GetBytes(payload);
//             request.uploadHandler = new UploadHandlerRaw(raw);
//             request.downloadHandler = new DownloadHandlerBuffer();
//             request.SetRequestHeader("Content-Type", "application/json");
//             request.SetRequestHeader("Authorization", "Bearer " + apiKey);
//             yield return request.SendWebRequest;
//             if (request.result == UnityWebRequest.Result.Success)
//             {
//                 ParseAndDisplayQuestion(request.downloader.text);
//             }
//             else
//             {
//                 Debug.LogError("AI Request Failed: " + request.error);
//             }
//         }
//     }

//     private void ParseAndDisplayQuestion(string jsonResponse)
//     {
//         try
//         {
//             string aiResponseText = ExtractContentFromAPIResponse(jsonResponse);
//             QuizQuestion newQuestion = JsonUtility.FromJson<QuizQuestion>(aiResponseText);
//             Debug.Log($"Question Generated: {newQuestion.questionText}");
//         }
//         catch (Exception e)
//         {
//             Debug.LogError("Failed to parse AI JSON: " + e.Message);
//         }
//     }

//     private string ExtractContentFromAPIResponse(string rawJson)
//     {
        
//         int startIndex = rawJson.IndexOf("\"content\": \"") + 12;
//         int endIndex = rawJson.LastIndexOf("\"");
//         return rawJson.Substring(startIndex, endIndex - startIndex).Replace("\\\"", "\"").Replace("\\n", "");
//     }                  
// }
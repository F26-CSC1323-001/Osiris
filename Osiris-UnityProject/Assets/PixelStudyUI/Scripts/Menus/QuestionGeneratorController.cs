using System;
using System.Text;
using System.Collections.Generic;
using System.Collections;
using PixelStudy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;


namespace PixelStudy.Menus
{
    /// <summary>Paste notes → generate questions → save them as the set used in game.</summary>
    public class QuestionGeneratorController : MonoBehaviour
    {
        public UIScreen screen;
        public TMP_InputField setNameField;
        public TMP_InputField notesField;
        public Button generateButton;
        public Button saveButton;
        public Button backButton;
        public RectTransform listContent;
        public GameObject rowTemplate;
        public GameObject emptyState;
        public TMP_Text listHeader;
        public TMP_Text statusText;
        public TMP_Text activeSetText;

        public Color easyColor = new Color(0.42f, 0.82f, 0.42f);
        public Color mediumColor = new Color(0.95f, 0.76f, 0.31f);
        public Color hardColor = new Color(0.9f, 0.33f, 0.3f);

        readonly List<GameObject> rows = new List<GameObject>();
        List<Question> generated = new List<Question>();
        Action onBack;
        [Serializable]
        public class QuizQuestion
        {
            public string questionText;
            public string[] options;
            public int correctAnswerIndex;

        }
        public InputField playerInputField;
        public Text aiText;
        private string apiKey = "KEY"; //Log into Open AI and generate your own key
        private string apiURL = "https://api.openai.com/v1/chat/completions";

        public void GenerateQuestion(string q)
        {
            StartCoroutine(SendQuizRequest(q));
        }
        
        private IEnumerator SendQuizRequest(string q)
        {
            string systemPrompt = "You are a quiz generator. Generate a new multiple-choice question based on the user's question. " +
                                "You must reply ONLY with a JSON object matching this structure: " +
                                "{\"questionText\":\"...\", \"options\":[\"...\",\"...\",\"...\"], \"correctAnswerIndex\":0}";
            string payload = "{" + "\"model\": \"gpt-4o-mini\"," + "\"response_format\": { \"type\": \"json_object\" }," + 
                "\"messages\": [" + "{\"role\": \"system\", \"content\": \"" + systemPrompt + "\"}," +
                "{\"role\": \"user\", \"content\": \"Topic: " + q + "\"}" + "]" + "}";

            using (UnityWebRequest request = new UnityWebRequest(apiURL, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(payload);
                request.uploadHandler = new UploadHandlerRaw(raw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    ParseAndDisplayQuestion(request.downloadHandler.text);
                }
                else
                {
                    Debug.LogError("AI Request Failed: " + request.error);
                }
            }
        }
        private void ParseAndDisplayQuestion(string jsonResponse)
        {
            try
            {
                string aiResponseText = ExtractContentFromAPIResponse(jsonResponse);
                QuizQuestion newQuestion = JsonUtility.FromJson<QuizQuestion>(aiResponseText);
                Debug.Log($"Question Generated: {newQuestion.questionText}");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to parse AI JSON: " + e.Message);
            }
        }

        private string ExtractContentFromAPIResponse(string rawJson)
        {
            
            int startIndex = rawJson.IndexOf("\"content\": \"") + 12;
            int endIndex = rawJson.LastIndexOf("\"");
            return rawJson.Substring(startIndex, endIndex - startIndex).Replace("\\\"", "\"").Replace("\\n", "");
        }

        public bool IsOpen => screen.IsOpen;

        void Awake()
        {
            generateButton.onClick.AddListener(Generate);
            saveButton.onClick.AddListener(SaveSet);
            backButton.onClick.AddListener(() => onBack?.Invoke());
            rowTemplate.SetActive(false);
        }

        public void Open(Action back)
        {
            onBack = back;
            saveButton.interactable = generated.Count > 0;
            RefreshActiveSet();
            screen.Show();
        }

        public void Close() => screen.Hide();

        void Generate()
        {
            if (string.IsNullOrWhiteSpace(notesField.text))
            {
                SetStatus("Paste some notes first, then press Generate.", hardColor);
                return;
            }

            generated = QuestionGenerator.Generate(notesField.text);
            foreach (var row in rows) Destroy(row);
            rows.Clear();

            foreach (var q in generated)
            {
                var row = Instantiate(rowTemplate, listContent);
                row.SetActive(true);
                var texts = row.GetComponentsInChildren<TMP_Text>(true);
                // Template children, in order: Prompt, Answer, Difficulty.
                texts[0].text = q.prompt;
                texts[1].text = "Answer: " + q.CorrectAnswer;
                texts[2].text = "Difficulty: " + q.difficulty;
                texts[2].color = q.difficulty == "Easy" ? easyColor : q.difficulty == "Hard" ? hardColor : mediumColor;
                rows.Add(row);
            }

            emptyState.SetActive(generated.Count == 0);
            listHeader.text = $"GENERATED QUESTIONS ({generated.Count})";
            saveButton.interactable = generated.Count > 0;
            if (generated.Count == 0)
                SetStatus("Couldn't find enough to quiz on. Try full sentences like \"A queue is a FIFO list.\"", hardColor);
            else
                SetStatus($"Made {generated.Count} questions. Save the set to use it in the tower.", easyColor);
        }

        void SaveSet()
        {
            // if (generated.Count == 0) return;
            // var setName = string.IsNullOrWhiteSpace(setNameField.text) ? "My Study Set" : setNameField.text.Trim();
            // QuestionBank.SaveSet(new QuestionSet { name = setName, questions = new List<Question>(generated) });
            // SetStatus($"Saved \"{setName}\". Your next floors will ask these questions.", easyColor);
            // RefreshActiveSet();
        }

        void RefreshActiveSet()
        {
            //if (activeSetText != null) activeSetText.text = "Active set: " + QuestionBank.ActiveSetName;
        }

        void SetStatus(string message, Color color)
        {
            // statusText.text = message;
            // statusText.color = color;
        }
    }
}

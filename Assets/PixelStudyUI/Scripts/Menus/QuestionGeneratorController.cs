using System;
using System.Collections.Generic;
using PixelStudy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.Menus
{
    /// <summary>
    /// Paste notes, press Generate: the AI server (BackEnd/app.py, Groq) writes multiple-choice questions,
    /// and they're saved right away as the active set, so the next floors' quizzes ask them (Save Set
    /// saves them again under a new name). If the server can't be reached, the offline generator in
    /// QuestionBank.cs makes simpler questions instead so the screen still works.
    /// </summary>
    public class QuestionGeneratorController : MonoBehaviour
    {
        public const string DefaultServer = "http://127.0.0.1:5000";
        const string ServerKey = "PixelStudy.AiServer";
        const string DefaultSetName = "My Study Set";

        public UIScreen screen;
        public TMP_InputField setNameField;
        public TMP_InputField notesField;
        [Tooltip("Where the AI question server runs (remembered between sessions).")]
        public TMP_InputField serverField;
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
        bool generating;

        public bool IsOpen => screen.IsOpen;

        /// <summary>The AI server's address.</summary>
        public static string Server
        {
            get => PlayerPrefs.GetString(ServerKey, DefaultServer);
            set
            {
                PlayerPrefs.SetString(ServerKey, string.IsNullOrWhiteSpace(value) ? DefaultServer : value.Trim());
                PlayerPrefs.Save();
            }
        }

        void Awake()
        {
            generateButton.onClick.AddListener(Generate);
            saveButton.onClick.AddListener(SaveSet);
            backButton.onClick.AddListener(() => onBack?.Invoke());
            if (serverField != null) serverField.onEndEdit.AddListener(value => Server = value);
            rowTemplate.SetActive(false);
        }

        public void Open(Action back)
        {
            onBack = back;
            if (serverField != null) serverField.text = Server;
            saveButton.interactable = generated.Count > 0 && !generating;
            RefreshActiveSet();
            screen.Show();
        }

        public void Close() => screen.Hide();

        void Generate()
        {
            if (generating) return;
            if (string.IsNullOrWhiteSpace(notesField.text))
            {
                SetStatus("Paste some notes first, then press Generate.", hardColor);
                return;
            }
            if (serverField != null) Server = serverField.text;

            generating = true;
            generateButton.interactable = false;
            saveButton.interactable = false;
            SetStatus("Asking the AI for questions...", mediumColor);
            StartCoroutine(AiQuestionClient.Generate(Server, notesField.text, OnAiQuestions, OnAiFailed));
        }

        void OnAiQuestions(List<Question> questions)
        {
            ShowQuestions(questions);
            var setName = SaveGenerated();
            SetStatus($"The AI made {questions.Count} questions, saved as \"{setName}\". Your next floors will ask them.", easyColor);
        }

        void OnAiFailed(string error)
        {
            // no server (or it failed): the offline generator keeps the screen usable
            var offline = QuestionGenerator.Generate(notesField.text);
            ShowQuestions(offline);
            if (offline.Count == 0)
            {
                SetStatus($"Couldn't reach the AI ({error}), and there wasn't enough in the notes to quiz on offline.", hardColor);
                return;
            }
            var setName = SaveGenerated();
            SetStatus($"Couldn't reach the AI ({error}). Made {offline.Count} simpler questions offline, saved as \"{setName}\".", mediumColor);
        }

        void ShowQuestions(List<Question> questions)
        {
            generating = false;
            generateButton.interactable = true;
            generated = questions;

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
        }

        void SaveSet()
        {
            if (generated.Count == 0) return;
            var setName = SaveGenerated();
            SetStatus($"Saved \"{setName}\". Your next floors will ask these questions.", easyColor);
        }

        /// <summary>Saves the generated questions as the active set (the one the quiz draws from). Returns its name.</summary>
        string SaveGenerated()
        {
            var setName = string.IsNullOrWhiteSpace(setNameField.text) ? DefaultSetName : setNameField.text.Trim();
            QuestionBank.SaveSet(new QuestionSet { name = setName, questions = new List<Question>(generated) });
            RefreshActiveSet();
            return setName;
        }

        void RefreshActiveSet()
        {
            if (activeSetText != null) activeSetText.text = "Active set: " + QuestionBank.ActiveSetName;
        }

        void SetStatus(string message, Color color)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using PixelStudy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.Game
{
    /// <summary>The quiz popup: one question at a time, four answers, green/red feedback.</summary>
    public class QuizPanel : MonoBehaviour
    {
        public UIScreen screen;
        public TMP_Text counterText;
        public TMP_Text floorText;
        public GameObject bossTag;
        public TMP_Text questionText;
        public TMP_Text feedbackText;
        public Button[] answerButtons;
        public TMP_Text[] answerTexts;
        public Image[] progressPips;
        public CanvasGroup answersGroup;
        public RectTransform shakeTarget;

        [Header("Colors")]
        public Color pipEmpty = new Color(0.2f, 0.16f, 0.28f);
        public Color pipCurrent = new Color(0.95f, 0.76f, 0.31f);
        public Color correctColor = new Color(0.3f, 0.69f, 0.31f);
        public Color wrongColor = new Color(0.85f, 0.27f, 0.3f);
        public Color hintColor = new Color(0.65f, 0.59f, 0.77f);

        public float revealDelay = 1.1f;

        static readonly string[] Letters = { "A", "B", "C", "D" };

        List<Question> questions;
        int index, correctCount;
        bool locked;
        Func<bool, bool> onAnswered;
        Action<int, int> onComplete;
        Coroutine routine;

        public bool IsOpen => screen.IsOpen;

        void Awake()
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                int n = i;
                answerButtons[i].onClick.AddListener(() => Choose(n));
            }
        }

        void Update()
        {
            if (!IsOpen || locked || questions == null) return;
            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) Choose(i);
            }
        }

        /// <param name="answered">Called after each answer with whether it was right. Return false to stop the quiz.</param>
        /// <param name="complete">Called with (correct, total) when all questions are done.</param>
        public void Begin(List<Question> list, int floor, bool boss, Func<bool, bool> answered, Action<int, int> complete)
        {
            questions = list;
            index = 0;
            correctCount = 0;
            onAnswered = answered;
            onComplete = complete;
            floorText.text = $"FLOOR {floor}";
            bossTag.SetActive(boss);
            for (int i = 0; i < progressPips.Length; i++)
            {
                progressPips[i].gameObject.SetActive(i < list.Count);
                progressPips[i].color = pipEmpty;
            }
            screen.Show();
            ShowQuestion();
        }

        public void Abort()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            questions = null;
            screen.Hide();
        }

        void ShowQuestion()
        {
            var q = questions[index];
            locked = false;
            answersGroup.interactable = true;
            counterText.text = $"QUESTION {index + 1} OF {questions.Count}";
            questionText.text = q.prompt;
            for (int i = 0; i < answerButtons.Length; i++)
            {
                bool used = i < q.answers.Length;
                answerButtons[i].gameObject.SetActive(used);
                if (used) answerTexts[i].text = q.answers[i];
                Hover(i)?.ClearOverrideColor();
            }
            if (index < progressPips.Length) progressPips[index].color = pipCurrent;
            feedbackText.text = "Press 1-4 or click an answer";
            feedbackText.color = hintColor;
        }

        void Choose(int choice)
        {
            if (locked || questions == null || choice >= questions[index].answers.Length) return;
            locked = true;
            answersGroup.interactable = false;

            var q = questions[index];
            bool right = choice == q.correctIndex;
            if (right) correctCount++;

            Hover(q.correctIndex)?.SetOverrideColor(correctColor);
            if (!right)
            {
                Hover(choice)?.SetOverrideColor(wrongColor);
                StartCoroutine(Shake());
            }
            if (index < progressPips.Length) progressPips[index].color = right ? correctColor : wrongColor;
            feedbackText.text = right ? "Correct!" : $"Not quite - the answer was {Letters[q.correctIndex]}.";
            feedbackText.color = right ? correctColor : wrongColor;

            bool keepGoing = onAnswered == null || onAnswered(right);
            if (keepGoing && questions != null) routine = StartCoroutine(Next());
        }

        IEnumerator Next()
        {
            yield return new WaitForSecondsRealtime(revealDelay);
            routine = null;
            index++;
            if (index < questions.Count)
            {
                ShowQuestion();
            }
            else
            {
                int correct = correctCount, total = questions.Count;
                questions = null;
                screen.Hide();
                onComplete?.Invoke(correct, total);
            }
        }

        IEnumerator Shake()
        {
            if (shakeTarget == null) yield break;
            var start = shakeTarget.anchoredPosition;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float strength = 12f * (1f - t / 0.35f);
                shakeTarget.anchoredPosition = start + new Vector2(Mathf.Round(UnityEngine.Random.Range(-strength, strength)), 0f);
                yield return null;
            }
            shakeTarget.anchoredPosition = start;
        }

        UIButtonHover Hover(int i) => i >= 0 && i < answerButtons.Length ? answerButtons[i].GetComponent<UIButtonHover>() : null;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PixelStudy
{
    [Serializable]
    public class Question
    {
        public string prompt;
        public string[] answers = new string[4];
        public int correctIndex;
        public string difficulty = "Medium";

        public string CorrectAnswer => answers[correctIndex];
    }

    [Serializable]
    public class QuestionSet
    {
        public string name;
        public List<Question> questions = new List<Question>();
    }

    /// <summary>
    /// Where quiz questions come from: the player's saved sets (made on the Question Generator
    /// screen) or a built-in set of intro CS questions.
    /// </summary>
    public static class QuestionBank
    {
        [Serializable]
        class SaveFile
        {
            public string activeSet = "";
            public List<QuestionSet> sets = new List<QuestionSet>();
        }

        public const string BuiltInSetName = "Built-in CS Basics";

        static SaveFile file;

        static string FilePath => Path.Combine(Application.persistentDataPath, "question_sets.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => file = null;

        static SaveFile Data
        {
            get
            {
                if (file != null) return file;
                file = new SaveFile();
                try
                {
                    if (File.Exists(FilePath)) file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath)) ?? new SaveFile();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[QuestionBank] Could not read {FilePath}: {e.Message}");
                }
                return file;
            }
        }

        public static IReadOnlyList<QuestionSet> Sets => Data.sets;

        public static string ActiveSetName
        {
            get
            {
                var set = ActiveSet;
                return set == null ? BuiltInSetName : set.name;
            }
        }

        static QuestionSet ActiveSet => Data.sets.FirstOrDefault(s => s.name == Data.activeSet && s.questions.Count > 0);

        public static void SaveSet(QuestionSet set)
        {
            Data.sets.RemoveAll(s => string.Equals(s.name, set.name, StringComparison.OrdinalIgnoreCase));
            Data.sets.Add(set);
            Data.activeSet = set.name;
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QuestionBank] Could not save {FilePath}: {e.Message}");
            }
        }

        /// <summary>Picks <paramref name="count"/> random questions with shuffled answers.</summary>
        public static List<Question> Draw(int count)
        {
            var pool = ActiveSet?.questions ?? BuiltIn;
            var shuffled = pool.OrderBy(_ => UnityEngine.Random.value).ToList();
            var result = new List<Question>();
            for (int i = 0; i < count; i++) result.Add(ShuffleAnswers(shuffled[i % shuffled.Count]));
            return result;
        }

        static Question ShuffleAnswers(Question q)
        {
            var order = Enumerable.Range(0, q.answers.Length).OrderBy(_ => UnityEngine.Random.value).ToArray();
            return new Question
            {
                prompt = q.prompt,
                difficulty = q.difficulty,
                answers = order.Select(i => q.answers[i]).ToArray(),
                correctIndex = Array.IndexOf(order, q.correctIndex),
            };
        }

        static Question Q(string prompt, string difficulty, string correct, string a, string b, string c) =>
            new Question { prompt = prompt, difficulty = difficulty, answers = new[] { correct, a, b, c }, correctIndex = 0 };

        static readonly List<Question> BuiltIn = new List<Question>
        {
            Q("What is the time complexity of binary search?", "Medium", "O(log n)", "O(n)", "O(n log n)", "O(1)"),
            Q("What data structure uses FIFO ordering?", "Easy", "Queue", "Stack", "Binary tree", "Hash table"),
            Q("What data structure uses LIFO ordering?", "Easy", "Stack", "Queue", "Linked list", "Heap"),
            Q("In C++, Java and C#, what is the index of the first element of an array?", "Easy", "0", "1", "-1", "It depends on the array"),
            Q("Which keyword exits a loop immediately?", "Easy", "break", "continue", "skip", "next"),
            Q("Which loop always runs its body at least once?", "Medium", "do-while", "for", "while", "foreach"),
            Q("What is 7 % 3?", "Easy", "1", "2", "0", "3"),
            Q("Which type stores only true or false?", "Easy", "bool", "int", "char", "string"),
            Q("What is the worst-case time complexity of linear search?", "Medium", "O(n)", "O(log n)", "O(1)", "O(n^2)"),
            Q("What is 5 written in binary?", "Medium", "101", "110", "111", "100"),
            Q("Which sort repeatedly swaps neighboring elements that are out of order?", "Medium", "Bubble sort", "Merge sort", "Quick sort", "Heap sort"),
            Q("What does OOP stand for?", "Easy", "Object-Oriented Programming", "Ordered Operation Processing", "Open Output Protocol", "Object Overload Pattern"),
            Q("A variable declared inside a function is called a...", "Easy", "Local variable", "Global variable", "Static field", "Constant literal"),
            Q("What is the average time complexity of merge sort?", "Hard", "O(n log n)", "O(n)", "O(n^2)", "O(log n)"),
            Q("What does a recursive function need so it eventually stops?", "Medium", "A base case", "A global variable", "A while loop", "A return type of void"),
        };
    }

    /// <summary>
    /// Turns pasted notes into multiple-choice questions without any internet connection.
    /// Lines like "Term is definition" / "Term: definition" become "What is Term?" questions;
    /// other sentences become fill-in-the-blank questions. Swap this out for an AI call later
    /// if your group wants smarter questions.
    /// </summary>
    public static class QuestionGenerator
    {
        static readonly Regex DefinitionPattern = new Regex(
            @"^(?<term>[A-Za-z0-9][\w\s\-\(\)'/]{1,48}?)\s*(?:(?<verb>\bis\b|\bare\b|\bmeans\b|\brefers to\b)|[:=]|\s[-–—]\s)\s*(?<def>.{3,})$",
            RegexOptions.IgnoreCase);

        static readonly Regex WordPattern = new Regex(@"\b[A-Za-z][A-Za-z\-]{4,}\b");

        static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "about", "above", "after", "again", "always", "among", "another", "because", "before", "being",
            "below", "between", "could", "every", "first", "other", "their", "there", "these", "thing",
            "things", "those", "through", "under", "until", "using", "usually", "where", "which", "while",
            "would", "should", "might", "often", "called", "means", "example", "also",
        };

        static readonly string[] FallbackWords = { "algorithm", "variable", "function", "memory", "pointer", "compiler", "iteration", "object" };

        public static List<Question> Generate(string notes, int maxQuestions = 12)
        {
            var result = new List<Question>();
            if (string.IsNullOrWhiteSpace(notes)) return result;

            var sentences = Regex.Split(notes, @"(?<=[\.\?\!])\s+|\r?\n+")
                .Select(s => s.Trim().TrimStart('-', '*', '•', ' ').Trim())
                .Where(s => s.Length >= 12)
                .Distinct()
                .ToList();

            var definitions = new List<(string term, string def, string verb, string sentence)>();
            var other = new List<string>();
            foreach (var s in sentences)
            {
                var m = DefinitionPattern.Match(s.TrimEnd('.'));
                if (m.Success && m.Groups["def"].Value.Length <= 140)
                    definitions.Add((m.Groups["term"].Value.Trim(), m.Groups["def"].Value.Trim(), m.Groups["verb"].Value.ToLowerInvariant(), s));
                else
                    other.Add(s);
            }

            var vocabulary = WordPattern.Matches(notes).Cast<Match>().Select(m => m.Value)
                .Where(w => !StopWords.Contains(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var d in definitions)
            {
                var otherDefs = definitions.Where(o => o.def != d.def).Select(o => o.def).Distinct().OrderBy(_ => UnityEngine.Random.value).Take(3).ToList();
                if (otherDefs.Count >= 3)
                {
                    // Enough definitions to mix up: "What is a stack?" -> pick the right definition.
                    var askVerb = d.verb == "are" ? "are" : "is";
                    result.Add(Build($"What {askVerb} {LowerArticle(d.term)}?", Shorten(d.def), otherDefs.Select(Shorten), d.sentence.Length));
                }
                else if (definitions.Count >= 2)
                {
                    // Only a few definitions: show the definition, pick the right term.
                    var term = CleanTerm(d.term);
                    var distractors = definitions.Select(o => CleanTerm(o.term)).Where(t => !string.Equals(t, term, StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var w in vocabulary.OrderBy(_ => UnityEngine.Random.value).Concat(FallbackWords))
                    {
                        if (distractors.Count >= 3) break;
                        var candidate = CleanTerm(w);
                        if (!d.sentence.Contains(w) && !distractors.Any(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase)) &&
                            !string.Equals(candidate, term, StringComparison.OrdinalIgnoreCase))
                            distractors.Add(candidate);
                    }
                    result.Add(Build($"Which term matches: \"{Shorten(d.def)}\"?", term, distractors, d.sentence.Length));
                }
                else
                {
                    other.Add(d.sentence);
                }
            }

            foreach (var s in other)
            {
                if (result.Count >= maxQuestions) break;
                var words = WordPattern.Matches(s).Cast<Match>().Select(m => m.Value).Where(w => !StopWords.Contains(w)).ToList();
                if (words.Count == 0) continue;
                var key = words.OrderByDescending(w => w.Length).First();
                var distractors = vocabulary
                    .Where(w => !string.Equals(w, key, StringComparison.OrdinalIgnoreCase) && !s.Contains(w))
                    .OrderBy(w => Mathf.Abs(w.Length - key.Length) + UnityEngine.Random.value * 3f)
                    .Take(3)
                    .ToList();
                foreach (var f in FallbackWords)
                {
                    if (distractors.Count >= 3) break;
                    if (!string.Equals(f, key, StringComparison.OrdinalIgnoreCase) && !distractors.Contains(f)) distractors.Add(f);
                }
                var blanked = new Regex(@"\b" + Regex.Escape(key) + @"\b").Replace(s, "_____", 1);
                result.Add(Build("Fill in the blank: " + blanked, key, distractors, s.Length));
            }

            return result.Take(maxQuestions).ToList();
        }

        static Question Build(string prompt, string correct, IEnumerable<string> distractors, int sourceLength)
        {
            var answers = new List<string> { correct };
            answers.AddRange(distractors.Take(3));
            var order = Enumerable.Range(0, answers.Count).OrderBy(_ => UnityEngine.Random.value).ToArray();
            return new Question
            {
                prompt = prompt,
                answers = order.Select(i => answers[i]).ToArray(),
                correctIndex = Array.IndexOf(order, 0),
                difficulty = sourceLength < 60 ? "Easy" : sourceLength < 120 ? "Medium" : "Hard",
            };
        }

        static readonly Regex LeadingArticle = new Regex(@"^(a|an|the)\s+", RegexOptions.IgnoreCase);

        /// <summary>"A stack" -> "Stack" (used as an answer choice).</summary>
        static string CleanTerm(string term)
        {
            term = LeadingArticle.Replace(term.Trim(), "");
            return term.Length > 0 ? char.ToUpperInvariant(term[0]) + term.Substring(1) : term;
        }

        /// <summary>"A stack" -> "a stack" (used inside "What is ...?").</summary>
        static string LowerArticle(string term)
        {
            var m = LeadingArticle.Match(term);
            return m.Success ? m.Value.ToLowerInvariant() + term.Substring(m.Length) : term;
        }

        static string Shorten(string s)
        {
            s = s.Trim().TrimEnd('.');
            if (s.Length > 0) s = char.ToUpperInvariant(s[0]) + s.Substring(1);
            return s.Length <= 60 ? s : s.Substring(0, 57).TrimEnd() + "...";
        }
    }
}

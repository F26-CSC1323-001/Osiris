
using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Menus
{
    public static class QuestionBank
    {
        private static QuestionSet activeSet;

        public static string ActiveSetName
        {
            get
            {
                return activeSet != null
                    ? activeSet.name
                    : "No active set";
            }
        }

        public static void SaveSet(QuestionSet set)
        {
            activeSet = set;

            string json = JsonUtility.ToJson(set);

            PlayerPrefs.SetString("ActiveQuestionSet", json);
            PlayerPrefs.Save();
        }

        public static QuestionSet LoadSet()
        {
            if (!PlayerPrefs.HasKey("ActiveQuestionSet"))
            {
                return null;
            }

            string json =
                PlayerPrefs.GetString("ActiveQuestionSet");

            activeSet = JsonUtility.FromJson<QuestionSet>(json);

            return activeSet;
        }

        public static QuestionSet GetActiveSet()
        {
            return activeSet;
        }
    }
}

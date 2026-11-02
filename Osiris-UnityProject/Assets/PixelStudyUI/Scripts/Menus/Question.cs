namespace PixelStudy.Menus
{
    [System.Serializable]
    public class Question
    {
        public string prompt;
        public string CorrectAnswer { get; private set; }
        public string difficulty;

        public Question(string prompt, string correctAnswer, string difficulty)
        {
            this.prompt = prompt;
            CorrectAnswer = correctAnswer;
            this.difficulty = difficulty;
        }
    }
}
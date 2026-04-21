namespace DontWorryMom
{
    public enum DifficultyLevel { Chill, Tense, Anxious, Panicked }

    public static class DifficultyConfig
    {
        public static int MaxTurns(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Chill    => 7,
            DifficultyLevel.Tense    => 10,
            DifficultyLevel.Anxious  => 14,
            DifficultyLevel.Panicked => 20,
            _                        => 10
        };

        // Starting meter values — harder difficulties begin with Mom already on edge
        public static float InitialWorry(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Chill    => 20f,
            DifficultyLevel.Tense    => 40f,
            DifficultyLevel.Anxious  => 52f,
            DifficultyLevel.Panicked => 62f,
            _                        => 40f
        };

        public static float InitialSuspicion(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Chill    => 5f,
            DifficultyLevel.Tense    => 10f,
            DifficultyLevel.Anxious  => 18f,
            DifficultyLevel.Panicked => 28f,
            _                        => 10f
        };

        public static string Label(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Chill    => "CHILL MOM",
            DifficultyLevel.Tense    => "TENSE MOM",
            DifficultyLevel.Anxious  => "ANXIOUS MOM",
            DifficultyLevel.Panicked => "PANICKED MOM",
            _                        => "MOM"
        };

        public static string Rating(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Chill    => "EASY",
            DifficultyLevel.Tense    => "EASY — MED",
            DifficultyLevel.Anxious  => "MED — HARD",
            DifficultyLevel.Panicked => "HARD",
            _                        => "MEDIUM"
        };
    }
}

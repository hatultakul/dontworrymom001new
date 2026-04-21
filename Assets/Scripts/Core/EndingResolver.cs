namespace DontWorryMom
{
    /// <summary>
    /// Evaluates final session state and returns the appropriate ending (GDD §4).
    ///
    ///   SECRET  — Worry < 30  AND Suspicion < 20  AND TruthCount >= 7
    ///   WIN     — both meters below 80 at end of call (no mid-session crash)
    ///   LOSE W  — Worry >= 80  OR crashed with WorryCrashed
    ///   LOSE S  — Suspicion >= 80  OR crashed with SuspicionCrashed
    /// </summary>
    public class EndingResolver
    {
        public EndingType Resolve(GameSession session)
        {
            // Mid-session crashes take priority
            if (session.WorryCrashed)      return EndingType.MomIsSpiralizing;
            if (session.SuspicionCrashed)  return EndingType.Caught;

            float w = session.Worry;
            float s = session.Suspicion;
            int   t = session.TruthCount;

            // Lose — worry hit the red zone
            if (w >= 80f)
                return EndingType.MomIsSpiralizing;

            // Lose — suspicion hit the red zone
            if (s >= 80f)
                return EndingType.Caught;

            // Secret ending: survived AND was fully honest
            if (w < 30f && s < 20f && t >= 7)
                return EndingType.TheTruth;

            // Win — both meters stayed below the danger threshold
            return EndingType.SteadyDaughter;
        }

        public static string Title(EndingType e) => e switch
        {
            EndingType.SteadyDaughter   => "Steady Daughter",
            EndingType.MomIsSpiralizing => "Mom is Spiraling",
            EndingType.Caught           => "Caught",
            EndingType.TheTruth         => "The Truth",
            _                           => string.Empty
        };

        public static string Body(EndingType e) => e switch
        {
            EndingType.SteadyDaughter =>
                "\"Okay, sweetheart. I love you. Don't be a stranger.\"\n\nShe believed you. Mostly.",

            EndingType.MomIsSpiralizing =>
                "\"I'm booking a flight.\"\n\nYou hear her opening a browser tab.",

            EndingType.Caught =>
                "A long pause.\n\n\"I know, baby. I know.\"\n\nShe always knew.",

            EndingType.TheTruth =>
                "Silence.\n\nThen: \"I'm proud of you. I don't say that enough.\"\n\nShe doesn't.",

            _ => string.Empty
        };
    }
}

namespace DontWorryMom
{
    public enum ResponseType  { TRUTH, LIE, DEFLECT }
    public enum ResponseTone  { CONFIDENT, VAGUE, OVERSHARE }

    public enum QuestionCategory
    {
        HEALTH, WORK, MONEY, LOVE, HOME, FRIENDS, FUTURE
    }

    public enum ContradictionSeverity { Soft, Hard }

    public enum EndingType
    {
        SteadyDaughter,   // WIN  — Worry < 50 AND Suspicion < 50
        MomIsSpiralizing, // LOSE — Worry >= 80 or hit 100
        Caught,           // LOSE — Suspicion >= 80 or hit 100
        TheTruth          // SECRET — Worry < 30, Suspicion < 20, >= 7 TRUTH answers
    }
}

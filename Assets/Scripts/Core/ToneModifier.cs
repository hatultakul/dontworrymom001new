using UnityEngine;

namespace DontWorryMom
{
    /// <summary>
    /// Applies tone to base worry/suspicion deltas using a full Type × Tone matrix.
    /// Tones are player-visible as OVERCONFIDENT / NEUTRAL / HESITANT (enum: CONFIDENT/VAGUE/OVERSHARE).
    ///
    /// OVERCONFIDENT (CONFIDENT) — performing calm; sounds polished, reads as rehearsed
    ///   TRUTH   — strong worry drop; if she's already suspicious the bravado reads defensive
    ///   LIE     — big worry drop, high suspicion cost; worse when she's already tracking you
    ///   DEFLECT — dismissive; both meters spike
    ///   Streak  — sounds scripted: susp +10, worry amplified
    ///
    /// NEUTRAL (VAGUE) — no performance; just talking; the safest baseline
    ///   TRUTH   — modest worry drop; modest suspicion drop; no amplifiers
    ///   LIE     — average damage both ways; no conditional risk
    ///   DEFLECT — small rise both meters; grants fact immunity
    ///   Streak  — prolonged neutrality reads as avoidant: moderate worry penalty
    ///
    /// HESITANT (OVERSHARE) — clearly anxious; nervousness is legible
    ///   TRUTH   — nuclear worry spike + suspicion crater; she believes you completely
    ///   LIE     — nervous lying is transparent: high suspicion
    ///   DEFLECT — rambling; suspicion drops; loses effect late game (turn >= 7)
    ///   Streak  — she starts matching your anxiety: susp +8
    ///
    /// Category sensitivity:
    ///   Emotional (HEALTH, FRIENDS, LOVE):
    ///     HESITANT TRUTH  → worry × 1.2 (alarming to hear emotional distress)
    ///   Factual (WORK, MONEY, HOME):
    ///     OVERCONFIDENT TRUTH → susp -2 (natural to sound sure about facts)
    ///     OVERCONFIDENT LIE   → susp -2 (expected to know your own situation)
    /// </summary>
    public static class ToneModifier
    {
        /// <summary>
        /// Returns adjusted (worryDelta, suspicionDelta).
        /// Tone is read from session.CurrentTone; streak from session.ToneStreak.
        /// </summary>
        public static (int worry, int suspicion) Apply(
            ResponseData   response,
            QuestionCategory category,
            GameSession    session)
        {
            float worry = response.worryDelta;
            int   susp  = response.suspicionDelta;

            ResponseTone tone   = session.CurrentTone;
            ResponseType type   = response.TypeEnum;
            int          streak = session.ToneStreak;
            float        w      = session.Worry;
            float        s      = session.Suspicion;
            int          turn   = session.TurnIndex;

            bool isEmotional = category == QuestionCategory.HEALTH  ||
                               category == QuestionCategory.FRIENDS ||
                               category == QuestionCategory.LOVE;
            bool isFactual   = category == QuestionCategory.WORK  ||
                               category == QuestionCategory.MONEY ||
                               category == QuestionCategory.HOME;

            switch (tone)
            {
                // ── OVERCONFIDENT ─────────────────────────────────────────────
                case ResponseTone.CONFIDENT:
                    switch (type)
                    {
                        case ResponseType.TRUTH:
                            worry *= 0.65f;
                            susp  -= 2;
                            if (isFactual) susp -= 2;    // natural to sound sure about facts
                            if (s > 60)    susp += 12;   // too polished when suspected = defensive
                            break;

                        case ResponseType.LIE:
                            worry *= 0.68f;
                            susp  += 14;
                            if (s > 50)    susp += 10;   // doubling down when she's already tracking you
                            if (isFactual) susp -= 2;    // expected to know your situation
                            break;

                        case ResponseType.DEFLECT:
                            worry += 12f;
                            susp  += 8;
                            break;
                    }
                    // Streak: 3+ turns sounds scripted
                    if (streak >= 3) { worry *= 1.15f; susp += 10; }
                    break;

                // ── NEUTRAL ───────────────────────────────────────────────────
                case ResponseTone.VAGUE:
                    switch (type)
                    {
                        case ResponseType.TRUTH:
                            worry *= 0.90f;
                            susp  -= 5;
                            break;

                        case ResponseType.LIE:
                            worry += 3f;
                            susp  += 5;
                            break;

                        case ResponseType.DEFLECT:
                            worry += 4f;
                            susp  += 3;
                            // Fact immunity: no facts recorded = no future contradictions
                            break;
                    }
                    // Streak: prolonged neutrality starts reading as avoidant
                    if (streak >= 3) worry *= 1.15f;
                    break;

                // ── HESITANT ──────────────────────────────────────────────────
                case ResponseTone.OVERSHARE:
                    switch (type)
                    {
                        case ResponseType.TRUTH:
                            worry *= 1.8f;
                            susp  -= 15;
                            if (isEmotional) worry *= 1.2f; // emotional distress sounds alarming
                            if (w > 60)      worry *= 1.2f; // piling on an already-worried mom
                            break;

                        case ResponseType.LIE:
                            worry *= 0.85f;
                            susp  += 12;
                            if (s > 45) susp += 8;          // nervous lying is transparent
                            break;

                        case ResponseType.DEFLECT:
                            worry *= 1.2f;
                            susp  -= 10;
                            if (turn >= 7) susp += 6;       // late game: she's focused; anxiety won't distract
                            break;
                    }
                    // Streak: she starts matching your anxiety level
                    if (streak >= 3) susp += 8;
                    break;
            }

            return (Mathf.RoundToInt(worry), susp);
        }
    }
}

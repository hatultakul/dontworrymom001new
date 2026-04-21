using System;
using System.Collections.Generic;

namespace DontWorryMom
{
    /// <summary>
    /// Watches fact state across the session and fires contradiction events per GDD §6.
    ///
    ///   Hard contradiction: same fact key set to the opposite value → +30 Suspicion.
    ///   Consistency bonus: 3+ consecutive LIE answers in same category → -10 Suspicion.
    /// </summary>
    public class ContradictionTracker
    {
        private readonly GameSession _session;

        private struct AnswerRecord
        {
            public QuestionCategory Category;
            public ResponseType     Type;
        }

        private readonly List<AnswerRecord> _history = new();

        // Per-category streak counters for consistency bonus
        private readonly Dictionary<QuestionCategory, int> _lieStreak = new();

        public event Action<ContradictionSeverity> OnContradiction;

        public ContradictionTracker(GameSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Call immediately after the player selects a response.
        /// Applies contradiction penalties, commits new facts, checks consistency bonus.
        /// Returns true if any contradiction was triggered.
        ///
        /// Pass factImmune=true for VAGUE DEFLECT: skips all fact processing and history
        /// recording so the answer leaves no traceable commitment (no future contradiction risk).
        /// </summary>
        public bool ProcessResponse(QuestionData question, ResponseData response,
                                    bool factImmune = false)
        {
            // VAGUE DEFLECT: the answer is ambiguous enough that no fact is established.
            // Nothing is recorded — no contradiction can be triggered now or later.
            if (factImmune) return false;

            bool contradicted = false;

            // ── Hard contradiction check ──────────────────────────────────────
            foreach (var kvp in response.FactsSetDict)
            {
                bool? existing = _session.GetFact(kvp.Key);
                if (existing.HasValue && existing.Value != kvp.Value)
                {
                    _session.ApplySuspicionDelta(30);
                    OnContradiction?.Invoke(ContradictionSeverity.Hard);
                    contradicted = true;
                    break;
                }
            }

            // ── Commit new facts ──────────────────────────────────────────────
            foreach (var kvp in response.FactsSetDict)
                _session.SetFact(kvp.Key, kvp.Value);

            // ── Record history ────────────────────────────────────────────────
            _history.Add(new AnswerRecord
            {
                Category = question.CategoryEnum,
                Type     = response.TypeEnum
            });

            // ── Consistency bonus (commit to a lie) ───────────────────────────
            if (response.TypeEnum == ResponseType.LIE)
            {
                _lieStreak.TryGetValue(question.CategoryEnum, out int streak);

                if (!contradicted)
                {
                    streak++;
                    _lieStreak[question.CategoryEnum] = streak;

                    if (streak == 3)
                        _session.ApplySuspicionDelta(-10);
                }
                else
                {
                    _lieStreak[question.CategoryEnum] = 0;
                }
            }
            else
            {
                _lieStreak[question.CategoryEnum] = 0;
            }

            return contradicted;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DontWorryMom
{
    /// <summary>
    /// Selects the next question from the pool based on session state (GDD §7).
    ///
    /// Rules:
    ///   • Turn 0  → scripted opener  (id = "q_opener")
    ///   • Turn 9  → scripted closer  (id = "q_closer")
    ///   • Otherwise: pick from tier-appropriate pool, weighted by lie history and meter state.
    ///   • No question id repeats within a session.
    ///   • requiredFacts must be satisfied for a question to be eligible.
    /// </summary>
    public class QuestionDirector
    {
        private readonly List<QuestionData> _allQuestions;
        private readonly GameSession        _session;
        private readonly HashSet<string>    _usedIds = new();

        // Categories where the player has lied recently (Mom circles back)
        private readonly Dictionary<QuestionCategory, int> _liesByCategory = new();

        // Last answered question's category + whether the answer was evasive
        private QuestionCategory _lastCategory;
        private bool             _lastWasEvasive;

        public QuestionDirector(List<QuestionData> allQuestions, GameSession session)
        {
            _allQuestions = allQuestions;
            _session      = session;
        }

        public QuestionData SelectQuestion(int turnIndex)
        {
            // Scripted hooks
            if (turnIndex == 0)                        return GetScripted("q_opener");
            if (turnIndex == _session.MaxTurns - 1)    return GetScripted("q_closer");

            int tier = TierForTurn(turnIndex);

            var candidates = FilterEligible(tier);
            if (candidates.Count == 0)
                candidates = FilterEligible(-1); // relax tier if pool thin

            // Pool exhausted (e.g. PANICKED 20-turn mode) — reset and replay
            if (candidates.Count == 0)
            {
                ResetPool();
                candidates = FilterEligible(tier);
                if (candidates.Count == 0)
                    candidates = FilterEligible(-1);
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException(
                    "Question pool exhausted — add more questions to questions.json.");

            var selected = WeightedRandom(candidates);
            _usedIds.Add(selected.id);
            return selected;
        }

        // Clears the used-ID set but preserves scripted questions so they are never replayed.
        private void ResetPool()
        {
            var scripted = new HashSet<string>(
                _allQuestions.Where(q => q.isScripted).Select(q => q.id));
            _usedIds.RemoveWhere(id => !scripted.Contains(id));
        }

        public void RecordLie(QuestionCategory category)
        {
            _liesByCategory.TryGetValue(category, out int n);
            _liesByCategory[category] = n + 1;
        }

        /// Call after each player response so the director knows what was just answered.
        public void RecordLastAnswer(QuestionCategory category, ResponseType type)
        {
            _lastCategory   = category;
            _lastWasEvasive = type == ResponseType.DEFLECT || type == ResponseType.LIE;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private List<QuestionData> FilterEligible(int tier)
        {
            return _allQuestions
                .Where(q => !q.isScripted
                         && (tier < 0 || q.tier == tier)
                         && !_usedIds.Contains(q.id)
                         && _session.FactsSatisfied(q.RequiredFactsDict)
                         && !_session.FactsForbidden(q.ForbiddenFactsDict))
                .ToList();
        }

        private QuestionData GetScripted(string id)
        {
            var q = _allQuestions.FirstOrDefault(q => q.id == id)
                 ?? throw new InvalidOperationException(
                        $"Scripted question '{id}' missing from questions.json.");
            _usedIds.Add(id);
            return q;
        }

        private QuestionData WeightedRandom(List<QuestionData> pool)
        {
            float[] weights = new float[pool.Count];
            float   total   = 0f;

            for (int i = 0; i < pool.Count; i++)
            {
                float w = 1f;

                // Mom circles back to categories where the player has lied
                if (_liesByCategory.TryGetValue(pool[i].CategoryEnum, out int lies))
                    w += lies * 0.5f;

                // Mom nudges on the same subject after a deflect or lie (but not too hard —
                // +1.2 weight so it's preferred but not guaranteed, and only for the next turn)
                if (_lastWasEvasive && pool[i].CategoryEnum == _lastCategory)
                    w += 1.2f;

                // "Are you sure?" loop: high Worry + low Suspicion → probe more
                if (_session.Worry > 60f && _session.Suspicion < 40f)
                    w += 0.4f;

                weights[i] = w;
                total      += w;
            }

            float roll       = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative) return pool[i];
            }

            return pool[pool.Count - 1];
        }

        // Tier thresholds scale with session length so difficulty ramps at the same
        // relative pace regardless of how many turns the player chose.
        //   First ~30 % of non-scripted turns  → tier 1  (safe topics)
        //   Middle ~40 %                        → tier 2  (touchier issues)
        //   Last ~30 %                          → tier 3  (callbacks, heavy topics)
        private int TierForTurn(int turn)
        {
            int inner = _session.MaxTurns - 2; // non-scripted turns (excl. opener + closer)
            if (inner <= 0) return 1;
            float pct = (float)(turn - 1) / inner; // 0..1 position in non-scripted span
            if (pct < 0.30f) return 1;
            if (pct < 0.70f) return 2;
            return 3;
        }
    }
}

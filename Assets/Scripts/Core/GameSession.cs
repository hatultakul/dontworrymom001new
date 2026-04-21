using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontWorryMom
{
    /// <summary>
    /// Central state container for one phone call session.
    /// Plain C# class — no MonoBehaviour dependency.
    /// </summary>
    public class GameSession
    {
        // ── Meters ──────────────────────────────────────────────────────────
        public float Worry      { get; private set; }
        public float Suspicion  { get; private set; }

        // ── Progress ─────────────────────────────────────────────────────────
        public int TurnIndex    { get; private set; } = 0;
        public int MaxTurns     { get; private set; }
        public int TruthCount   { get; private set; } = 0;
        public int RngSeed      { get; }

        // ── Fact dictionary ──────────────────────────────────────────────────
        private readonly Dictionary<string, bool> _facts = new();
        public IReadOnlyDictionary<string, bool> Facts => _facts;

        // ── Response history (for recap screen) ─────────────────────────────
        private readonly List<string> _chosenResponseIds = new();
        public IReadOnlyList<string> ChosenResponseIds => _chosenResponseIds;

        // ── Events ───────────────────────────────────────────────────────────
        public event Action<float, float> OnMetersChanged;   // (worry, suspicion)
        public event Action OnWorryLose;                     // Worry hit 100
        public event Action OnSuspicionLose;                 // Suspicion hit 100

        // ── Flags for mid-session lose triggers ──────────────────────────────
        public bool WorryCrashed     { get; private set; }
        public bool SuspicionCrashed { get; private set; }

        // ── Tone state ────────────────────────────────────────────────────────
        public ResponseTone CurrentTone  { get; private set; } = ResponseTone.VAGUE;
        public int          ToneStreak   { get; private set; } = 0;
        private ResponseTone _lastUsedTone = ResponseTone.VAGUE;

        /// Called by UI when the player switches tone mode.
        public void SetTone(ResponseTone tone) => CurrentTone = tone;

        /// Called by DialoguePresenter after each response is processed.
        public void RecordToneUsed()
        {
            if (CurrentTone == _lastUsedTone)
                ToneStreak++;
            else
                ToneStreak = 1;
            _lastUsedTone = CurrentTone;
        }

        // ────────────────────────────────────────────────────────────────────
        public GameSession(int? seed = null, int maxTurns = 10,
                           float initialWorry = 40f, float initialSuspicion = 10f)
        {
            MaxTurns   = maxTurns;
            Worry      = initialWorry;
            Suspicion  = initialSuspicion;
            RngSeed    = seed ?? Environment.TickCount;
            UnityEngine.Random.InitState(RngSeed);
        }

        // ── Meter mutation ───────────────────────────────────────────────────
        public void ApplyWorryDelta(int delta)
        {
            Worry = Mathf.Clamp(Worry + delta, 0f, 100f);
            OnMetersChanged?.Invoke(Worry, Suspicion);

            if (Worry >= 100f && !WorryCrashed)
            {
                WorryCrashed = true;
                OnWorryLose?.Invoke();
            }
        }

        public void ApplySuspicionDelta(int delta)
        {
            Suspicion = Mathf.Clamp(Suspicion + delta, 0f, 100f);
            OnMetersChanged?.Invoke(Worry, Suspicion);

            if (Suspicion >= 100f && !SuspicionCrashed)
            {
                SuspicionCrashed = true;
                OnSuspicionLose?.Invoke();
            }
        }

        // ── Fact management ──────────────────────────────────────────────────
        public void SetFact(string key, bool value) => _facts[key] = value;

        public bool? GetFact(string key) =>
            _facts.TryGetValue(key, out bool v) ? v : (bool?)null;

        /// <summary>
        /// Returns true if every entry in <paramref name="required"/> matches the session facts.
        /// An empty dictionary always returns true.
        /// </summary>
        public bool FactsSatisfied(Dictionary<string, bool> required)
        {
            if (required == null || required.Count == 0) return true;
            foreach (var kvp in required)
            {
                bool? current = GetFact(kvp.Key);
                if (!current.HasValue || current.Value != kvp.Value)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if ANY entry in <paramref name="forbidden"/> matches the session facts.
        /// A null or empty dictionary always returns false (nothing forbidden).
        /// </summary>
        public bool FactsForbidden(Dictionary<string, bool> forbidden)
        {
            if (forbidden == null || forbidden.Count == 0) return false;
            foreach (var kvp in forbidden)
            {
                bool? current = GetFact(kvp.Key);
                if (current.HasValue && current.Value == kvp.Value)
                    return true;
            }
            return false;
        }

        // ── Turn / truth tracking ────────────────────────────────────────────
        public void IncrementTurn()    => TurnIndex++;
        public void RecordTruth()      => TruthCount++;
        public void RecordResponse(string id) => _chosenResponseIds.Add(id);
    }
}

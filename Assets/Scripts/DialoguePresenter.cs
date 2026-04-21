using System;
using System.Collections;
using DontWorryMom.UI;
using UnityEngine;

namespace DontWorryMom
{
    /// <summary>
    /// Drives the turn loop: selects questions, presents dialogue, collects responses,
    /// applies meter deltas, and routes to the ending.
    ///
    /// Plain C# — uses a MonoBehaviour (GameManager) as coroutine host.
    /// </summary>
    public class DialoguePresenter
    {
        // ── Services ──────────────────────────────────────────────────────────
        private readonly GameSession         _session;
        private readonly QuestionDirector    _director;
        private readonly ContradictionTracker _contradictions;
        private readonly MomController       _mom;
        private readonly EndingResolver      _resolver;
        private readonly PhoneUIController   _ui;
        private readonly MonoBehaviour       _host; // coroutine runner

        // ── Per-turn state ────────────────────────────────────────────────────
        private QuestionData _currentQuestion;
        private bool         _sessionEnded      = false;
        private bool         _awaitingFollowup  = false; // true while showing a follow-up question

        // ── Timing ───────────────────────────────────────────────────────────
        private const float TypingDelay    = 1.4f;  // simulated Mom typing duration
        private const float ReplyDelay     = 0.9f;  // pause after player picks

        public event Action<EndingType> OnSessionEnded;

        // ─────────────────────────────────────────────────────────────────────

        public DialoguePresenter(
            GameSession session,
            QuestionDirector director,
            ContradictionTracker contradictions,
            MomController mom,
            EndingResolver resolver,
            PhoneUIController ui,
            MonoBehaviour host)
        {
            _session        = session;
            _director       = director;
            _contradictions = contradictions;
            _mom            = mom;
            _resolver       = resolver;
            _ui             = ui;
            _host           = host;

            // Wire up immediate-loss events
            _session.OnWorryLose      += () => _host.StartCoroutine(ImmediateLose(EndingType.MomIsSpiralizing));
            _session.OnSuspicionLose  += () => _host.StartCoroutine(ImmediateLose(EndingType.Caught));

            // Wire up contradiction events for UI feedback (Mom's reaction line)
            _contradictions.OnContradiction += severity =>
            {
                AudioManager.Instance?.PlayContradiction();
                _host.StartCoroutine(ShowContradictionReply(severity));
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        public void StartSession() =>
            _host.StartCoroutine(RunTurn());

        // ─────────────────────────────────────────────────────────────────────
        // TURN LOOP
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator RunTurn()
        {
            if (_sessionEnded) yield break;

            _currentQuestion = _director.SelectQuestion(_session.TurnIndex);
            _ui.SetTurnIndex(_session.TurnIndex);

            // ── Mom typing indicator ─────────────────────────────────────────
            _ui.ShowTypingIndicator(true);
            yield return new WaitForSeconds(TypingDelay);
            _ui.ShowTypingIndicator(false);

            // ── Mom's bubble ─────────────────────────────────────────────────
            bool typingDone = false;
            _ui.SpawnMomBubble(_currentQuestion.promptText, () => typingDone = true);
            yield return new WaitUntil(() => typingDone);

            // ── Optional follow-up bubble (mom keeps talking before player can respond) ──
            if (!string.IsNullOrEmpty(_currentQuestion.followupText))
            {
                yield return new WaitForSeconds(0.35f);
                _ui.ShowTypingIndicator(true);
                yield return new WaitForSeconds(0.9f);
                _ui.ShowTypingIndicator(false);
                bool followupDone = false;
                _ui.SpawnMomBubble(_currentQuestion.followupText, () => followupDone = true);
                yield return new WaitUntil(() => followupDone);
            }

            // Small pause before responses appear
            yield return new WaitForSeconds(0.3f);

            // ── Show player responses ─────────────────────────────────────────
            _ui.ShowResponses(_currentQuestion.responses, interactive: true);
        }

        // Called by UI when the player picks a response (main or follow-up)
        public void OnResponseChosen(ResponseData response)
        {
            if (_sessionEnded) return;
            if (_awaitingFollowup)
            {
                _awaitingFollowup = false;
                _host.StartCoroutine(ProcessFollowupResponse(response));
            }
            else
            {
                _host.StartCoroutine(ProcessResponse(response));
            }
        }

        private IEnumerator ProcessResponse(ResponseData response)
        {
            // ── Player bubble ────────────────────────────────────────────────
            _ui.SpawnPlayerBubble(response.text);

            // ── VAGUE DEFLECT: fact immunity — no commitment, no contradiction risk ──
            bool factImmune = _session.CurrentTone == ResponseTone.VAGUE &&
                              response.TypeEnum    == ResponseType.DEFLECT;

            // ── Contradiction check (may add suspicion + fire event) ──────────
            bool contradicted = _contradictions.ProcessResponse(_currentQuestion, response,
                                                                factImmune);

            // ── Apply tone-modified deltas (tone read from session, not response) ──
            var (wd, sd) = ToneModifier.Apply(response, _currentQuestion.CategoryEnum, _session);

            // ── Record tone streak AFTER Apply reads the current streak ────────
            _session.RecordToneUsed();

            // Final question counts double for ending calculation
            bool isFinal = _session.TurnIndex == _session.MaxTurns - 1;
            int  mult    = isFinal ? 2 : 1;

            _session.ApplyWorryDelta(wd * mult);
            _session.ApplySuspicionDelta(sd * mult);

            // ── Track truth count ─────────────────────────────────────────────
            if (response.TypeEnum == ResponseType.TRUTH)
                _session.RecordTruth();

            // ── Lie tracking for director weighting ──────────────────────────
            if (response.TypeEnum == ResponseType.LIE)
                _director.RecordLie(_currentQuestion.CategoryEnum);

            // ── Subject-continuity hint for next question selection ───────────
            _director.RecordLastAnswer(_currentQuestion.CategoryEnum, response.TypeEnum);

            _session.RecordResponse(response.momReplyId ?? "");

            // ── Update meters UI ─────────────────────────────────────────────
            _ui.UpdateMeters(_session.Worry, _session.Suspicion);

            // ── If session already ended (meter crash) — stop here ────────────
            if (_sessionEnded) yield break;

            yield return new WaitForSeconds(ReplyDelay);

            // ── Show Mom's micro-reply (unless a contradiction reply is pending) ─
            if (!contradicted && !string.IsNullOrEmpty(response.momReplyId))
            {
                var reply = _mom.GetReply(response.momReplyId);
                yield return ShowMomReply(reply.text);
            }

            // ── Optional per-response follow-up question ──────────────────────
            // Mom probes deeper based on this specific answer, before the next turn.
            // The player responds via ProcessFollowupResponse (routed by the flag).
            if (!_sessionEnded && !string.IsNullOrEmpty(response.followupQuestion) &&
                response.FollowupResponsesRuntime != null && response.FollowupResponsesRuntime.Length > 0)
            {
                yield return ShowMomReply(response.followupQuestion);
                _awaitingFollowup = true;
                _ui.ShowResponses(response.FollowupResponsesRuntime, interactive: true);
                yield break; // flow continues in ProcessFollowupResponse
            }

            // ── Advance turn ──────────────────────────────────────────────────
            _session.IncrementTurn();
            _ui.SetTurnIndex(_session.TurnIndex);

            if (_session.TurnIndex >= _session.MaxTurns)
                EndSession();
            else
                _host.StartCoroutine(RunTurn());
        }

        // Handles the player's answer to a follow-up question.
        // Applies simple deltas and advances the turn — no contradiction tracking,
        // no tone modifier, no fact recording (follow-ups are intentionally lightweight).
        private IEnumerator ProcessFollowupResponse(ResponseData followup)
        {
            _ui.SpawnPlayerBubble(followup.text);

            _session.ApplyWorryDelta(followup.worryDelta);
            _session.ApplySuspicionDelta(followup.suspicionDelta);
            _ui.UpdateMeters(_session.Worry, _session.Suspicion);

            if (_sessionEnded) yield break;

            yield return new WaitForSeconds(ReplyDelay);

            if (!string.IsNullOrEmpty(followup.momReplyId))
            {
                var reply = _mom.GetReply(followup.momReplyId);
                yield return ShowMomReply(reply.text);
            }

            _session.IncrementTurn();
            _ui.SetTurnIndex(_session.TurnIndex);

            if (_session.TurnIndex >= _session.MaxTurns)
                EndSession();
            else
                _host.StartCoroutine(RunTurn());
        }

        private IEnumerator ShowMomReply(string text)
        {
            _ui.ShowTypingIndicator(true);
            yield return new WaitForSeconds(0.8f);
            _ui.ShowTypingIndicator(false);

            bool done = false;
            _ui.SpawnMomBubble(text, () => done = true);
            yield return new WaitUntil(() => done);
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator ShowContradictionReply(ContradictionSeverity severity)
        {
            yield return new WaitForSeconds(0.5f);
            var reply = _mom.GetContradictionReply(severity);
            yield return ShowMomReply(reply.text);
        }

        private IEnumerator ImmediateLose(EndingType ending)
        {
            if (_sessionEnded) yield break;
            _sessionEnded = true;
            yield return new WaitForSeconds(1.5f);
            ShowEnding(ending);
        }

        private void EndSession()
        {
            if (_sessionEnded) return;
            _sessionEnded = true;

            var ending = _resolver.Resolve(_session);
            ShowEnding(ending);
        }

        private void ShowEnding(EndingType ending)
        {
            _ui.HideResponses();
            _ui.ShowEnding(EndingResolver.Title(ending), EndingResolver.Body(ending), ending);
            OnSessionEnded?.Invoke(ending);
        }
    }
}

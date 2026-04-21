using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DontWorryMom
{
    /// <summary>
    /// Picks the appropriate Mom reply line given an event or ending state.
    /// Falls back gracefully if a specific reply id is missing.
    /// </summary>
    public class MomController
    {
        private readonly List<MomReplyData> _replies;

        public MomController(List<MomReplyData> replies)
        {
            _replies = replies;
        }

        /// <summary>Look up a specific reply by id (set per-response in the JSON).</summary>
        public MomReplyData GetReply(string replyId)
        {
            return _replies.FirstOrDefault(r => r.id == replyId)
                ?? GetContextReply("fallback");
        }

        /// <summary>Pick a random reply with matching context tag.</summary>
        public MomReplyData GetContextReply(string context)
        {
            var options = _replies.Where(r => r.context == context).ToList();

            if (options.Count == 0)
                return new MomReplyData { id = "_fallback", text = "Mmhm." };

            return options[Random.Range(0, options.Count)];
        }

        public MomReplyData GetContradictionReply(ContradictionSeverity severity)
        {
            string ctx = severity == ContradictionSeverity.Hard
                ? "hard_contradiction"
                : "soft_contradiction";
            return GetContextReply(ctx);
        }

        public MomReplyData GetEndingReply(EndingType ending)
        {
            string ctx = ending switch
            {
                EndingType.SteadyDaughter   => "ending_steady",
                EndingType.MomIsSpiralizing  => "ending_spiraling",
                EndingType.Caught            => "ending_caught",
                EndingType.TheTruth          => "ending_truth",
                _                            => "ending_steady"
            };
            return GetContextReply(ctx);
        }
    }
}

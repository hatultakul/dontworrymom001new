using System;

namespace DontWorryMom
{
    [Serializable]
    public class MomReplyData
    {
        public string id;
        public string text;

        /// <summary>
        /// Semantic context tag used for fallback lookup.
        /// Examples: "soft_contradiction", "hard_contradiction",
        ///           "ending_steady", "ending_spiraling", "ending_caught", "ending_truth",
        ///           "fallback"
        /// </summary>
        public string context;
    }

    [Serializable]
    public class MomReplyDatabase
    {
        public MomReplyData[] replies;
    }
}

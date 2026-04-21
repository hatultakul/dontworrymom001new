using System;
using System.Collections.Generic;

namespace DontWorryMom
{
    [Serializable]
    public class QuestionData
    {
        // --- Serialized ---
        public string      id;
        public string      promptText;
        public string      followupText;   // optional second mom bubble shown before player responds
        public string      category;       // "HEALTH" | "WORK" | "MONEY" | "LOVE" | "HOME" | "FRIENDS" | "FUTURE"
        public int         tier;           // 1 = Q1-3, 2 = Q4-7, 3 = Q8-10
        public bool        isScripted;     // true for q_opener and q_closer
        public ResponseData[] responses;
        public string[]    factTags;       // fact keys this question can establish / contradict
        public FactEntry[] requiredFacts;   // session facts that must be set for this Q to be eligible
        public FactEntry[] forbiddenFacts;  // if any of these facts match, this Q is ineligible

        // --- Runtime-only ---
        [NonSerialized] public QuestionCategory CategoryEnum;
        [NonSerialized] public Dictionary<string, bool> RequiredFactsDict;
        [NonSerialized] public Dictionary<string, bool> ForbiddenFactsDict;

        public void RuntimeInit()
        {
            CategoryEnum = (QuestionCategory)Enum.Parse(
                typeof(QuestionCategory), category, ignoreCase: true);

            RequiredFactsDict = new Dictionary<string, bool>();
            if (requiredFacts != null)
                foreach (var e in requiredFacts)
                    RequiredFactsDict[e.key] = e.value;

            ForbiddenFactsDict = new Dictionary<string, bool>();
            if (forbiddenFacts != null)
                foreach (var e in forbiddenFacts)
                    ForbiddenFactsDict[e.key] = e.value;

            if (responses != null)
                foreach (var r in responses)
                    r.RuntimeInit();
        }
    }

    [Serializable]
    public class QuestionDatabase
    {
        public QuestionData[] questions;
    }
}

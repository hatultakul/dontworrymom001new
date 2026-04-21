using System;
using System.Collections.Generic;

namespace DontWorryMom
{
    [Serializable]
    public class ResponseData
    {
        // --- Serialized fields (JSON-safe) ---
        public string      text;
        public string      type;              // "TRUTH" | "LIE" | "DEFLECT"
        public string      tone;              // "CONFIDENT" | "VAGUE" | "OVERSHARE"
        public int         worryDelta;
        public int         suspicionDelta;
        public FactEntry[] factsSet;
        public string      momReplyId;

        // Follow-up question triggered by this specific answer.
        // followupResponses uses a FLAT type to avoid Unity serializer depth-limit errors
        // (ResponseData[] would be self-referential → depth 10 exceeded).
        public string                 followupQuestion;   // Mom's follow-up question text
        public FollowupResponseData[] followupResponses;  // player options (flat, no recursion)

        // --- Runtime-only ---
        [NonSerialized] public ResponseType  TypeEnum;
        [NonSerialized] public ResponseTone  ToneEnum;
        [NonSerialized] public Dictionary<string, bool> FactsSetDict;
        [NonSerialized] public ResponseData[] FollowupResponsesRuntime; // converted at RuntimeInit

        public void RuntimeInit()
        {
            TypeEnum = (ResponseType)Enum.Parse(typeof(ResponseType), type, ignoreCase: true);
            ToneEnum = (ResponseTone)Enum.Parse(typeof(ResponseTone), tone, ignoreCase: true);

            FactsSetDict = new Dictionary<string, bool>();
            if (factsSet != null)
                foreach (var e in factsSet)
                    FactsSetDict[e.key] = e.value;

            // Convert flat follow-up entries → full ResponseData so the UI can reuse ShowResponses
            if (followupResponses != null && followupResponses.Length > 0)
            {
                FollowupResponsesRuntime = new ResponseData[followupResponses.Length];
                for (int i = 0; i < followupResponses.Length; i++)
                {
                    var f = followupResponses[i];
                    var r = new ResponseData
                    {
                        text           = f.text,
                        type           = f.type,
                        tone           = string.IsNullOrEmpty(f.tone) ? "VAGUE" : f.tone,
                        worryDelta     = f.worryDelta,
                        suspicionDelta = f.suspicionDelta,
                        factsSet       = f.factsSet,
                        momReplyId     = f.momReplyId
                    };
                    r.RuntimeInit();
                    FollowupResponsesRuntime[i] = r;
                }
            }
        }
    }

    /// <summary>
    /// Flat data type for follow-up response options stored in JSON.
    /// Identical fields to ResponseData but NO self-referential arrays,
    /// so Unity's JsonUtility serialization depth limit is never hit.
    /// </summary>
    [Serializable]
    public class FollowupResponseData
    {
        public string      text;
        public string      type;
        public string      tone;
        public int         worryDelta;
        public int         suspicionDelta;
        public FactEntry[] factsSet;
        public string      momReplyId;
    }
}

// Editor-only: imports dialogue JSON files and creates ScriptableObject assets.
// Menu: Tools > Don't Worry Mom > Import Dialogue JSON
//
// Workflow:
//   1. Edit questions.json / mom_replies.json in StreamingAssets/dialogue/
//   2. Run this importer to generate SOs under Assets/Generated/Dialogue/
//   3. SOs are designer-editable in the Inspector; JSON remains the source of truth.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DontWorryMom.Editor
{
    // ── ScriptableObject wrappers ─────────────────────────────────────────────

    public class QuestionSO : ScriptableObject
    {
        public QuestionData data;
    }

    public class MomReplySO : ScriptableObject
    {
        public MomReplyData data;
    }

    // ── Importer window ───────────────────────────────────────────────────────

    public static class DialogueJsonImporter
    {
        private const string QuestionsJson  = "Assets/StreamingAssets/dialogue/questions.json";
        private const string RepliesJson    = "Assets/StreamingAssets/dialogue/mom_replies.json";
        private const string OutputFolder   = "Assets/Generated/Dialogue";

        [MenuItem("Tools/Don't Worry Mom/Import Dialogue JSON")]
        public static void ImportAll()
        {
            EnsureFolder(OutputFolder);
            EnsureFolder(OutputFolder + "/Questions");
            EnsureFolder(OutputFolder + "/Replies");

            ImportQuestions();
            ImportReplies();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DialogueImporter] Import complete.");
        }

        [MenuItem("Tools/Don't Worry Mom/Validate Fact Tags")]
        public static void ValidateFactTags()
        {
            string path = Path.Combine(Application.dataPath,
                "StreamingAssets/dialogue/questions.json");

            if (!File.Exists(path)) { Debug.LogError("questions.json not found."); return; }

            var db = JsonUtility.FromJson<QuestionDatabase>(File.ReadAllText(path));
            if (db?.questions == null) { Debug.LogError("Parse failed."); return; }

            var declared  = new HashSet<string>();
            var referenced = new HashSet<string>();
            var errors    = new List<string>();

            foreach (var q in db.questions)
            {
                foreach (var t in q.factTags ?? System.Array.Empty<string>())
                    declared.Add(t);

                foreach (var r in q.responses ?? System.Array.Empty<ResponseData>())
                {
                    foreach (var f in r.factsSet ?? System.Array.Empty<FactEntry>())
                        referenced.Add(f.key);
                }
            }

            foreach (var key in referenced)
                if (!declared.Contains(key))
                    errors.Add($"Fact key '{key}' is set by a response but not declared in any factTags.");

            if (errors.Count == 0)
                Debug.Log("[Validate] All fact tags consistent.");
            else
                foreach (var e in errors) Debug.LogWarning($"[Validate] {e}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void ImportQuestions()
        {
            string text = File.ReadAllText(QuestionsJson);
            var db = JsonUtility.FromJson<QuestionDatabase>(text);
            if (db?.questions == null) { Debug.LogError("Failed to parse questions.json."); return; }

            foreach (var q in db.questions)
            {
                string assetPath = $"{OutputFolder}/Questions/{q.id}.asset";
                var so = AssetDatabase.LoadAssetAtPath<QuestionSO>(assetPath)
                      ?? ScriptableObject.CreateInstance<QuestionSO>();
                so.data = q;
                so.name = q.id;

                if (!File.Exists(Path.Combine(Application.dataPath,
                    assetPath.Replace("Assets/", ""))))
                    AssetDatabase.CreateAsset(so, assetPath);
                else
                    EditorUtility.SetDirty(so);
            }
        }

        private static void ImportReplies()
        {
            string text = File.ReadAllText(RepliesJson);
            var db = JsonUtility.FromJson<MomReplyDatabase>(text);
            if (db?.replies == null) { Debug.LogError("Failed to parse mom_replies.json."); return; }

            foreach (var r in db.replies)
            {
                string assetPath = $"{OutputFolder}/Replies/{r.id}.asset";
                var so = AssetDatabase.LoadAssetAtPath<MomReplySO>(assetPath)
                      ?? ScriptableObject.CreateInstance<MomReplySO>();
                so.data = r;
                so.name = r.id;

                if (!File.Exists(Path.Combine(Application.dataPath,
                    assetPath.Replace("Assets/", ""))))
                    AssetDatabase.CreateAsset(so, assetPath);
                else
                    EditorUtility.SetDirty(so);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                int last = path.LastIndexOf('/');
                AssetDatabase.CreateFolder(path.Substring(0, last), path.Substring(last + 1));
            }
        }
    }
}
#endif

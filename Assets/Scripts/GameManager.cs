using System.Collections.Generic;
using System.IO;
using DontWorryMom.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace DontWorryMom
{
    /// <summary>
    /// Entry point for the game. Attach this to an empty GameObject in your scene.
    ///
    /// On Start it will:
    ///   1. Load question and reply JSON from StreamingAssets/dialogue/
    ///   2. Build the phone UI programmatically
    ///   3. Wire all core services together
    ///   4. Start the first turn
    ///
    /// No additional setup required — just hit Play.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindFirstObjectByType<GameManager>() == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }
        }

        [Header("Optional overrides (leave 0 for random seed)")]
        [SerializeField] private int forcedSeed = 0;

        // ── Loaded data ───────────────────────────────────────────────────────
        private List<QuestionData>  _questions;
        private List<MomReplyData>  _replies;

        // ── Core services ─────────────────────────────────────────────────────
        private GameSession          _session;
        private QuestionDirector     _director;
        private ContradictionTracker _contradictions;
        private MomController        _mom;
        private EndingResolver       _resolver;
        private DialoguePresenter    _presenter;
        private PhoneUIController    _ui;

        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            // 1. Load content
            if (!LoadData()) return;

            // 2. Build UI (shows difficulty screen automatically)
            SetupCanvas();

            // 3. Wait for player to choose difficulty
            _ui.OnDifficultyChosen += StartWithDifficulty;
        }

        private void StartWithDifficulty(DifficultyLevel difficulty)
        {
            _ui.OnDifficultyChosen -= StartWithDifficulty;

            int   maxTurns  = DifficultyConfig.MaxTurns(difficulty);
            float initWorry = DifficultyConfig.InitialWorry(difficulty);
            float initSusp  = DifficultyConfig.InitialSuspicion(difficulty);
            int   seed      = forcedSeed != 0 ? forcedSeed : System.Environment.TickCount;

            _session        = new GameSession(seed, maxTurns, initWorry, initSusp);
            _director       = new QuestionDirector(_questions, _session);
            _contradictions = new ContradictionTracker(_session);
            _mom            = new MomController(_replies);
            _resolver       = new EndingResolver();

            _presenter = new DialoguePresenter(
                _session, _director, _contradictions, _mom, _resolver, _ui, host: this);

            _session.OnMetersChanged += (w, s) => _ui.UpdateMeters(w, s);
            _ui.OnResponseChosen     += r    => _presenter.OnResponseChosen(r);
            _ui.OnToneChanged        += tone => _session.SetTone(tone);

            _ui.SetMaxTurns(maxTurns);
            _ui.SetTurnIndex(0);
            _ui.HideDifficultyScreen();
            _ui.StartCallTimer();
            _ui.UpdateMeters(_session.Worry, _session.Suspicion);

            _presenter.StartSession();
        }

        // ─────────────────────────────────────────────────────────────────────
        // DATA LOADING
        // ─────────────────────────────────────────────────────────────────────

        private bool LoadData()
        {
            string qPath = Path.Combine(Application.streamingAssetsPath, "dialogue", "questions.json");
            string rPath = Path.Combine(Application.streamingAssetsPath, "dialogue", "mom_replies.json");

            if (!File.Exists(qPath))
            {
                Debug.LogError($"[GameManager] questions.json not found at: {qPath}");
                return false;
            }
            if (!File.Exists(rPath))
            {
                Debug.LogError($"[GameManager] mom_replies.json not found at: {rPath}");
                return false;
            }

            var qDb = JsonUtility.FromJson<QuestionDatabase>(File.ReadAllText(qPath));
            var rDb = JsonUtility.FromJson<MomReplyDatabase>(File.ReadAllText(rPath));

            if (qDb?.questions == null || rDb?.replies == null)
            {
                Debug.LogError("[GameManager] Failed to parse dialogue JSON.");
                return false;
            }

            _questions = new List<QuestionData>(qDb.questions);
            _replies   = new List<MomReplyData>(rDb.replies);

            // Runtime init (parse enum strings, build dictionaries)
            foreach (var q in _questions) q.RuntimeInit();

            Debug.Log($"[GameManager] Loaded {_questions.Count} questions, {_replies.Count} replies.");
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI BOOTSTRAP
        // ─────────────────────────────────────────────────────────────────────

        private void SetupCanvas()
        {
            // EventSystem (required for UI Toolkit input)
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            // AudioManager
            if (FindFirstObjectByType<AudioManager>() == null)
                new GameObject("AudioManager").AddComponent<AudioManager>();

            // UI Toolkit — PhoneUIController sets up UIDocument in Awake
            var uiObj = new GameObject("PhoneUI");
            uiObj.AddComponent<UIDocument>(); // needed before PhoneUIController.Awake queries it
            _ui = uiObj.AddComponent<PhoneUIController>();
            _ui.Build();
        }
    }
}

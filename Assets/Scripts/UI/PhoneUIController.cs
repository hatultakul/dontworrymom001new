using System;
using System.Collections;
using System.Collections.Generic;
using DontWorryMom;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DontWorryMom.UI
{
    /// <summary>
    /// Drives the portrait-phone UI via UI Toolkit (UIDocument + UXML/USS).
    /// Keeps the same public API as the old UGUI version so DialoguePresenter is unchanged.
    /// Call Build() once after the component is added.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PhoneUIController : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────
        public event Action<ResponseData>    OnResponseChosen;
        public event Action<ResponseTone>    OnToneChanged;
        public event Action<DifficultyLevel> OnDifficultyChosen;

        // ── Tone active classes ───────────────────────────────────────────────
        private static readonly string[] ToneActiveClass =
            { "tone-active-overconfident", "tone-active-neutral", "tone-active-hesitant" };

        // ── Meter fill VisualElements ─────────────────────────────────────────
        private VisualElement _worryNormal, _worryDanger;
        private VisualElement _susNormal,   _susDanger;
        private Label         _worryLabel,  _susLabel;
        private VisualElement _dangerBanner;
        private Label         _dangerBannerText;

        // ── Meter state (for danger pulse & animation) ─────────────────────
        private float     _worryValue;
        private float     _susValue;
        private Coroutine _worryAnim, _susAnim;

        // ── Other UI refs ─────────────────────────────────────────────────────
        private Label         _timer;
        private Label         _turnLabel;
        private VisualElement _typingIndicator;
        private VisualElement[] _dots = new VisualElement[3];
        private ScrollView    _chatScroll;
        private Button[]        _toneBtns   = new Button[3];
        private VisualElement[] _toneStrips = new VisualElement[3];
        private VisualElement _responseButtons;
        private VisualElement _endingPanel;
        private Label         _endingVerdict;
        private Label         _endingTitle;
        private Label         _endingBody;

        // ── Difficulty panel ──────────────────────────────────────────────────
        private VisualElement _difficultyPanel;
        private VisualElement _responsePanel;

        // ── State ─────────────────────────────────────────────────────────────
        private int       _activeToneIdx   = 1; // NEUTRAL default
        private int       _maxTurns        = 10;
        private float     _callStart;
        private bool      _awaitingInput;
        private bool      _difficultyShowing;
        private bool      _typewriterSkip;
        private Coroutine _typewriterCo;
        private Action    _typewriterOnComplete;
        private Label     _typewriterLabel;
        private string    _typewriterFull;
        private bool      _endingShowing;

        // ── Type / Tone helpers ───────────────────────────────────────────────
        private static Color TypeColor(ResponseType t) => t switch
        {
            ResponseType.TRUTH   => new Color(0.200f, 0.840f, 0.400f),
            ResponseType.LIE     => new Color(0.920f, 0.320f, 0.260f),
            ResponseType.DEFLECT => new Color(0.750f, 0.650f, 0.180f),
            _                    => Color.gray
        };

        private static string TypeLabel(ResponseType t) => t switch
        {
            ResponseType.TRUTH   => "TRUTH",
            ResponseType.LIE     => "LIE",
            ResponseType.DEFLECT => "DEFLECT",
            _                    => "?"
        };

        // ─────────────────────────────────────────────────────────────────────
        // AWAKE — wire UIDocument before Build() is called
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            var doc = GetComponent<UIDocument>();

            // PanelSettings: try Resources first, create at runtime if missing
            var settings = Resources.Load<PanelSettings>("UI/PhoneUIPanelSettings");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode           = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(540, 960);
                settings.screenMatchMode     = PanelScreenMatchMode.MatchWidthOrHeight;
                settings.match               = 1.0f;

                // Assign default runtime theme so UI renders correctly
#if UNITY_EDITOR
                var tss = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                    "Packages/com.unity.ui/PackageResources/StyleSheets/Themes/UnityDefaultRuntimeTheme.tss");
                if (tss != null) settings.themeStyleSheet = tss;
#endif
            }
            doc.panelSettings = settings;

            // Load UXML + USS from Resources
            var tree = Resources.Load<VisualTreeAsset>("UI/PhoneUI");
            if (tree == null) { Debug.LogError("[PhoneUI] PhoneUI.uxml not found in Resources/UI/"); return; }
            doc.visualTreeAsset = tree;

            var uss = Resources.Load<StyleSheet>("UI/PhoneUI");
            if (uss != null) doc.rootVisualElement.styleSheets.Add(uss);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC BUILD
        // ─────────────────────────────────────────────────────────────────────

        public void Build()
        {
            _callStart = Time.time;

            var root  = GetComponent<UIDocument>().rootVisualElement;

            // Center the phone frame in the panel
            root.style.flexDirection  = FlexDirection.Column;
            root.style.alignItems     = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.width          = new StyleLength(new Length(100, LengthUnit.Percent));
            root.style.height         = new StyleLength(new Length(100, LengthUnit.Percent));

            // Query meter fills + labels
            _worryNormal     = root.Q("worry-normal");
            _worryDanger     = root.Q("worry-danger");
            _susNormal       = root.Q("sus-normal");
            _susDanger       = root.Q("sus-danger");
            _worryLabel      = root.Q<Label>("worry-label");
            _susLabel        = root.Q<Label>("sus-label");
            _dangerBanner    = root.Q("danger-banner");
            _dangerBannerText = root.Q<Label>("danger-banner-text");

            // Query labels / chrome
            _timer         = root.Q<Label>("timer");
            _turnLabel     = root.Q<Label>("turn-label");
            _typingIndicator = root.Q("typing-indicator");
            for (int i = 0; i < 3; i++) _dots[i] = root.Q($"dot-{i}");

            // Chat
            _chatScroll = root.Q<ScrollView>("chat-scroll");

            // Seed the chat area with a "call started" header
            var header = new Label("CALL STARTED");
            header.AddToClassList("chat-call-header");
            _chatScroll.contentContainer.Add(header);

            // Response panel
            _responsePanel   = root.Q("response-panel");
            _responseButtons = root.Q("response-buttons");

            // Tone buttons — inject a colored strip as first child of each
            Color[] toneStripColors =
            {
                new Color(0.18f, 0.48f, 0.95f), // OVERCONFIDENT — blue
                new Color(0.67f, 0.67f, 0.78f), // NEUTRAL       — grey
                new Color(0.90f, 0.57f, 0.12f), // HESITANT      — orange
            };
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                _toneBtns[i] = root.Q<Button>($"tone-{i}");

                var strip = new VisualElement();
                strip.AddToClassList("tone-btn-strip");
                strip.style.backgroundColor = new StyleColor(toneStripColors[i]);
                strip.style.opacity = 0f;
                _toneBtns[i].Insert(0, strip);
                _toneStrips[i] = strip;

                _toneBtns[i].clicked += () =>
                {
                    _activeToneIdx = captured;
                    UpdateToneVisuals();
                    ResponseTone[] tones = { ResponseTone.CONFIDENT, ResponseTone.VAGUE, ResponseTone.OVERSHARE };
                    OnToneChanged?.Invoke(tones[captured]);
                };
            }
            UpdateToneVisuals();

            // Ending panel
            _endingPanel   = root.Q("ending-panel");
            _endingVerdict = root.Q<Label>("ending-verdict");
            _endingTitle   = root.Q<Label>("ending-title");
            _endingBody    = root.Q<Label>("ending-body");

            // Difficulty panel + strip colors
            _difficultyPanel = root.Q("difficulty-panel");
            Color[] stripColors =
            {
                new Color(0.18f, 0.72f, 0.35f), // CHILL    — green
                new Color(0.90f, 0.78f, 0.25f), // TENSE    — yellow
                new Color(0.90f, 0.55f, 0.13f), // ANXIOUS  — orange
                new Color(0.87f, 0.19f, 0.19f), // PANICKED — red
            };
            for (int i = 0; i < 4; i++)
            {
                int captured = i;
                var strip = root.Q($"diff-strip-{i}");
                if (strip != null) strip.style.backgroundColor = new StyleColor(stripColors[i]);
                var btn = root.Q<Button>($"diff-{i}");
                if (btn != null)
                    btn.clicked += () => FireDifficulty(captured);
            }

            ShowDifficultyScreen();

            // Schedule dot animation
            root.schedule.Execute(AnimateDots).Every(16);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DIALOGUE PRESENTER API
        // ─────────────────────────────────────────────────────────────────────

        public void SetTurnIndex(int turn)
        {
            if (_turnLabel != null) _turnLabel.text = $"Q {turn + 1}/{_maxTurns}";
        }

        public void SetMaxTurns(int max)  => _maxTurns = max;
        public void StartCallTimer()      => _callStart = Time.time;

        public void ShowDifficultyScreen()
        {
            _difficultyShowing = true;
            if (_difficultyPanel != null) _difficultyPanel.style.display = DisplayStyle.Flex;
            if (_responsePanel   != null) _responsePanel.style.display   = DisplayStyle.None;
        }

        public void HideDifficultyScreen()
        {
            _difficultyShowing = false;
            if (_difficultyPanel != null) _difficultyPanel.style.display = DisplayStyle.None;
            if (_responsePanel   != null) _responsePanel.style.display   = DisplayStyle.Flex;
        }

        private void FireDifficulty(int idx)
        {
            DifficultyLevel d = (DifficultyLevel)idx;
            HideDifficultyScreen();
            OnDifficultyChosen?.Invoke(d);
        }

        public void ShowTypingIndicator(bool show)
        {
            if (_typingIndicator == null) return;
            if (show)
            {
                // Always remove + re-append so indicator is at the end of the chat flow
                _typingIndicator.RemoveFromHierarchy();
                _chatScroll.contentContainer.Add(_typingIndicator);
                _typingIndicator.style.display = DisplayStyle.Flex;
                ScrollToBottom();
            }
            else
            {
                // Remove entirely so it takes no space and doesn't displace bubbles
                _typingIndicator.RemoveFromHierarchy();
            }
        }

        public void SpawnMomBubble(string text, Action onComplete)
        {
            AudioManager.Instance?.PlayBubble();
            var (bubble, label) = CreateMomBubble("");
            _chatScroll.contentContainer.Add(bubble);
            ScrollToBottom();

            if (_typewriterCo != null)
            {
                StopCoroutine(_typewriterCo);
                if (_typewriterLabel != null) _typewriterLabel.text = _typewriterFull; // snap old label to full text
                var pending = _typewriterOnComplete;
                _typewriterOnComplete = null;
                _typewriterLabel      = null;
                _typewriterFull       = null;
                pending?.Invoke();
            }
            _typewriterSkip       = false;
            _typewriterOnComplete = onComplete;
            _typewriterLabel      = label;
            _typewriterFull       = text;
            _typewriterCo         = StartCoroutine(TypewriterCo(label, text, onComplete));
        }

        public void SpawnPlayerBubble(string text)
        {
            var (bubble, _) = CreatePlayerBubble(text);
            _chatScroll.contentContainer.Add(bubble);
            ScrollToBottom();
        }

        public void ShowResponses(ResponseData[] responses, bool interactive)
        {
            _responseButtons.Clear();

            for (int i = 0; i < responses.Length; i++)
            {
                var r = responses[i];
                var btn = BuildResponseButton(r, i + 1);
                _responseButtons.Add(btn);

                if (interactive)
                {
                    int captured = i;
                    ResponseData capturedR = r;
                    btn.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (!_awaitingInput) return;
                        _awaitingInput = false;
                        HideResponses();
                        AudioManager.Instance?.PlayClick();
                        OnResponseChosen?.Invoke(capturedR);
                    });
                }
                else
                {
                    btn.SetEnabled(false);
                }
            }

            _awaitingInput = interactive;
        }

        public void HideResponses()
        {
            _awaitingInput = false;
            _responseButtons.Clear();
        }

        public void UpdateMeters(float worry, float suspicion)
        {
            AnimateMeterTo(ref _worryAnim, _worryNormal, _worryDanger,
                           ref _worryValue, worry);
            AnimateMeterTo(ref _susAnim,   _susNormal,   _susDanger,
                           ref _susValue,   suspicion);

            UpdateDangerWarning(worry, suspicion);
        }

        private void UpdateDangerWarning(float worry, float suspicion)
        {
            bool wCrit = worry     > 80f;
            bool sCrit = suspicion > 80f;

            // Meter labels
            if (_worryLabel != null)
            {
                _worryLabel.text = wCrit ? "⚠ WORRY" : "WORRY";
                if (wCrit) _worryLabel.AddToClassList("meter-label-critical");
                else       _worryLabel.RemoveFromClassList("meter-label-critical");
            }
            if (_susLabel != null)
            {
                _susLabel.text = sCrit ? "⚠ SUSP." : "SUSPICION";
                if (sCrit) _susLabel.AddToClassList("meter-label-critical");
                else       _susLabel.RemoveFromClassList("meter-label-critical");
            }

            // Banner
            if (_dangerBanner == null) return;
            if (!wCrit && !sCrit)
            {
                _dangerBanner.style.display = DisplayStyle.None;
                return;
            }

            _dangerBanner.style.display = DisplayStyle.Flex;
            if (wCrit && sCrit)
                _dangerBannerText.text = "⚠  WORRY + SUSPICION CRITICAL — NEXT SPIKE ENDS THE CALL";
            else if (wCrit)
                _dangerBannerText.text = "⚠  WORRY CRITICAL — NEXT SPIKE ENDS THE CALL";
            else
                _dangerBannerText.text = "⚠  SUSPICION CRITICAL — NEXT SPIKE ENDS THE CALL";
        }

        public void ShowEnding(string title, string body, EndingType ending)
        {
            if (_endingPanel == null) return;

            bool isWin = ending == EndingType.SteadyDaughter || ending == EndingType.TheTruth;

            // Verdict line
            if (_endingVerdict != null)
            {
                _endingVerdict.text = isWin ? "CALL ENDED" : "CALL FAILED";
                _endingVerdict.RemoveFromClassList("ending-verdict-win");
                _endingVerdict.RemoveFromClassList("ending-verdict-lose");
                _endingVerdict.AddToClassList(isWin ? "ending-verdict-win" : "ending-verdict-lose");
            }

            // Title
            _endingTitle.text = title;
            _endingTitle.RemoveFromClassList("ending-title-win");
            _endingTitle.RemoveFromClassList("ending-title-lose");
            _endingTitle.AddToClassList(isWin ? "ending-title-win" : "ending-title-lose");

            // Body
            _endingBody.text = body;
            _endingBody.RemoveFromClassList("ending-body-win");
            _endingBody.RemoveFromClassList("ending-body-lose");
            _endingBody.AddToClassList(isWin ? "ending-body-win" : "ending-body-lose");

            // Panel background
            _endingPanel.RemoveFromClassList("ending-panel-win");
            _endingPanel.RemoveFromClassList("ending-panel-lose");
            _endingPanel.AddToClassList(isWin ? "ending-panel-win" : "ending-panel-lose");

            _endingPanel.style.display = DisplayStyle.Flex;
            _endingShowing = true;
            AudioManager.Instance?.PlayEnding();
        }

        // ─────────────────────────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            // Call timer
            if (_timer != null && !_endingShowing)
            {
                float e = Time.time - _callStart;
                _timer.text = $"{(int)(e / 60f)}:{(int)(e % 60f):00}";
            }

            // Danger zone pulse
            PulseDanger(_worryDanger, _worryValue);
            PulseDanger(_susDanger,   _susValue);

            if (Keyboard.current == null) return;

            // Any key restarts after ending
            if (_endingShowing && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            // Difficulty screen: 1-4 to choose
            if (_difficultyShowing)
            {
                Key[] diffKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
                for (int i = 0; i < diffKeys.Length; i++)
                    if (Keyboard.current[diffKeys[i]].wasPressedThisFrame)
                        FireDifficulty(i);
                return;
            }

            if (_awaitingInput)
            {
                if (Keyboard.current[Key.Q].wasPressedThisFrame) SelectTone(0);
                if (Keyboard.current[Key.W].wasPressedThisFrame) SelectTone(1);
                if (Keyboard.current[Key.E].wasPressedThisFrame) SelectTone(2);

                Key[] numKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
                var btns = _responseButtons.Children();
                int idx = 0;
                foreach (var child in btns)
                {
                    if (idx < numKeys.Length && Keyboard.current[numKeys[idx]].wasPressedThisFrame)
                        child.SendEvent(new ClickEvent());
                    idx++;
                }
            }

            // Skip typewriter
            if (_typewriterCo != null && Keyboard.current.anyKey.wasPressedThisFrame)
                _typewriterSkip = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BUBBLE BUILDERS
        // ─────────────────────────────────────────────────────────────────────

        private (VisualElement root, Label label) CreateMomBubble(string text)
        {
            var root = new VisualElement();
            root.AddToClassList("mom-bubble");

            var strip = new VisualElement();
            strip.AddToClassList("mom-bubble-strip");
            root.Add(strip);

            var lbl = new Label(text);
            lbl.AddToClassList("mom-bubble-text");
            root.Add(lbl);

            // Fade in
            root.style.opacity = 0f;
            root.schedule.Execute(() => FadeIn(root)).ExecuteLater(0);

            return (root, lbl);
        }

        private (VisualElement root, Label label) CreatePlayerBubble(string text)
        {
            var root = new VisualElement();
            root.AddToClassList("player-bubble");

            var lbl = new Label(text);
            lbl.AddToClassList("player-bubble-text");
            root.Add(lbl);

            root.style.opacity = 0f;
            root.schedule.Execute(() => FadeIn(root)).ExecuteLater(0);

            return (root, lbl);
        }

        // ─────────────────────────────────────────────────────────────────────
        // RESPONSE BUTTON BUILDER
        // ─────────────────────────────────────────────────────────────────────

        private VisualElement BuildResponseButton(ResponseData data, int keyNum)
        {
            Color typeCol = TypeColor(data.TypeEnum);

            var btn = new VisualElement();
            btn.AddToClassList("response-btn");

            // Left type-color strip (8px wide — improved from 5px)
            var strip = new VisualElement();
            strip.AddToClassList("response-btn-strip");
            strip.style.backgroundColor = new StyleColor(typeCol);
            btn.Add(strip);

            // TYPE badge (TRUTH / LIE / DEFLECT) with color
            var badge = new Label(TypeLabel(data.TypeEnum));
            badge.AddToClassList("response-btn-badge");
            badge.style.color = new StyleColor(typeCol);
            btn.Add(badge);

            // Response text
            var text = new Label(data.text);
            text.AddToClassList("response-btn-text");
            btn.Add(text);

            // Key number hint (1-4) — fixes the bug where keyLabel was passed but never displayed
            var key = new Label(keyNum.ToString());
            key.AddToClassList("response-btn-key");
            btn.Add(key);

            return btn;
        }

        // ─────────────────────────────────────────────────────────────────────
        // TONE VISUALS
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateToneVisuals()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_toneBtns[i] == null) continue;
                bool active = i == _activeToneIdx;
                foreach (var cls in ToneActiveClass)
                    _toneBtns[i].RemoveFromClassList(cls);
                if (active)
                    _toneBtns[i].AddToClassList(ToneActiveClass[i]);
                if (_toneStrips[i] != null)
                    _toneStrips[i].style.opacity = active ? 1f : 0f;
            }
        }

        private void SelectTone(int idx)
        {
            _activeToneIdx = idx;
            UpdateToneVisuals();
            ResponseTone[] tones = { ResponseTone.CONFIDENT, ResponseTone.VAGUE, ResponseTone.OVERSHARE };
            OnToneChanged?.Invoke(tones[idx]);
        }

        // ─────────────────────────────────────────────────────────────────────
        // METER ANIMATION
        // ─────────────────────────────────────────────────────────────────────

        private void AnimateMeterTo(ref Coroutine co, VisualElement normal, VisualElement danger,
                                    ref float stored, float target)
        {
            float from = stored;
            stored = target;

            if (co != null) StopCoroutine(co);
            co = StartCoroutine(MeterLerpCo(normal, danger, from, target));
        }

        private IEnumerator MeterLerpCo(VisualElement normal, VisualElement danger, float from, float to)
        {
            float elapsed = 0f;
            const float dur = 0.40f;
            float fromT = Mathf.Clamp01(from / 100f);
            float toT   = Mathf.Clamp01(to   / 100f);

            while (elapsed < dur)
            {
                float t = Mathf.Lerp(fromT, toT, elapsed / dur);
                ApplyMeterT(normal, danger, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            ApplyMeterT(normal, danger, toT);
        }

        private static void ApplyMeterT(VisualElement normal, VisualElement danger, float t)
        {
            if (normal != null)
                normal.style.width = new StyleLength(Length.Percent(Mathf.Clamp01(t / 0.80f) * 80f));
            if (danger != null)
                danger.style.width = new StyleLength(Length.Percent(Mathf.Clamp01((t - 0.80f) / 0.20f) * 20f));
        }

        private static void PulseDanger(VisualElement danger, float value)
        {
            if (danger == null || value <= 80f) return;
            float fill  = (value - 80f) / 20f;
            float pulse = (Mathf.Sin(Time.time * 5.5f) + 1f) * 0.5f;
            var cold    = new Color(1.00f, 0.12f, 0.08f);
            var hot     = new Color(1.00f, 0.45f, 0.30f);
            danger.style.backgroundColor = new StyleColor(Color.Lerp(cold, hot, pulse * fill));
        }

        // ─────────────────────────────────────────────────────────────────────
        // SCROLL
        // ─────────────────────────────────────────────────────────────────────

        private void ScrollToBottom()
        {
            _chatScroll?.schedule.Execute(() =>
            {
                if (_chatScroll == null) return;
                _chatScroll.scrollOffset = new Vector2(0f,
                    _chatScroll.contentContainer.layout.height);
            }).ExecuteLater(1);
        }

        // ─────────────────────────────────────────────────────────────────────
        // COROUTINES
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator TypewriterCo(Label target, string full, Action onComplete)
        {
            target.text = "";
            const float charDelay = 1f / 35f;
            int sinceSound = 0;

            foreach (char c in full)
            {
                if (_typewriterSkip) break;
                target.text += c;
                sinceSound++;
                if (sinceSound >= 3)
                {
                    AudioManager.Instance?.PlayTypingChar();
                    sinceSound = 0;
                }
                yield return new WaitForSeconds(charDelay);
                ScrollToBottom();
            }

            target.text           = full;
            _typewriterCo         = null;
            _typewriterOnComplete = null;
            _typewriterLabel      = null;
            _typewriterFull       = null;
            onComplete?.Invoke();
        }

        private void FadeIn(VisualElement el)
        {
            StartCoroutine(FadeInCo(el, 0.18f));
        }

        private IEnumerator FadeInCo(VisualElement el, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                if (el == null) yield break;
                el.style.opacity = t / duration;
                t += Time.deltaTime;
                yield return null;
            }
            if (el != null) el.style.opacity = 1f;
        }

        // ─────────────────────────────────────────────────────────────────────
        // DOT ANIMATION (scheduled, runs every 16ms)
        // ─────────────────────────────────────────────────────────────────────

        private void AnimateDots()
        {
            if (_typingIndicator == null || _typingIndicator.style.display == DisplayStyle.None)
                return;

            float t = Time.time * 3.2f;
            for (int i = 0; i < _dots.Length; i++)
            {
                if (_dots[i] == null) continue;
                float wave  = Mathf.Sin(t - i * 1.05f);
                float scale = Mathf.Lerp(0.55f, 1.45f, (wave + 1f) * 0.5f);
                float alpha = Mathf.Lerp(0.30f, 1.00f, (wave + 1f) * 0.5f);
                _dots[i].style.scale   = new StyleScale(new Scale(new Vector2(scale, scale)));
                _dots[i].style.opacity = alpha;
            }
        }
    }
}

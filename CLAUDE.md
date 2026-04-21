# Don't Worry, Mom — Developer Reference

## What This Is
A Unity 6.3 narrative game. One phone call. Ten questions from a worried mother.
The player navigates Worry and Suspicion meters through careful (or reckless) responses.

## Core Loop
1. Mom asks a question → typewriter effect, left-side chat bubble
2. Player picks 1 of 4 responses → keyboard 1–4 or click
3. Tone modifier applies before meter deltas (CONFIDENT / VAGUE / OVERSHARE)
4. Mom reacts with a micro-reply line from `mom_replies.json`
5. Repeat for 10 questions → ending screen

## Meter Rules (ToneModifier.cs — full Type × Tone matrix)

| Tone / Type   | Worry                       | Suspicion                                   |
|---------------|-----------------------------|---------------------------------------------|
| CONFIDENT TRUTH   | ×0.65                   | −3 (−5 if factual cat; +10 if S>65)         |
| CONFIDENT LIE     | ×0.70                   | +12 (+8 if S>50; −3 if factual cat)         |
| CONFIDENT DEFLECT | base+10                 | +7                                          |
| VAGUE TRUTH       | ×1.25 (×1.3 if W>60)   | −7 (−3 extra if emotional cat)              |
| VAGUE LIE         | ×1.1                    | +4 (+6 if S>55)                             |
| VAGUE DEFLECT     | base+6                  | +4 — grants fact immunity (no contradiction)|
| OVERSHARE TRUTH   | ×1.8 (×1.2 if emotional; ×1.2 if W>60) | −15                        |
| OVERSHARE LIE     | ×0.85                   | +9 (+7 if S>45)                             |
| OVERSHARE DEFLECT | ×1.2                    | −10 (+6 late-game if turn≥7)                |

**Streak penalty** (same tone 3+ consecutive turns, checked pre-apply):
- CONFIDENT streak ≥3: worry×1.1, susp+8
- VAGUE streak ≥3: worry×1.25
- OVERSHARE streak ≥3: susp+7

**Category sensitivity** (emotional = HEALTH/FRIENDS/LOVE; factual = WORK/MONEY/HOME):
- VAGUE TRUTH + emotional: susp −3 extra
- OVERSHARE TRUTH + emotional: worry ×1.2 extra
- CONFIDENT TRUTH + factual: susp −2 extra
- CONFIDENT LIE + factual: susp −3 extra

**Contradictions:**
- Hard contradiction (fact-flip): **+30 Suspicion**
- Soft contradiction (type-switch TRUTH↔LIE within category): **+15 Suspicion**
- 3+ consecutive LIEs in same category: **−10 Suspicion** (consistency bonus, fires exactly at streak=3)

**Turn 9 (the closer, 0-indexed) applies ×2 multiplier to both wd and sd after tone/type processing.**

## Endings (GDD §4)
| Ending           | Trigger                                  |
|------------------|------------------------------------------|
| Steady Daughter  | W<50 AND S<50 (WIN)                      |
| Mom Is Spiraling | W≥80 or Worry hit 100 during session     |
| Caught           | S≥80 or Suspicion hit 100 during session |
| The Truth        | W<30 AND S<20 AND ≥7 TRUTH answers (secret) |

## Questions (GDD §7)
- Turn 0 = `q_opener` (scripted)
- Turn 9 = `q_closer` (scripted)
- Turns 1–2: tier-1 pool (eat, sleep, apartment, friends)
- Turns 3–6: tier-2 pool (work, bills, dating, noise, cousin, future-plan)
- Turns 7–8: tier-3 pool (mall, ex, rent, school, doctor) — some require facts

## UI Design (GDD §10)
- Portrait phone, letterboxed on desktop
- Canvas: 540×960 reference, `matchWidthOrHeight=1.0` (height-match → side letterbox)
- Phone frame: fixed 540×960, centred anchor (0.5,0.5)
- Mom bubbles: left, dark background, 4px teal left strip
- Player bubbles: right, blue background
- Meters: fill bars (green→red color shift) + pulse — **no numbers shown**
- Response tone revealed as colored left strip on each button (always visible)
- Keyboard Q/W/E = switch tone (CONFIDENT/VAGUE/OVERSHARE)
- Keyboard 1–4 = pick response
- Any key skips typewriter; any key after ending restarts

## Architecture (Namespace: DontWorryMom)
```
GameManager.cs          bootstrap: RuntimeInitializeOnLoadMethod, builds canvas
PhoneUIController.cs    all UI built in code — no scene setup needed
DialoguePresenter.cs    coroutine turn loop (plain C#, GameManager is coroutine host)
AudioManager.cs         procedural sounds via AudioClip.Create (singleton)
GameSession.cs          meters, facts, turn index
QuestionDirector.cs     selects questions (tier + lie-weighting)
ContradictionTracker.cs detects fact-flips, fires events
MomController.cs        picks reply lines from JSON
EndingResolver.cs       maps session state → EndingType
```

## Content Files
- `Assets/StreamingAssets/dialogue/questions.json`   — 17 questions
- `Assets/StreamingAssets/dialogue/mom_replies.json` — ~80 reply lines

## Known Layout Rules (UI Toolkit)
- ScrollToBottom: `_chatScroll.scrollOffset = new Vector2(0, contentContainer.layout.height)` deferred via `.schedule.Execute(...).ExecuteLater(1)` so layout resolves first
- `PhoneUIPanelSettings.asset` (Resources/UI/) must have `UnityDefaultRuntimeTheme` TSS assigned — created via reflection on `PanelSettingsCreator.GetFirstThemeOrCreateDefaultTheme()` in editor
- Root VisualElement flex-centers the phone frame: `alignItems=Center, justifyContent=Center, width/height=100%`
- `resolvedStyle.width` on `position:absolute` fills returns 0 until rendered; use `style.width.value` to inspect fill percentage in code

## Project Root
`c:/Users/dor/Desktop/Don't Worry, Mom 001/Don't Worry, Mom 001/`

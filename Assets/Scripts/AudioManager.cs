using UnityEngine;

namespace DontWorryMom
{
    /// <summary>
    /// Procedurally-generated audio — no audio files needed.
    /// All sounds are synthesized from sine waves, noise, and envelopes.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int SampleRate = 22050;

        private AudioSource _src;
        private AudioClip   _clipBubble;
        private AudioClip   _clipTypingChar;
        private AudioClip   _clipClick;
        private AudioClip   _clipContradiction;
        private AudioClip   _clipEnding;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake  = false;
            _src.spatialBlend = 0f; // 2D audio

            _clipBubble        = MakeBubble();
            _clipTypingChar    = MakeTypingChar();
            _clipClick         = MakeClick();
            _clipContradiction = MakeContradiction();
            _clipEnding        = MakeEnding();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void PlayBubble()        => _src.PlayOneShot(_clipBubble,        0.55f);
        public void PlayTypingChar()    => _src.PlayOneShot(_clipTypingChar,     0.25f);
        public void PlayClick()         => _src.PlayOneShot(_clipClick,          0.70f);
        public void PlayContradiction() => _src.PlayOneShot(_clipContradiction,  0.65f);
        public void PlayEnding()        => _src.PlayOneShot(_clipEnding,         0.60f);

        // ── Sound Factories ───────────────────────────────────────────────────

        /// Soft notification ding: 880 Hz sine, 0.35 s exponential decay
        private static AudioClip MakeBubble()
        {
            const float dur  = 0.35f;
            const float freq = 880f;
            int n = Mathf.RoundToInt(SampleRate * dur);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Exp(-8f * t / dur);
                data[i]   = Mathf.Sin(Mathf.PI * 2 * freq * t) * env * 0.38f
                           + Mathf.Sin(Mathf.PI * 2 * freq * 2 * t) * env * 0.08f;
            }
            return BuildClip("bubble", data);
        }

        /// Very short white-noise click for typewriter characters
        private static AudioClip MakeTypingChar()
        {
            const float dur = 0.018f;
            int n = Mathf.RoundToInt(SampleRate * dur);
            float[] data = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / n;
                float env = Mathf.Exp(-t * 18f);
                data[i]   = (float)(rng.NextDouble() * 2 - 1) * env * 0.28f;
            }
            return BuildClip("typingchar", data);
        }

        /// Crisp UI click: band-limited noise burst
        private static AudioClip MakeClick()
        {
            const float dur = 0.06f;
            int n = Mathf.RoundToInt(SampleRate * dur);
            float[] data = new float[n];
            var rng = new System.Random(42);
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / n;
                float env = Mathf.Exp(-t * 25f) * (1f - t * 0.5f);
                data[i]   = (float)(rng.NextDouble() * 2 - 1) * env * 0.35f;
            }
            return BuildClip("click", data);
        }

        /// Two-tone descending alert: 660 Hz → 440 Hz
        private static AudioClip MakeContradiction()
        {
            const float dur = 0.55f;
            int n = Mathf.RoundToInt(SampleRate * dur);
            float[] data = new float[n];
            float half = dur / 2f;
            for (int i = 0; i < n; i++)
            {
                float t     = (float)i / SampleRate;
                float freq  = t < half ? 660f : 440f;
                float local = t < half ? t : t - half;
                float env   = Mathf.Exp(-local / half * 5.5f);
                data[i]     = Mathf.Sin(Mathf.PI * 2 * freq * t) * env * 0.32f;
            }
            return BuildClip("contradiction", data);
        }

        /// Warm ending chord: 440 + 554 + 659 Hz (A major triad), 0.9 s fade
        private static AudioClip MakeEnding()
        {
            const float dur = 0.90f;
            int n = Mathf.RoundToInt(SampleRate * dur);
            float[] data = new float[n];
            float[] freqs = { 440f, 554.4f, 659.3f };
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Exp(-3.5f * t / dur);
                float s   = 0f;
                foreach (float f in freqs)
                    s += Mathf.Sin(Mathf.PI * 2 * f * t);
                data[i] = s / freqs.Length * env * 0.35f;
            }
            return BuildClip("ending", data);
        }

        private static AudioClip BuildClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

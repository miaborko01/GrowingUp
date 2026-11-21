using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Perceived loudness per sound category. Written by the AgeController (part of the PLAYER),
    /// read by every ProceduralAudio source. The sound sources in the world never change; only how
    /// loudly the current Virtual Subjectiveness perceives them.
    /// </summary>
    public static class PerceptionMix
    {
        public static float Nature = 1f;
        public static float Music = 1f;
        public static float Sfx = 1f;

        public static float Gain(ProceduralAudio.Category c)
        {
            switch (c)
            {
                case ProceduralAudio.Category.Nature: return Nature;
                case ProceduralAudio.Category.Music: return Music;
                default: return Sfx;
            }
        }
    }

    /// <summary>
    /// Synthesises a simple looping (or one-shot) sound at startup so the project needs no audio files.
    /// Replace the generated clip with a real AudioClip any time: if the AudioSource already has a clip
    /// and "Keep Existing Clip" is ticked, nothing is generated.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralAudio : MonoBehaviour
    {
        public enum Kind { Wind, River, Birds, ChildMusic, AdultDrone, DoorLocked, DoorClose, Splash, Chirp, Whoosh, Chime, Crunch, Snap, Bling, Laughter, ChildVoices, Rustle, Scrape, Shimmer, Owl, Buzz }
        public enum Category { Nature, Music, Sfx }

        public Kind kind = Kind.Wind;
        public Category category = Category.Nature;
        [Range(0f, 1f)] public float baseVolume = 0.5f;
        public int seed = 1;
        public bool keepExistingClip = true;
        public bool playOnStart = true;
        [Tooltip("If off, another script (e.g. AgeController for music) sets the AudioSource volume.")]
        public bool controlVolume = true;

        const int Rate = 22050;
        AudioSource _src;

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            if (!(keepExistingClip && _src.clip != null))
            {
                _src.clip = Generate(kind, seed);
            }
            _src.playOnAwake = false;
            if (controlVolume) _src.volume = baseVolume * PerceptionMix.Gain(category);
        }

        void Start()
        {
            if (playOnStart && _src.clip != null) _src.Play();
        }

        void Update()
        {
            if (controlVolume) _src.volume = baseVolume * PerceptionMix.Gain(category);
        }

        public void PlayOneShot()
        {
            if (_src == null) _src = GetComponent<AudioSource>();
            _src.volume = baseVolume * PerceptionMix.Gain(category);
            _src.Play();
        }

        // ------------------------------------------------------------------ synthesis
        public static AudioClip Generate(Kind kind, int seed)
        {
            var rng = new System.Random(seed * 7919 + (int)kind * 104729);
            float[] d;
            bool loop = true;
            switch (kind)
            {
                case Kind.Wind: d = Wind(rng, 12f); break;
                case Kind.River: d = River(rng, 8f); break;
                case Kind.Birds: d = Birds(rng, 14f); break;
                case Kind.ChildMusic: d = ChildMusic(rng); break;
                case Kind.AdultDrone: d = Drone(rng, 16f); break;
                case Kind.DoorLocked: d = DoorLocked(rng); loop = false; break;
                case Kind.DoorClose: d = DoorClose(rng); loop = false; break;
                case Kind.Splash: d = Splash(rng); loop = false; break;
                case Kind.Chirp: d = Chirp(rng); loop = false; break;
                case Kind.Whoosh: d = Whoosh(rng); loop = false; break;
                case Kind.Chime: d = Chime(rng); loop = false; break;
                case Kind.Crunch: d = Crunch(rng); loop = false; break;
                case Kind.Bling: d = Bling(rng); loop = false; break;
                case Kind.Laughter: d = Laughter(rng, 4.5f, 3); loop = false; break;
                case Kind.ChildVoices: d = Babble(rng, 9f); break;
                case Kind.Rustle: d = Rustle(rng); loop = false; break;
                case Kind.Scrape: d = Scrape(rng); loop = false; break;
                case Kind.Shimmer: d = Shimmer(rng); loop = false; break;
                case Kind.Owl: d = Owl(rng, 16f); break;
                case Kind.Buzz: d = Buzz(rng, 4f); break;
                default: d = Snap(rng); loop = false; break;
            }
            if (loop && kind != Kind.ChildMusic) d = MakeSeamless(d, (int)(Rate * 0.5f));
            Normalize(d, kind == Kind.Birds ? 0.6f : 0.8f);
            var clip = AudioClip.Create("Procedural_" + kind + "_" + seed, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static float Rnd(System.Random r) => (float)r.NextDouble();
        static float Noise(System.Random r) => Rnd(r) * 2f - 1f;

        static float[] Wind(System.Random r, float secs)
        {
            int n = (int)(secs * Rate); var d = new float[n];
            float lp = 0f, lp2 = 0f;
            float p1 = Rnd(r) * 6f, p2 = Rnd(r) * 6f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float gust = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / secs * 2f + p1) + 0.15f * Mathf.Sin(2f * Mathf.PI * t / secs * 5f + p2);
                float cut = 0.01f + 0.03f * gust;
                lp += (Noise(r) - lp) * cut;
                lp2 += (lp - lp2) * cut;
                d[i] = lp2 * gust;
            }
            return d;
        }

        static float[] River(System.Random r, float secs)
        {
            int n = (int)(secs * Rate); var d = new float[n];
            float lp = 0f, hpPrev = 0f, hp = 0f, babble = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = Noise(r);
                lp += (x - lp) * 0.25f;            // low-pass ~1.5 kHz
                hp = 0.97f * (hp + lp - hpPrev);   // remove rumble
                hpPrev = lp;
                babble += (Noise(r) - babble) * 0.002f; // slow random gurgle
                d[i] = hp * (0.7f + 2.5f * Mathf.Abs(babble));
            }
            return d;
        }

        static float[] Birds(System.Random r, float secs)
        {
            int n = (int)(secs * Rate); var d = new float[n];
            float t0 = 0.3f + Rnd(r);
            while (t0 < secs - 1.2f)
            {
                int chirps = 2 + r.Next(5);
                float baseF = 2400f + Rnd(r) * 2600f;
                float sweep = (Rnd(r) - 0.3f) * 2500f;
                float len = 0.05f + Rnd(r) * 0.1f;
                float gap = len + 0.03f + Rnd(r) * 0.08f;
                for (int c = 0; c < chirps; c++)
                {
                    int start = (int)((t0 + c * gap) * Rate);
                    int ln = (int)(len * Rate);
                    float ph = 0f;
                    for (int k = 0; k < ln && start + k < n; k++)
                    {
                        float u = (float)k / ln;
                        float f = baseF + sweep * u + 300f * Mathf.Sin(u * 40f);
                        ph += 2f * Mathf.PI * f / Rate;
                        float env = Mathf.Sin(Mathf.PI * u);
                        d[start + k] += Mathf.Sin(ph) * env * env * 0.5f;
                    }
                }
                t0 += chirps * gap + 0.6f + Rnd(r) * 2.2f;
            }
            return d;
        }

        static void Pluck(float[] d, float startSec, float freq, float durSec, float amp, bool soft)
        {
            int s = (int)(startSec * Rate), ln = (int)(durSec * Rate);
            for (int k = 0; k < ln; k++)
            {
                int i = (s + k) % d.Length; // wrap tail around the loop point
                float t = (float)k / Rate;
                float env = Mathf.Exp(-t * (soft ? 3f : 6f)) * Mathf.Min(1f, t * 200f);
                float w = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t) + 0.12f * Mathf.Sin(6f * Mathf.PI * freq * t);
                d[i] += w * env * amp;
            }
        }

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        static float[] ChildMusic(System.Random r)
        {
            // Bright, bouncy music-box tune in C major pentatonic, 112 bpm, 8 bars of 4/4.
            float beat = 60f / 112f; int bars = 8;
            int n = (int)(bars * 4 * beat * Rate); var d = new float[n];
            int[] scale = { 72, 74, 76, 79, 81, 84, 86, 88 };
            int[] chords = { 48, 53, 55, 48, 45, 53, 55, 48 }; // C F G C Am F G C roots
            int idx = 3;
            for (int b = 0; b < bars; b++)
            {
                int root = chords[b];
                for (int q = 0; q < 4; q++)
                {
                    float tb = (b * 4 + q) * beat;
                    Pluck(d, tb, Midi(root + (q % 2 == 0 ? 0 : 7)), beat * 1.5f, 0.22f, true); // bass
                    for (int e = 0; e < 2; e++)
                    {
                        if (Rnd(r) < 0.2f && !(q == 0 && e == 0)) continue;
                        idx = Mathf.Clamp(idx + r.Next(-2, 3), 0, scale.Length - 1);
                        Pluck(d, tb + e * beat * 0.5f, Midi(scale[idx]), beat * 1.2f, 0.18f, false);
                    }
                }
            }
            return d;
        }

        static float[] Drone(System.Random r, float secs)
        {
            int n = (int)(secs * Rate); var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float lfo = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / secs * 2f);
                float s = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f
                        + Mathf.Sin(2f * Mathf.PI * 55.4f * t) * 0.4f
                        + Mathf.Sin(2f * Mathf.PI * 65.4f * t) * 0.25f * lfo   // minor third
                        + Mathf.Sin(2f * Mathf.PI * 110.3f * t) * 0.1f;
                lp += (Noise(r) - lp) * 0.02f;
                d[i] = s * lfo + lp * 0.6f;
            }
            return d;
        }

        static float[] DoorLocked(System.Random r)
        {
            // Three dull wooden knocks + metallic latch rattle: the door is stuck.
            int n = (int)(1.1f * Rate); var d = new float[n];
            float[] hits = { 0f, 0.16f, 0.30f };
            foreach (var h in hits)
            {
                int s = (int)(h * Rate); float lp = 0f;
                for (int k = 0; k < (int)(0.18f * Rate) && s + k < n; k++)
                {
                    float t = (float)k / Rate;
                    lp += (Noise(r) - lp) * 0.15f;
                    float env = Mathf.Exp(-t * 35f);
                    d[s + k] += (Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.9f + lp * 1.2f) * env;
                }
                // latch click
                for (int k = 0; k < (int)(0.03f * Rate) && s + k < n; k++)
                {
                    float t = (float)k / Rate;
                    d[s + k] += Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 180f) * 0.35f;
                }
            }
            return d;
        }

        static float[] DoorClose(System.Random r)
        {
            // short hinge creak, a heavy wooden thud and the latch clicking shut
            int n = (int)(1.0f * Rate); var d = new float[n];
            for (int k = 0; k < (int)(0.35f * Rate); k++)
            {
                float t = (float)k / Rate, f = 420f + 260f * Mathf.Sin(t * 9f);
                d[k] += Mathf.Sin(2f * Mathf.PI * f * t + Mathf.Sin(t * 180f) * 2f) * 0.18f * Mathf.Sin(Mathf.PI * t / 0.35f);
            }
            int s0 = (int)(0.38f * Rate); float lp = 0f;
            for (int k = 0; k < (int)(0.5f * Rate) && s0 + k < n; k++)
            {
                float t = (float)k / Rate; lp += (Noise(r) - lp) * 0.08f;
                d[s0 + k] += (Mathf.Sin(2f * Mathf.PI * 62f * t) * 1.2f + lp * 1.5f) * Mathf.Exp(-t * 14f);
            }
            int s1 = (int)(0.47f * Rate);
            for (int k = 0; k < (int)(0.05f * Rate) && s1 + k < n; k++)
            {
                float t = (float)k / Rate;
                d[s1 + k] += Mathf.Sin(2f * Mathf.PI * 2600f * t) * Mathf.Exp(-t * 120f) * 0.4f;
            }
            return d;
        }

        static float[] Splash(System.Random r)
        {
            int n = (int)(0.9f * Rate); var d = new float[n]; float lp = 0f, hp = 0f, prev = 0f;
            for (int k = 0; k < n; k++)
            {
                float t = (float)k / Rate; lp += (Noise(r) - lp) * 0.35f; hp = 0.95f * (hp + lp - prev); prev = lp;
                d[k] += hp * Mathf.Exp(-t * 6f) * Mathf.Min(1f, t * 80f);
            }
            for (int b = 0; b < 14; b++) // droplets
            {
                int s = (int)((0.08f + Rnd(r) * 0.6f) * Rate); float f = 900f + Rnd(r) * 1800f;
                for (int k = 0; k < (int)(0.05f * Rate) && s + k < n; k++)
                {
                    float t = (float)k / Rate;
                    d[s + k] += Mathf.Sin(2f * Mathf.PI * (f + 6000f * t) * t) * Mathf.Exp(-t * 90f) * 0.35f;
                }
            }
            return d;
        }

        static float[] Chirp(System.Random r)
        {
            int n = (int)(0.75f * Rate); var d = new float[n];
            for (int c = 0; c < 3; c++)
            {
                int s = (int)((0.04f + c * 0.17f) * Rate), ln = (int)(0.11f * Rate); float ph = 0f, f0 = 2800f + c * 350f;
                for (int k = 0; k < ln && s + k < n; k++)
                {
                    float u = (float)k / ln, f = f0 + 1900f * u + 250f * Mathf.Sin(u * 50f);
                    ph += 2f * Mathf.PI * f / Rate;
                    float env = Mathf.Sin(Mathf.PI * u);
                    d[s + k] += Mathf.Sin(ph) * env * env;
                }
            }
            return d;
        }

        static float[] Whoosh(System.Random r)
        {
            // airy breath swell + rising sparkling bell arpeggio: the dandelion seeds fly away
            int n = (int)(2.4f * Rate); var d = new float[n]; float lp = 0f;
            for (int k = 0; k < n; k++)
            {
                float t = (float)k / Rate; lp += (Noise(r) - lp) * (0.04f + 0.1f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 1.2f)));
                d[k] += lp * 1.6f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 1.4f)) * Mathf.Exp(-Mathf.Max(0f, t - 1.0f) * 2f);
            }
            int[] notes = { 84, 88, 91, 96, 100, 103, 108 };
            for (int i = 0; i < notes.Length; i++) Pluck(d, 0.25f + i * 0.13f, Midi(notes[i]), 1.2f, 0.16f, true);
            return d;
        }

        static float[] Chime(System.Random r)
        {
            int n = (int)(3.0f * Rate); var d = new float[n];
            int[] notes = { 72, 76, 79, 84, 88, 91, 96 };
            for (int i = 0; i < notes.Length; i++) Pluck(d, i * 0.11f, Midi(notes[i]), 2.2f, 0.2f, true);
            for (int i = 0; i < 12; i++) Pluck(d, 0.8f + Rnd(r) * 1.4f, Midi(96 + r.Next(0, 12)), 0.8f, 0.06f, false); // shimmer
            return d;
        }

        static float[] Crunch(System.Random r)
        {
            int n = (int)(0.7f * Rate); var d = new float[n];
            foreach (var h in new[] { 0.02f, 0.28f })
            {
                int s = (int)(h * Rate); float lp = 0f;
                for (int k = 0; k < (int)(0.2f * Rate) && s + k < n; k++)
                {
                    float t = (float)k / Rate; lp += (Noise(r) - lp) * 0.5f;
                    float crackle = Rnd(r) < 0.04f ? Noise(r) * 2f : 0f;
                    d[s + k] += (lp + crackle) * Mathf.Exp(-t * 18f);
                }
            }
            Pluck(d, 0.45f, Midi(88), 0.4f, 0.15f, false); // little happy "ding"
            return d;
        }

        static float[] Snap(System.Random r)
        {
            int n = (int)(0.6f * Rate); var d = new float[n];
            for (int k = 0; k < (int)(0.08f * Rate); k++)
            {
                float t = (float)k / Rate;
                d[k] += (Mathf.Sin(2f * Mathf.PI * 520f * t) + Noise(r) * 0.4f) * Mathf.Exp(-t * 60f);
            }
            Pluck(d, 0.06f, Midi(91), 0.5f, 0.25f, false);
            return d;
        }

        static float[] Bling(System.Random r)
        {
            // tiny magical "bling": two bright bell notes
            int n = (int)(1.2f * Rate); var d = new float[n];
            Pluck(d, 0f, Midi(93), 0.9f, 0.3f, true);
            Pluck(d, 0.07f, Midi(100), 0.9f, 0.25f, true);
            Pluck(d, 0.14f, Midi(105), 0.7f, 0.12f, false);
            return d;
        }

        // A sung vowel: glottal harmonics shaped by two formants (+ breath). Crude, but reads as a voice.
        static void Syllable(float[] d, System.Random r, float start, float dur, float f0, float f0End, float f1, float f2, float amp)
        {
            int s0 = (int)(start * Rate), len = (int)(dur * Rate);
            float ph = 0f, lp = 0f;
            for (int k = 0; k < len && s0 + k < d.Length; k++)
            {
                float t = (float)k / len;
                float f = Mathf.Lerp(f0, f0End, t) * (1f + 0.02f * Mathf.Sin(k * 0.002f));
                ph += f / Rate;
                float v = 0f;
                for (int h = 1; h <= 14; h++)
                {
                    float fh = f * h;
                    float g = Mathf.Exp(-Mathf.Pow((fh - f1) / 180f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((fh - f2) / 260f, 2f)) + 0.05f / h;
                    v += Mathf.Sin(2f * Mathf.PI * ph * h) * g;
                }
                lp += (Noise(r) - lp) * 0.3f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.4f)) * Mathf.Exp(-t * 1.5f);
                d[s0 + k] += (v * 0.25f + lp * 0.35f) * env * amp;
            }
        }

        static float[] Laughter(System.Random r, float secs, int kids)
        {
            // several children giggling "ha-ha-ha" at different pitches
            int n = (int)(secs * Rate); var d = new float[n];
            for (int c = 0; c < kids; c++)
            {
                float t = 0.1f + Rnd(r) * 0.6f, pitch = 330f + Rnd(r) * 170f;
                while (t < secs - 0.6f)
                {
                    int ha = 3 + r.Next(0, 5);
                    for (int i = 0; i < ha && t < secs - 0.3f; i++)
                    {
                        float p = pitch * (1f - i * 0.03f);
                        Syllable(d, r, t, 0.13f, p * 1.08f, p * 0.95f, 850f + Rnd(r) * 150f, 1300f + Rnd(r) * 300f, 0.8f);
                        t += 0.17f + Rnd(r) * 0.05f;
                    }
                    t += 0.5f + Rnd(r) * 1.2f;
                }
            }
            return d;
        }

        static float[] Babble(System.Random r, float secs)
        {
            // distant children's voices playing (loop)
            int n = (int)(secs * Rate); var d = new float[n];
            float[,] vowels = { { 750, 1200 }, { 400, 2200 }, { 300, 2700 }, { 500, 900 }, { 650, 1700 } };
            for (int c = 0; c < 4; c++)
            {
                float t = Rnd(r) * 0.8f, pitch = 280f + Rnd(r) * 160f;
                while (t < secs - 0.5f)
                {
                    int words = 2 + r.Next(0, 4);
                    for (int w = 0; w < words; w++)
                    {
                        int v = r.Next(0, 5); float dur = 0.12f + Rnd(r) * 0.15f;
                        float p = pitch * (0.9f + Rnd(r) * 0.3f);
                        Syllable(d, r, t, dur, p, p * (0.85f + Rnd(r) * 0.3f), vowels[v, 0], vowels[v, 1], 0.4f);
                        t += dur + 0.03f;
                    }
                    t += 0.3f + Rnd(r) * 1.0f;
                }
            }
            return d;
        }

        static float[] Rustle(System.Random r)
        {
            // leaves and branches pushed aside
            int n = (int)(1.4f * Rate); var d = new float[n]; float lp = 0f, hp = 0f, prev = 0f;
            for (int k = 0; k < n; k++)
            {
                float t = (float)k / n;
                float x = Noise(r); hp = 0.85f * (hp + x - prev); prev = x; lp += (hp - lp) * 0.5f;
                float crackle = Rnd(r) < 0.003f ? Noise(r) * 3f : 0f;
                float env = Mathf.Clamp01(t * 8f) * Mathf.Exp(-t * 2.5f) * (0.7f + 0.3f * Mathf.Sin(t * 40f));
                d[k] = (lp + crackle) * env;
            }
            return d;
        }

        static float[] Scrape(System.Random r)
        {
            // stone dragged over dirt
            int n = (int)(0.6f * Rate); var d = new float[n]; float lp = 0f;
            for (int k = 0; k < n; k++)
            {
                float t = (float)k / n;
                lp += (Noise(r) - lp) * 0.12f;
                float grit = Rnd(r) < 0.02f ? Noise(r) : 0f;
                d[k] = (lp * 2f + grit) * Mathf.Sin(Mathf.PI * t);
            }
            return d;
        }

        static float[] Shimmer(System.Random r)
        {
            // magical swelling shimmer for the transformation: rising bells that slowly sink and darken
            int n = (int)(8f * Rate); var d = new float[n];
            int[] up = { 72, 76, 79, 83, 86, 88, 91, 95 };
            for (int i = 0; i < 40; i++)
            {
                float t = i * 0.17f;
                int note = up[i % up.Length] - (i / 10) * 5;
                Pluck(d, t, Midi(note), 1.6f, 0.12f * (1f - i / 48f), true);
            }
            for (int k = 0; k < n; k++)
            {
                float t = (float)k / Rate;
                float pad = Mathf.Sin(2f * Mathf.PI * Midi(48) * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * Midi(55) * t) * 0.3f;
                d[k] += pad * 0.12f * Mathf.Clamp01(t / 2f) * Mathf.Clamp01((8f - t) / 2f);
            }
            return d;
        }

        static float[] Owl(System.Random r, float secs)
        {
            // tawny-owl style "hoo... hoo-hoo-hooo" twice per loop, low and breathy
            int n = (int)(secs * Rate); var d = new float[n];
            foreach (var start in new[] { 1.0f, 8.5f })
            {
                float f = 360f + Rnd(r) * 40f;
                float[] at = { 0f, 1.1f, 1.45f, 1.8f }; float[] len = { 0.6f, 0.25f, 0.25f, 0.9f };
                for (int k = 0; k < at.Length; k++)
                {
                    int s0 = (int)((start + at[k]) * Rate), ln = (int)(len[k] * Rate); float ph = 0f, lp = 0f;
                    for (int i = 0; i < ln && s0 + i < n; i++)
                    {
                        float t = (float)i / ln;
                        float ff = f * (1f + 0.04f * Mathf.Sin(t * Mathf.PI) - (k == 3 ? 0.06f * t : 0f) + 0.01f * Mathf.Sin(i * 0.004f));
                        ph += ff / Rate; lp += (Noise(r) - lp) * 0.05f;
                        float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.3f)) * (k == 3 ? Mathf.Exp(-t * 1.2f) : 1f);
                        d[s0 + i] += (Mathf.Sin(2f * Mathf.PI * ph) + 0.15f * Mathf.Sin(4f * Mathf.PI * ph) + lp * 0.6f) * env * 0.5f;
                    }
                }
            }
            return d;
        }

        static float[] Buzz(System.Random r, float secs)
        {
            // mosquito whine: thin buzzy tone ~600 Hz with drifting pitch
            int n = (int)(secs * Rate); var d = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = 610f + 45f * Mathf.Sin(t * 1.3f) + 20f * Mathf.Sin(t * 5.1f);
                ph += f / Rate; ph -= Mathf.Floor(ph);
                float saw = ph * 2f - 1f;
                d[i] = (saw * 0.6f + Mathf.Sin(2f * Mathf.PI * ph * 2f) * 0.3f) * (0.7f + 0.3f * Mathf.Sin(t * 23f));
            }
            return d;
        }

        // Cross-fades the tail into the head and drops the tail, so the loop point is click-free.
        static float[] MakeSeamless(float[] d, int fade)
        {
            fade = Mathf.Min(fade, d.Length / 4);
            int n = d.Length - fade;
            var o = new float[n];
            System.Array.Copy(d, o, n);
            for (int k = 0; k < fade; k++)
            {
                float a = (float)k / fade;
                o[k] = d[k] * a + d[n + k] * (1f - a);
            }
            return o;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0.0001f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float s = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= s;
        }
    }
}

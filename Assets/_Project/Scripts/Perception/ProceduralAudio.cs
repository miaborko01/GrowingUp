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
        public enum Kind { Wind, River, Birds, ChildMusic, AdultDrone, DoorLocked }
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
                default: d = DoorLocked(rng); loop = false; break;
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

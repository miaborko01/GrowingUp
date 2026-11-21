using System.Collections;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// The transformation (step "GrowingUp"), started when the child eats the berry.
    ///  1. The rig grows 0.2 -> 1.0 (AgeController.GrowUp), Magic blends to Grey, music/butterflies fade.
    ///  2. Meanwhile: sparkles, flashes and waves of light that go from light to dark; the mother's
    ///     voice echoes ("Go have fun, don't be late!") with children's laughter and voices.
    ///  3. The sun sets (world time of day, not the player's perception).
    ///  4. Inner monologue: "Oh... just as tasty as I remember." The player turns around (towards the
    ///     house, where the sun is going down): "Oh, it's getting late. I should go home." "It's been so
    ///     long. I wonder if the old house is still there." -> AdultReturn.
    ///
    /// PLACEHOLDER AUDIO: VO_Mother_Echo (uses the mother's clip from the intro if one is set),
    /// SFX_ChildrenLaughter, SFX_ChildrenVoices, SFX_TransformShimmer.
    /// </summary>
    public class GrowingUpSequence : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.GrowingUp;

        [Header("Timing")]
        public float growDuration = 7f;
        [Tooltip("Max seconds to wait for the player to turn around before the second line.")]
        public float turnTimeout = 10f;

        [Header("Light effects (part of the player)")]
        public ParticleSystem lightWaves;
        public ParticleSystem sparkles;
        public Gradient fxColour;
        [Tooltip("Seconds of each flash peak, relative to the grow duration (0..1).")]
        public float[] flashTimes = { 0.05f, 0.2f, 0.36f, 0.52f, 0.68f, 0.82f };

        [Header("Audio (placeholders)")]
        public AudioSource motherEcho;
        public AudioSource laughter;
        public AudioSource voices;
        public AudioSource shimmer;
        [Tooltip("The mother's line in the intro (its clip is reused for the echo).")]
        public AudioSource introMotherVoice;

        [Header("Sunset (world)")]
        public Light sun;
        public Transform house;
        public Color sunsetColour = new Color(1f, 0.58f, 0.32f);
        [Tooltip("Warm sunset light (not night): the adult walks home in the evening.")]
        public float sunsetIntensity = 0.85f;
        public float sunsetElevation = 9f;
        public float sunsetAmbient = 0.8f;
        public float sunsetSeconds = 9f;

        [Header("Lines")]
        public string echoLine = "<i>...go have fun... don't be late...</i>";
        public string tastyLine = "<i>\"Oh... just as tasty as I remember.\"</i>";
        public string lateLine = "<i>\"Oh, it's getting late. I should go home.\"</i>";
        public string houseLine = "<i>\"It's been so long. I wonder if the old house is still there.\"</i>";

        Quaternion _noonRot; Color _noonCol; float _noonInt, _noonAmb;
        bool _started, _done;

        void Start()
        {
            if (sun != null) { _noonRot = sun.transform.rotation; _noonCol = sun.color; _noonInt = sun.intensity; RenderSettings.sun = sun; }
            _noonAmb = RenderSettings.ambientIntensity;
            var story = StoryManager.Instance;
            if (story != null) story.StepChanged += OnStep;
        }

        void OnDestroy() { if (StoryManager.Instance != null) StoryManager.Instance.StepChanged -= OnStep; }

        void OnStep(StoryStep s)
        {
            if (s == StoryStep.GrowingUp && !_started) StartCoroutine(Run());
            else if (s < StoryStep.GrowingUp && (_started || _done))
            {
                // debug jump back to a child step: daylight again
                StopAllCoroutines();
                _started = _done = false;
                ApplySun(0f);
                if (ScreenOverlay.Instance != null) ScreenOverlay.Instance.Set(Color.clear);
            }
        }

        IEnumerator Run()
        {
            _started = true;
            var age = AgeController.Instance;
            if (age != null) age.GrowUp(growDuration);
            StartCoroutine(Effects());
            StartCoroutine(Sunset());
            yield return Voices();

            float wait = growDuration + 0.8f - 4.6f; // Voices() takes ~4.6 s
            if (wait > 0f) yield return new WaitForSeconds(wait);
            while (age != null && age.IsTransitioning) yield return null;

            Say(tastyLine, 3.6f);
            yield return new WaitForSeconds(4.0f);

            // wait for the player to turn around towards home (or time out)
            for (float t = 0f; t < turnTimeout; t += Time.deltaTime)
            {
                if (FacingHome(0.55f)) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            Say(lateLine, 3.6f);
            yield return new WaitForSeconds(3.9f);
            Say(houseLine, 4.4f);
            yield return new WaitForSeconds(4.6f);
            _done = true;
            if (StoryManager.Instance != null) StoryManager.Instance.Complete(StoryStep.GrowingUp);
        }

        static void Say(string line, float secs) { if (Subtitles.Instance != null) Subtitles.Instance.Show(line, secs); }

        bool FacingHome(float dot)
        {
            var age = AgeController.Instance;
            if (age == null || age.Head == null || house == null) return true;
            Vector3 f = age.Head.forward; f.y = 0f;
            Vector3 d = house.position - age.Head.position; d.y = 0f;
            return f.sqrMagnitude > 1e-4f && Vector3.Dot(f.normalized, d.normalized) > dot;
        }

        IEnumerator Voices()
        {
            if (shimmer != null) shimmer.Play();
            yield return new WaitForSeconds(0.6f);
            if (laughter != null) laughter.Play();
            if (voices != null) { voices.volume = 0f; voices.Play(); StartCoroutine(FadeVoices()); }
            yield return new WaitForSeconds(0.8f);
            PlayEcho();
            yield return new WaitForSeconds(3.2f);
            PlayEcho();
        }

        void PlayEcho()
        {
            if (motherEcho != null && motherEcho.clip == null && introMotherVoice != null) motherEcho.clip = introMotherVoice.clip;
            if (motherEcho != null && motherEcho.clip != null) motherEcho.Play();
            Say(echoLine, 2.6f); // shown always: the real line is missing until a recording is dropped in
        }

        IEnumerator FadeVoices()
        {
            float d = growDuration;
            for (float t = 0f; t < d; t += Time.deltaTime)
            {
                if (voices == null) yield break;
                float k = t / d;
                voices.volume = 0.55f * Mathf.Clamp01(k * 4f) * Mathf.Clamp01((1f - k) * 2.5f);
                yield return null;
            }
            if (voices != null) voices.Stop();
        }

        IEnumerator Effects()
        {
            var overlay = ScreenOverlay.Instance;
            var age = AgeController.Instance;
            float d = growDuration, nextWave = 0f;
            if (sparkles != null) sparkles.Play();
            if (lightWaves != null) lightWaves.Play();
            for (float t = 0f; t < d + 1.2f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / d);
                Color c = fxColour != null ? fxColour.Evaluate(k) : Color.Lerp(new Color(1f, 0.95f, 0.8f), new Color(0.1f, 0.1f, 0.2f), k);

                // flashes: short peaks, brightest at the start, darker veils at the end
                float flash = 0f;
                foreach (var ft in flashTimes)
                {
                    float u = (k - ft) * d; // seconds from the peak
                    if (u > -0.08f && u < 0.5f) flash = Mathf.Max(flash, u < 0f ? 1f + u / 0.08f : Mathf.Exp(-u * 7f));
                }
                float veil = Mathf.SmoothStep(0f, 0.35f, (k - 0.55f) / 0.45f) * Mathf.Clamp01((d + 1.2f - t) / 1.2f);
                float a = Mathf.Max(flash * Mathf.Lerp(0.75f, 0.35f, k), veil);
                if (overlay != null) overlay.Set(new Color(c.r, c.g, c.b, a));

                // follow the player's feet (the FX live under the rig, but are simulated in world space)
                if (age != null && age.Head != null)
                {
                    Vector3 h = age.Head.position;
                    if (lightWaves != null) lightWaves.transform.position = new Vector3(h.x, h.y * 0.5f, h.z);
                    if (sparkles != null) sparkles.transform.position = h;
                }
                if (t < d && t >= nextWave && lightWaves != null)
                {
                    nextWave = t + Mathf.Lerp(0.55f, 1.1f, k);
                    var ep = new ParticleSystem.EmitParams { startColor = new Color(c.r, c.g, c.b, Mathf.Lerp(0.9f, 0.5f, k)), applyShapeToPosition = true };
                    lightWaves.Emit(ep, 1);
                }
                if (sparkles != null)
                {
                    var main = sparkles.main; main.startColor = new Color(c.r, c.g, c.b, 1f);
                    var em = sparkles.emission; em.rateOverTimeMultiplier = Mathf.Lerp(90f, 0f, k);
                }
                yield return null;
            }
            if (sparkles != null) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (overlay != null) overlay.Set(Color.clear);
        }

        IEnumerator Sunset()
        {
            for (float t = 0f; t < sunsetSeconds; t += Time.deltaTime)
            {
                ApplySun(Mathf.SmoothStep(0f, 1f, t / sunsetSeconds));
                yield return null;
            }
            ApplySun(1f);
        }

        float _lastGi = -1f;
        void ApplySun(float k)
        {
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Slerp(_noonRot, SunsetRotation(), k);
                sun.color = Color.Lerp(_noonCol, sunsetColour, k);
                sun.intensity = Mathf.Lerp(_noonInt, sunsetIntensity, k);
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(_noonAmb, sunsetAmbient, k);
            if (Time.time - _lastGi > 0.5f || k >= 1f) { _lastGi = Time.time; DynamicGI.UpdateEnvironment(); }
        }

        /// The sun goes down behind the house, so turning around towards home shows the sunset.
        Quaternion SunsetRotation()
        {
            Vector3 from = house != null ? house.position : new Vector3(0f, 0f, -4f);
            Vector3 to = transform.position;
            var age = AgeController.Instance;
            if (age != null && age.Head != null) to = age.Head.position;
            Vector3 dir = to - from; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-3f) dir = Vector3.forward;
            dir.Normalize();
            dir.y = -Mathf.Tan(sunsetElevation * Mathf.Deg2Rad);
            return Quaternion.LookRotation(dir.normalized);
        }

        public void ForceComplete()
        {
            if (_done) return;
            _done = true; _started = true;
            StopAllCoroutines();
            if (sun != null) { sun.transform.rotation = SunsetRotation(); sun.color = sunsetColour; sun.intensity = sunsetIntensity; }
            RenderSettings.ambientIntensity = sunsetAmbient;
            DynamicGI.UpdateEnvironment();
            if (ScreenOverlay.Instance != null) ScreenOverlay.Instance.Set(Color.clear);
            if (sparkles != null) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (voices != null) voices.Stop();
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace GrowingUp
{
    /// <summary>
    /// Main quest 4: the berries (child, step "Berries"). The bush stands in the middle of the fairy
    /// ring. When the child comes out of the bush tunnel, glowing magical berries pop onto it and it
    /// lights up. The child picks a berry and brings it to the mouth (MouthZone) to eat it -> crunch,
    /// the step completes and the story moves to GrowingUp (GrowingUpSequence starts the transformation).
    /// </summary>
    public class BerryBush : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.Berries;

        public GrabbableItem[] berries;
        public float berryRadius = 0.022f;
        public ParticleSystem bushGlow;
        public Color berryGlow = new Color(1f, 0.25f, 0.5f);

        [Header("Placeholder sounds")]
        public AudioSource appearSound; // SFX_BerriesAppear
        public AudioSource eatSound;    // SFX_BerryCrunch

        [Header("Hook")]
        public UnityEvent onBerryEaten;

        public bool Revealed { get; private set; }
        public bool Eaten { get; private set; }
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            foreach (var b in berries) if (b != null) b.gameObject.SetActive(false);
        }

        void Start()
        {
            var story = StoryManager.Instance;
            if (story != null)
            {
                story.StepChanged += OnStep;
                if (story.Reached(StoryStep.Berries)) Reveal(false);
            }
        }

        void OnDestroy() { if (StoryManager.Instance != null) StoryManager.Instance.StepChanged -= OnStep; }

        void OnStep(StoryStep s) { if (s >= StoryStep.Berries && !Revealed) Reveal(s == StoryStep.Berries); }

        void Reveal(bool withFx)
        {
            Revealed = true;
            if (bushGlow != null && !Eaten) bushGlow.Play();
            if (withFx && appearSound != null) appearSound.Play();
            StartCoroutine(PopIn(withFx));
        }

        IEnumerator PopIn(bool animate)
        {
            for (int i = 0; i < berries.Length; i++)
            {
                var b = berries[i];
                if (b == null || b.Locked) continue;
                b.gameObject.SetActive(true);
                if (!animate) continue;
                Vector3 s = b.transform.localScale;
                b.transform.localScale = Vector3.zero;
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.25f)
                {
                    float k = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
                    b.transform.localScale = s * (t * k);
                    yield return null;
                }
                b.transform.localScale = s;
                yield return new WaitForSeconds(0.08f);
            }
        }

        void Update()
        {
            if (!Revealed) return;
            float k = 2.6f + 1.4f * Mathf.Sin(Time.time * 2.5f);
            var ageC = AgeController.Instance;
            if (ageC != null) k *= 1f - ageC.Blend; // the glow is the child's magic: gone for the adult
            foreach (var b in berries)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                var r = b.GetComponent<Renderer>();
                if (r != null) { r.GetPropertyBlock(_mpb); _mpb.SetColor("_EmissionColor", berryGlow * k); r.SetPropertyBlock(_mpb); }
            }

            if (Eaten) return;
            var story = StoryManager.Instance;
            if (story == null || !story.Is(StoryStep.Berries)) return;
            foreach (var b in berries)
            {
                if (b == null || b.Locked || !b.gameObject.activeInHierarchy) continue;
                if (MouthZone.IsAtMouth(b.transform.position, berryRadius)) { Eat(b); break; }
            }
        }

        void Eat(GrabbableItem berry)
        {
            Eaten = true;
            if (eatSound != null) { eatSound.transform.position = berry.transform.position; eatSound.Play(); }
            berry.Consume();
            if (bushGlow != null) bushGlow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Debug.Log("[Story] Berry eaten -> GrowingUp");
            if (StoryManager.Instance != null) StoryManager.Instance.Complete(StoryStep.Berries);
            onBerryEaten?.Invoke();
        }

        public void ForceComplete()
        {
            if (!Revealed) Reveal(false);
            if (Eaten) return;
            Eaten = true;
            foreach (var b in berries) if (b != null && !b.Locked) { b.gameObject.SetActive(true); b.Consume(); break; }
            if (bushGlow != null) bushGlow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}

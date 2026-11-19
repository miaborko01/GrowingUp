using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Fairy ring (child, step "FairyRing"). A circle of mushrooms with a few empty, softly glowing
    /// spots. Loose mushrooms lie around; the child carries them over and releases them near an empty
    /// spot -> they snap in upright. When the ring is complete: chime + rising light, and the berry
    /// bush lights up (next step). The completed ring stays for the adult.
    /// </summary>
    public class FairyRing : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.FairyRing;

        public Transform[] slots;
        public Renderer[] slotGlows;
        public GameObject[] slotSparkles;
        public GrabbableItem[] looseMushrooms;
        [Tooltip("Released within this horizontal distance (m) of an empty spot -> snaps in.")]
        public float snapRadius = 0.14f;
        public Color glowColor = new Color(1f, 0.8f, 0.35f);

        [Header("Feedback (placeholder sounds)")]
        public AudioSource snapSound;   // SFX_MushroomSnap
        public AudioSource chime;       // SFX_FairyChime
        public ParticleSystem completeMagic;

        public bool Complete { get; private set; }
        bool[] _filled;
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            _filled = new bool[slots.Length];
            _mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            var story = StoryManager.Instance;
            bool active = story != null && story.Is(StoryStep.FairyRing);
            PulseGlows(active);
            if (!active || Complete) return;

            foreach (var m in looseMushrooms)
            {
                if (m == null || m.Locked || m.IsHeld) continue;
                Vector3 p = m.transform.position;
                if (p.y > 0.3f || m.Body.linearVelocity.sqrMagnitude > 0.3f) continue;
                int best = -1; float bestD = snapRadius;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (_filled[i]) continue;
                    float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(slots[i].position.x, slots[i].position.z));
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0) Fill(best, m, true);
            }
        }

        void Fill(int i, GrabbableItem m, bool fx)
        {
            _filled[i] = true;
            m.LockInPlace(slots[i].position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            if (slotGlows[i] != null) slotGlows[i].enabled = false;
            if (slotSparkles != null && i < slotSparkles.Length && slotSparkles[i] != null) slotSparkles[i].SetActive(false);
            if (fx && snapSound != null) { snapSound.transform.position = slots[i].position; snapSound.Play(); }

            foreach (var f in _filled) if (!f) return;
            Complete = true;
            if (fx)
            {
                if (chime != null) chime.Play();
                if (completeMagic != null) completeMagic.Play();
            }
            if (StoryManager.Instance != null && StoryManager.Instance.Is(StoryStep.FairyRing))
                StoryManager.Instance.Complete(StoryStep.FairyRing);
        }

        void PulseGlows(bool active)
        {
            float k = active ? 0.55f + 0.45f * Mathf.Sin(Time.time * 3f) : 0.25f;
            for (int i = 0; i < slotGlows.Length; i++)
            {
                if (slotGlows[i] == null || !slotGlows[i].enabled) continue;
                slotGlows[i].GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", glowColor * (k * 2.5f));
                slotGlows[i].SetPropertyBlock(_mpb);
            }
        }

        public void ForceComplete()
        {
            if (Complete) return;
            int mi = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (_filled[i]) continue;
                while (mi < looseMushrooms.Length && (looseMushrooms[mi] == null || looseMushrooms[mi].Locked)) mi++;
                if (mi >= looseMushrooms.Length) break;
                Fill(i, looseMushrooms[mi], false);
            }
            Complete = true;
        }
    }
}

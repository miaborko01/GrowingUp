using UnityEngine;
using UnityEngine.Events;

namespace GrowingUp
{
    /// <summary>
    /// The river on the ADULT's way back. By dusk the river runs faster and heavier: the water rises and
    /// covers the child's stepping stones (driven by the age blend, so the debug toggle shows it too).
    /// The adult can't wade through it (bank wall). Logs lie on the bank: grab one and drop it into the
    /// river at the crossing -> it settles across the water as part of a bridge (guided: a faint outline
    /// shows where the next log goes). After 3 logs the bridge is done and the adult can cross.
    /// </summary>
    public class RiverBridge : MonoBehaviour
    {
        [Header("Rising, faster river (adult)")]
        public Transform water;
        public Renderer waterRenderer;
        [Tooltip("How much the water rises for the adult (m). Stepping stone tops are ~0.12 m.")]
        public float riseHeight = 0.12f;
        public float flowSpeedChild = 0.45f, flowSpeedAdult = 1.15f;
        public float rippleChild = 0.7f, rippleAdult = 1.15f;
        public float swellChild = 0.006f, swellAdult = 0.018f;
        public float foamChild = 0.12f, foamAdult = 0.2f;
        public ProceduralAudio[] riverSounds;
        public float soundBoostAdult = 1.8f;

        [Header("Bridge")]
        public GrabbableItem[] logs;
        public Transform[] slots;
        [Tooltip("Faint outline at each slot (the next one is shown as a guide).")]
        public GameObject[] guides;
        [Tooltip("Blocks the crossing until the bridge is complete (adult only).")]
        public Collider gapBlocker;
        [Tooltip("Bank walls left and right of the crossing (adult only, the river is too strong).")]
        public Collider[] bankWalls;
        public float riverZ = 13f, halfWidth = 0.92f;
        [Tooltip("Released log within this distance (m, sideways) of the crossing snaps into the bridge.")]
        public float snapRadius = 1.2f;
        public AudioSource splashSound;
        public ParticleSystem splashFx;
        public UnityEvent onBridgeComplete;

        public int Placed { get; private set; }
        public bool Done => slots != null && Placed >= slots.Length;

        MaterialPropertyBlock _mpb;
        float _waterY0, _lastBlend = -1f;
        float[] _soundBase;
        bool _grabOn = true;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (water != null) _waterY0 = water.localPosition.y;
            if (riverSounds != null)
            {
                _soundBase = new float[riverSounds.Length];
                for (int i = 0; i < riverSounds.Length; i++) if (riverSounds[i] != null) _soundBase[i] = riverSounds[i].baseVolume;
            }
            UpdateGuides();
        }

        void Update()
        {
            var age = AgeController.Instance;
            float b = age != null ? age.Blend : 0f;
            if (Mathf.Abs(b - _lastBlend) > 0.0005f) { ApplyFlood(b); _lastBlend = b; }

            bool adult = age != null && age.CurrentAge == Age.Adult && !age.IsTransitioning;
            if (gapBlocker != null) gapBlocker.enabled = adult && !Done;
            if (bankWalls != null) foreach (var w in bankWalls) if (w != null) w.enabled = adult;
            if (adult != _grabOn)
            {
                _grabOn = adult;
                foreach (var l in logs) if (l != null && !l.Locked) l.SetGrabEnabled(adult); // too heavy for the child
            }
            foreach (var g in guides) if (g != null && g.activeSelf != (adult && !Done && System.Array.IndexOf(guides, g) == Placed)) g.SetActive(adult && !Done && System.Array.IndexOf(guides, g) == Placed);

            if (!adult || Done) return;
            foreach (var l in logs)
            {
                if (l == null || l.Locked || l.IsHeld) continue;
                Vector3 p = l.transform.position;
                Vector3 slot = slots[Placed].position;
                bool overRiver = Mathf.Abs(p.z - riverZ) < halfWidth + 0.6f;
                if (overRiver && Mathf.Abs(p.x - slot.x) < snapRadius && p.y < 0.9f) { Place(l); break; }
            }
        }

        void Place(GrabbableItem log)
        {
            var slot = slots[Placed];
            log.LockInPlace(slot.position, slot.rotation);
            Placed++;
            if (splashFx != null) { splashFx.transform.position = slot.position + Vector3.up * 0.05f; splashFx.Play(); }
            if (splashSound != null) { splashSound.transform.position = slot.position; splashSound.Play(); }
            UpdateGuides();
            if (Done) onBridgeComplete?.Invoke();
        }

        void UpdateGuides()
        {
            if (guides == null) return;
            for (int i = 0; i < guides.Length; i++) if (guides[i] != null) guides[i].SetActive(false);
        }

        void ApplyFlood(float b)
        {
            if (water != null)
            {
                var lp = water.localPosition; lp.y = _waterY0 + riseHeight * b; water.localPosition = lp;
            }
            if (waterRenderer != null)
            {
                waterRenderer.GetPropertyBlock(_mpb);
                var flow = waterRenderer.sharedMaterial.GetVector("_Flow");
                flow.w = Mathf.Lerp(flowSpeedChild, flowSpeedAdult, b);
                _mpb.SetVector("_Flow", flow);
                _mpb.SetFloat("_RippleStrength", Mathf.Lerp(rippleChild, rippleAdult, b));
                _mpb.SetFloat("_SwellHeight", Mathf.Lerp(swellChild, swellAdult, b));
                _mpb.SetFloat("_FoamWidth", Mathf.Lerp(foamChild, foamAdult, b));
                waterRenderer.SetPropertyBlock(_mpb);
            }
            if (riverSounds != null)
                for (int i = 0; i < riverSounds.Length; i++)
                    if (riverSounds[i] != null) riverSounds[i].baseVolume = Mathf.Min(1f, _soundBase[i] * Mathf.Lerp(1f, soundBoostAdult, b));
        }

        /// Debug: finish the bridge instantly.
        [ContextMenu("Debug: build the bridge")]
        public void BuildNow()
        {
            foreach (var l in logs) { if (Done) break; if (l != null && !l.Locked) Place(l); }
        }
    }
}

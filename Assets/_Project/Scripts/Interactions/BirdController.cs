using System.Collections;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Feeding the bird (child, step "Bird").
    /// A small bird sits by the path and idles (hops, pecks, looks around, chirps). Seeds lie nearby.
    /// When the child holds a seed out or drops it near the bird, the bird hops over, eats it (the seed
    /// disappears), chirps happily and flies a little circle. That completes the step.
    /// </summary>
    public class BirdController : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.Bird;

        [Header("Model parts (built from primitives)")]
        public Transform model;
        public Transform head;
        public Transform wingL, wingR;

        [Header("Seeds")]
        public GrabbableItem[] seeds;
        [Tooltip("Seeds closer than this (horizontal, m) to the bird get noticed.")]
        public float noticeRadius = 0.7f;
        [Tooltip("The bird can reach seeds up to this height (held out by the tiny child).")]
        public float reachHeight = 0.3f;

        [Header("Sound (placeholder: SFX_BirdChirp)")]
        public AudioSource chirp;

        public bool Fed { get; private set; }

        Vector3 _home;
        bool _busy, _flying;
        float _nextIdle, _nextChirp;

        void Start()
        {
            _home = transform.position;
            _nextIdle = Time.time + 1f;
            _nextChirp = Time.time + Random.Range(2f, 5f);
        }

        void Update()
        {
            AnimateWings();
            if (_busy) return;

            var story = StoryManager.Instance;
            if (!Fed && story != null && story.Is(StoryStep.Bird))
            {
                var seed = FindSeed();
                if (seed != null) { StartCoroutine(GoEat(seed)); return; }
            }

            if (Time.time > _nextChirp)
            {
                _nextChirp = Time.time + Random.Range(Fed ? 3f : 4f, Fed ? 6f : 9f);
                if (chirp != null) { chirp.pitch = Random.Range(0.9f, 1.15f); chirp.Play(); }
            }
            if (Time.time > _nextIdle)
            {
                _nextIdle = Time.time + Random.Range(1.2f, 2.8f);
                int r = Random.Range(0, 3);
                if (r == 0) StartCoroutine(Peck(1));
                else if (r == 1) StartCoroutine(LookAround());
                else
                {
                    Vector3 off = Random.insideUnitSphere * 0.2f; off.y = 0f;
                    StartCoroutine(HopTo(_home + off));
                }
            }
        }

        GrabbableItem FindSeed()
        {
            GrabbableItem best = null; float bestD = noticeRadius;
            foreach (var s in seeds)
            {
                if (s == null || !s.gameObject.activeInHierarchy || s.Locked) continue;
                Vector3 d = s.transform.position - transform.position;
                if (d.y > reachHeight) continue;
                d.y = 0f;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = s; }
            }
            return best;
        }

        IEnumerator GoEat(GrabbableItem seed)
        {
            _busy = true;
            for (int hops = 0; hops < 12 && seed != null && seed.gameObject.activeInHierarchy; hops++)
            {
                Vector3 to = seed.transform.position - transform.position; to.y = 0f;
                if (to.magnitude < 0.07f) break;
                if (to.magnitude > noticeRadius * 1.6f) { _busy = false; yield break; } // child took it away
                Vector3 target = transform.position + to.normalized * Mathf.Min(0.1f, to.magnitude - 0.05f);
                yield return HopTo(target, false);
            }
            if (seed == null || !seed.gameObject.activeInHierarchy) { _busy = false; yield break; }

            Face(seed.transform.position);
            yield return Peck(3);
            seed.Consume();
            Fed = true;
            if (chirp != null) { chirp.pitch = 1.15f; chirp.Play(); }
            yield return new WaitForSeconds(0.35f);
            yield return FlyCircle();
            if (chirp != null) { chirp.pitch = 1.2f; chirp.Play(); }
            if (StoryManager.Instance != null) StoryManager.Instance.Complete(StoryStep.Bird);
            _busy = false;
        }

        void Face(Vector3 p)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 1e-5f) transform.rotation = Quaternion.LookRotation(d);
        }

        IEnumerator HopTo(Vector3 target, bool setBusy = true)
        {
            if (setBusy) _busy = true;
            target.y = _home.y;
            Face(target);
            Vector3 from = transform.position;
            const float dur = 0.2f, h = 0.045f;
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                transform.position = Vector3.Lerp(from, target, t) + Vector3.up * (4f * h * t * (1f - t));
                yield return null;
            }
            transform.position = target;
            yield return new WaitForSeconds(0.06f);
            if (setBusy) _busy = false;
        }

        IEnumerator Peck(int times)
        {
            bool wasBusy = _busy; _busy = true;
            if (head != null)
            {
                Quaternion rest = head.localRotation;
                for (int i = 0; i < times; i++)
                {
                    for (float t = 0; t < 1f; t += Time.deltaTime / 0.12f) { head.localRotation = rest * Quaternion.Euler(55f * t, 0, 0); yield return null; }
                    for (float t = 0; t < 1f; t += Time.deltaTime / 0.12f) { head.localRotation = rest * Quaternion.Euler(55f * (1 - t), 0, 0); yield return null; }
                }
                head.localRotation = rest;
            }
            _busy = wasBusy;
        }

        IEnumerator LookAround()
        {
            _busy = true;
            if (head != null)
            {
                Quaternion rest = head.localRotation;
                float yaw = Random.Range(-60f, 60f);
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.25f) { head.localRotation = rest * Quaternion.Euler(0, yaw * t, 0); yield return null; }
                yield return new WaitForSeconds(Random.Range(0.3f, 0.8f));
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.25f) { head.localRotation = rest * Quaternion.Euler(0, yaw * (1 - t), 0); yield return null; }
                head.localRotation = rest;
            }
            _busy = false;
        }

        IEnumerator FlyCircle()
        {
            _flying = true;
            Vector3 start = transform.position, centre = _home;
            const float radius = 0.5f, height = 0.45f, dur = 3.2f;
            float a0 = Mathf.Atan2(start.z - centre.z, start.x - centre.x);
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                float a = a0 + t * Mathf.PI * 2f;
                float lift = Mathf.Sin(Mathf.PI * t);
                Vector3 p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius * lift + 0.05f) + Vector3.up * (height * lift);
                Vector3 vel = p - transform.position;
                transform.position = p;
                if (vel.sqrMagnitude > 1e-6f) { vel.y = 0f; if (vel.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(vel); }
                yield return null;
            }
            transform.position = _home;
            _flying = false;
        }

        void AnimateWings()
        {
            if (wingL == null || wingR == null) return;
            float a = _flying ? Mathf.Sin(Time.time * 40f) * 60f + 20f : (_busy ? 4f : 0f);
            wingL.localRotation = Quaternion.Euler(0, 0, a);
            wingR.localRotation = Quaternion.Euler(0, 0, -a);
        }

        public void ForceComplete()
        {
            StopAllCoroutines();
            _busy = false; _flying = false;
            transform.position = _home == Vector3.zero ? transform.position : _home;
            if (!Fed)
            {
                Fed = true;
                foreach (var s in seeds) if (s != null && s.gameObject.activeInHierarchy && !s.IsHeld) { s.Consume(); break; }
            }
        }
    }
}

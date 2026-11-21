using System.Collections.Generic;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// SIDE QUEST: glowing mushrooms along the path. Touching a mushroom as the CHILD (with a hand, or by
    /// bumping into it) makes it light up for a few seconds with a little "bling" and a puff of sparkles.
    /// One manager for all mushrooms under this object (cheap: one Update, distance checks only).
    /// The mushrooms' material must have _EMISSION enabled (the builder sets this up).
    /// </summary>
    public class GlowMushrooms : MonoBehaviour
    {
        public Color glowColor = new Color(0.55f, 1f, 0.75f);
        [Tooltip("Peak emission intensity.")]
        public float intensity = 2.2f;
        public float glowSeconds = 2.5f;
        [Tooltip("Extra touch distance (m) around a mushroom's bounds for hands.")]
        public float handMargin = 0.02f;
        [Tooltip("SFX_MushroomBling (placeholder) - moved to the touched mushroom.")]
        public AudioSource bling;
        public ParticleSystem sparkle;

        class Shroom { public Renderer[] rends; public Bounds bounds; public float until; public float lit; }
        readonly List<Shroom> _list = new List<Shroom>();
        MaterialPropertyBlock _mpb;
        OVRCameraRig _rig;
        public int Touches { get; private set; }

        void Start()
        {
            _mpb = new MaterialPropertyBlock();
            _rig = FindAnyObjectByType<OVRCameraRig>();
            foreach (Transform t in transform)
            {
                if (bling != null && t == bling.transform) continue;
                if (sparkle != null && t == sparkle.transform) continue;
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                _list.Add(new Shroom { rends = rs, bounds = b });
            }
        }

        void Update()
        {
            var age = AgeController.Instance;
            bool child = age != null && age.CurrentAge == Age.Child && !age.IsTransitioning;
            Vector3 l = Vector3.zero, r = Vector3.zero, body = Vector3.zero;
            float bodyR = 0f;
            bool hands = false, hasBody = false;
            if (child && _rig != null)
            {
                hands = true; l = _rig.leftHandAnchor.position; r = _rig.rightHandAnchor.position;
                if (age.character != null)
                {
                    hasBody = true;
                    var cc = age.character;
                    body = cc.Pose.position; bodyR = cc.Radius + 0.02f;
                }
            }

            float now = Time.time;
            foreach (var s in _list)
            {
                if (child && now > s.until - glowSeconds + 0.6f)
                {
                    bool touched = false;
                    if (hands)
                    {
                        var b = s.bounds; b.Expand(handMargin * 2f);
                        touched = b.Contains(l) || b.Contains(r);
                    }
                    if (!touched && hasBody)
                    {
                        Vector3 c = s.bounds.center;
                        float rad = Mathf.Max(s.bounds.extents.x, s.bounds.extents.z) * 0.5f;
                        float dx = body.x - c.x, dz = body.z - c.z;
                        touched = body.y - 0.2f < s.bounds.max.y && dx * dx + dz * dz < (rad + bodyR) * (rad + bodyR);
                    }
                    if (touched) Light(s);
                }

                float target = now < s.until ? 1f : 0f;
                float k = Mathf.MoveTowards(s.lit, target, Time.deltaTime / (target > s.lit ? 0.12f : 0.8f));
                if (Mathf.Abs(k - s.lit) < 1e-4f && k == 0f && s.lit == 0f) continue;
                s.lit = k;
                float pulse = 1f + 0.15f * Mathf.Sin(now * 9f);
                foreach (var rend in s.rends)
                {
                    rend.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_EmissionColor", glowColor * (intensity * k * pulse));
                    rend.SetPropertyBlock(_mpb);
                }
            }
        }

        void Light(Shroom s)
        {
            bool wasDark = Time.time > s.until;
            s.until = Time.time + glowSeconds;
            if (!wasDark) return;
            Touches++;
            Vector3 top = new Vector3(s.bounds.center.x, s.bounds.max.y, s.bounds.center.z);
            if (bling != null) { bling.transform.position = top; bling.pitch = Random.Range(0.92f, 1.12f); bling.Play(); }
            if (sparkle != null) { sparkle.transform.position = top; sparkle.Play(); }
        }
    }
}

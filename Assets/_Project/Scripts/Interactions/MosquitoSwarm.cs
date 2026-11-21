using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// ADULT, between the river and the log: instead of butterflies, a swarm of mosquitoes buzzes around
    /// the head. Swing a hand quickly near the face to swat them away; they scatter and come back after
    /// a while (fewer each time) until you leave the area.
    /// </summary>
    public class MosquitoSwarm : MonoBehaviour
    {
        [Header("Area (world z along the path)")]
        public float zMin = 5f;
        public float zMax = 11.9f;

        [Header("Swarm")]
        public ParticleSystem swarm;
        public ParticleSystem scatter;
        public AudioSource buzz;
        public float rate = 26f;
        [Tooltip("Hand speed (m/s) near the face that counts as a swat.")]
        public float swatSpeed = 1.4f;
        [Tooltip("Hand must be this close to the head (m).")]
        public float swatReach = 0.75f;
        public float returnSeconds = 5f;

        public int Swats { get; private set; }

        OVRCameraRig _rig;
        Vector3 _lastL, _lastR;
        float _density, _awayUntil;
        bool _active;

        void Start()
        {
            _rig = FindAnyObjectByType<OVRCameraRig>();
            if (swarm != null) swarm.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (buzz != null) { buzz.volume = 0f; }
        }

        void Update()
        {
            var age = AgeController.Instance;
            if (_rig == null || age == null) return;
            Transform head = _rig.centerEyeAnchor;
            Vector3 h = head.position;
            bool inArea = age.CurrentAge == Age.Adult && !age.IsTransitioning && h.z > zMin && h.z < zMax;

            // swat detection
            Vector3 l = _rig.leftHandAnchor.position, r = _rig.rightHandAnchor.position;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float vl = (l - _lastL).magnitude / dt, vr = (r - _lastR).magnitude / dt;
            _lastL = l; _lastR = r;
            if (inArea && _density > 0.15f && Time.time > _awayUntil &&
                ((vl > swatSpeed && (l - h).magnitude < swatReach) || (vr > swatSpeed && (r - h).magnitude < swatReach)))
                Swat(vl > vr ? l : r);

            float target = inArea && Time.time > _awayUntil ? 1f / (1f + Swats * 0.35f) : 0f;
            _density = Mathf.MoveTowards(_density, target, Time.deltaTime / (target > _density ? 2.5f : 0.4f));

            if (swarm != null)
            {
                swarm.transform.position = h + head.forward * 0.15f;
                var em = swarm.emission; em.rateOverTime = rate * _density;
                if (_density > 0.01f && !swarm.isPlaying) swarm.Play();
                if (_density <= 0.01f && swarm.isPlaying) swarm.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (buzz != null)
            {
                // the buzz circles the ears
                float a = Time.time * 2.3f;
                buzz.transform.position = h + new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a * 1.7f) * 0.1f, Mathf.Sin(a) * 0.3f);
                buzz.volume = 0.5f * _density;
                if (_density > 0.01f && !buzz.isPlaying) buzz.Play();
                if (_density <= 0.01f && buzz.isPlaying) buzz.Stop();
            }
            _active = inArea;
        }

        void Swat(Vector3 hand)
        {
            Swats++;
            _awayUntil = Time.time + returnSeconds;
            _density = 0f;
            if (swarm != null) swarm.Clear(true);
            if (scatter != null)
            {
                scatter.transform.position = hand;
                scatter.Play();
            }
            OVRInput.SetControllerVibration(0.4f, 0.2f, OVRInput.Controller.Touch);
            Invoke(nameof(StopHaptics), 0.12f);
        }

        void StopHaptics() => OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);

        /// Debug: swat now.
        [ContextMenu("Debug: swat")]
        public void DebugSwat() { if (_rig != null) Swat(_rig.centerEyeAnchor.position); }

        public bool InArea => _active;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace GrowingUp
{
    /// <summary>
    /// The front door of the house (on World/House/DoorHinge). Part of the world; it never changes per age.
    ///  - Intro: it stands open, then CloseBehindChild() shuts it with the door-close sound.
    ///  - Afterwards it stays shut. The ADULT reaching for the handle or pushing against it plays a
    ///    stuck/locked rattle; onAdultTriedDoor is the hook for the ending (Phase 3).
    /// </summary>
    public class HouseDoor : MonoBehaviour
    {
        [Header("Door")]
        [Tooltip("Hinge Y angle when open (swung inwards).")]
        public float openAngle = 100f;
        public float closedAngle = 0f;
        [Tooltip("Start open (the intro closes it behind the child). Off = start closed.")]
        public bool startOpen = true;
        public float closeTime = 0.45f;
        public Transform handle;

        [Header("Sounds (placeholders: drop real clips on these AudioSources)")]
        [Tooltip("SFX_DoorClose - played when the door shuts behind the child.")]
        public AudioSource closeSound;
        [Tooltip("SFX_DoorLocked - the adult tries the stuck door.")]
        public ProceduralAudio lockedSound;

        [Header("Trying the locked door (adult)")]
        public float handReach = 0.22f;
        public float bodyReach = 0.55f;
        public float cooldown = 1.5f;

        [Header("Hooks")]
        [Tooltip("Invoked every time the ADULT tries the locked door. Ending sequence goes here (Phase 3).")]
        public UnityEvent onAdultTriedDoor;
        public int AdultAttempts { get; private set; }
        public bool IsClosed { get; private set; }

        OVRCameraRig _rig;
        Transform _doorPanel;
        float _nextTry, _baseAngle;
        Coroutine _rattle;

        void Awake()
        {
            _doorPanel = transform.Find("Door");
            SetAngle(startOpen ? openAngle : closedAngle);
            IsClosed = !startOpen;
        }

        void Start() => _rig = FindAnyObjectByType<OVRCameraRig>();

        /// Intro: slam the door shut behind the child.
        public void CloseBehindChild()
        {
            if (IsClosed) return;
            StartCoroutine(CloseRoutine());
        }

        /// Instant (debug skip).
        public void SetClosedImmediate() { StopAllCoroutines(); SetAngle(closedAngle); IsClosed = true; }

        IEnumerator CloseRoutine()
        {
            float from = _baseAngle;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, closeTime))
            {
                SetAngle(Mathf.Lerp(from, closedAngle, t * t)); // accelerating swing, like a slam
                yield return null;
            }
            SetAngle(closedAngle);
            IsClosed = true;
            if (closeSound != null) closeSound.Play();
        }

        void SetAngle(float a)
        {
            _baseAngle = a;
            transform.localRotation = Quaternion.Euler(0f, a, 0f);
        }

        void Update()
        {
            var age = AgeController.Instance;
            if (!IsClosed || age == null || _rig == null || age.CurrentAge != Age.Adult || age.IsTransitioning) return;
            if (Time.time < _nextTry) return;

            bool tried = false;
            if (handle != null && (Near(_rig.leftHandAnchor, handle.position, handReach) || Near(_rig.rightHandAnchor, handle.position, handReach))) tried = true;
            if (!tried && _doorPanel != null)
            {
                Vector3 d = _rig.centerEyeAnchor.position - _doorPanel.position; d.y = 0f;
                if (d.magnitude < bodyReach + 0.15f) tried = true;
            }
            if (tried) TryLockedDoor();
        }

        static bool Near(Transform t, Vector3 p, float r) => t != null && t.gameObject.activeInHierarchy && (t.position - p).sqrMagnitude < r * r;

        /// Can also be called from an interaction event (e.g. a Poke on the handle).
        public void TryLockedDoor()
        {
            var age = AgeController.Instance;
            if (age != null && age.CurrentAge != Age.Adult) return;
            _nextTry = Time.time + cooldown;
            AdultAttempts++;
            if (lockedSound != null) lockedSound.PlayOneShot();
            if (_rattle != null) StopCoroutine(_rattle);
            _rattle = StartCoroutine(Rattle());
            OVRInput.SetControllerVibration(0.3f, 0.4f, OVRInput.Controller.Touch);
            Invoke(nameof(StopHaptics), 0.25f);
            onAdultTriedDoor?.Invoke();
        }

        void StopHaptics() => OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);

        IEnumerator Rattle()
        {
            float[] hits = { 0f, 0.16f, 0.30f };
            float start = Time.time;
            while (Time.time - start < 0.45f)
            {
                float t = Time.time - start, jolt = 0f;
                foreach (var h in hits) { float u = t - h; if (u >= 0f && u < 0.1f) jolt += Mathf.Sin(u / 0.1f * Mathf.PI) * 1.2f; }
                transform.localRotation = Quaternion.Euler(0f, _baseAngle + jolt, 0f);
                yield return null;
            }
            transform.localRotation = Quaternion.Euler(0f, _baseAngle, 0f);
            _rattle = null;
        }
    }
}

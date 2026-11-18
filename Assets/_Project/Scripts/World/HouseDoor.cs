using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace GrowingUp
{
    /// <summary>
    /// The front door of the house (put on the DoorHinge). The door itself is part of the world and
    /// never changes; what changes is what the current player can do with it:
    ///  - CHILD: the door stands open (they just came out of it).
    ///  - ADULT: the door is shut and won't open. Reaching for the handle or pushing against it plays
    ///    a stuck/locked rattle. onAdultTriedDoor is the hook for the ending (fade to black / end text).
    /// </summary>
    public class HouseDoor : MonoBehaviour
    {
        [Header("Door")]
        [Tooltip("Hinge Y angle when open (swung inwards).")]
        public float openAngle = 100f;
        public float closedAngle = 0f;
        [Tooltip("Seconds to swing between open and closed when the age changes.")]
        public float swingTime = 1.2f;
        public Transform handle;

        [Header("Trying the locked door (adult)")]
        [Tooltip("A hand/controller this close (m) to the handle counts as trying the door.")]
        public float handReach = 0.22f;
        [Tooltip("The player's body this close (m, horizontal) to the closed door counts as pushing it.")]
        public float bodyReach = 0.55f;
        public float cooldown = 1.5f;
        public ProceduralAudio lockedSound;

        [Header("Hooks")]
        [Tooltip("Invoked every time the ADULT tries the locked door. Ending sequence goes here (later phase).")]
        public UnityEvent onAdultTriedDoor;
        public int adultAttempts { get; private set; }

        OVRCameraRig _rig;
        Transform _doorPanel;
        float _nextTry;
        Coroutine _swing, _rattle;
        float _baseAngle;

        void Start()
        {
            _rig = FindFirstObjectByType<OVRCameraRig>();
            _doorPanel = transform.Find("Door");
            var age = AgeController.Instance;
            if (age != null)
            {
                age.AgeChanged += OnAgeChanged;
                SetAngle(age.CurrentAge == Age.Child ? openAngle : closedAngle);
            }
        }

        void OnDestroy()
        {
            if (AgeController.Instance != null) AgeController.Instance.AgeChanged -= OnAgeChanged;
        }

        void OnAgeChanged(Age a)
        {
            if (_swing != null) StopCoroutine(_swing);
            _swing = StartCoroutine(Swing(a == Age.Child ? openAngle : closedAngle));
        }

        IEnumerator Swing(float target)
        {
            float from = _baseAngle;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, swingTime))
            {
                SetAngle(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            SetAngle(target);
            _swing = null;
        }

        void SetAngle(float a)
        {
            _baseAngle = a;
            transform.localRotation = Quaternion.Euler(0f, a, 0f);
        }

        void Update()
        {
            var age = AgeController.Instance;
            if (age == null || _rig == null || age.CurrentAge != Age.Adult || age.IsTransitioning || _swing != null) return;
            if (Time.time < _nextTry) return;

            bool tried = false;
            if (handle != null)
            {
                if (Near(_rig.leftHandAnchor, handle.position, handReach) || Near(_rig.rightHandAnchor, handle.position, handReach)) tried = true;
            }
            if (!tried && _doorPanel != null)
            {
                Vector3 d = _rig.centerEyeAnchor.position - _doorPanel.position; d.y = 0f;
                if (d.magnitude < bodyReach + 0.15f) tried = true;
            }
            if (tried) TryLockedDoor();
        }

        static bool Near(Transform t, Vector3 p, float r) => t != null && t.gameObject.activeInHierarchy && (t.position - p).sqrMagnitude < r * r;

        /// Can also be called from an interaction (e.g. a Poke/Grab event on the handle).
        public void TryLockedDoor()
        {
            var age = AgeController.Instance;
            if (age != null && age.CurrentAge != Age.Adult) return;
            _nextTry = Time.time + cooldown;
            adultAttempts++;
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
            // Three small jolts in time with the knocks: the door is stuck in its frame.
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

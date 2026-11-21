using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace GrowingUp
{
    /// <summary>
    /// Main quest 3: the bush tunnel (step "BushTunnel").
    /// A big bush blocks the path. The CHILD crawls through a low leafy tunnel in it (~5 s). The leafy
    /// ceiling (adultBlocker) is too low for the ADULT's body: on the way back, the adult reaching into
    /// the bush or pushing against it bends both halves of the bush aside (push the branches), which
    /// removes the blocker so they can walk through.
    /// </summary>
    public class BushTunnel : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.BushTunnel;

        [Header("Tunnel (world z along the path)")]
        public float entranceZ = 18.5f;
        public float exitZ = 21.5f;
        public float pathX = 0f;

        [Header("Adult: push the branches aside")]
        public Transform leftHalf;
        public Transform rightHalf;
        [Tooltip("Collider that stops the adult body (the low leafy ceiling).")]
        public Collider adultBlocker;
        [Tooltip("How far each half bends outwards (degrees, around its outer bottom edge).")]
        public float pushAngle = 32f;
        public float pushTime = 1.2f;
        [Tooltip("Hand / body this close (m) to an entrance counts as pushing.")]
        public float reach = 0.45f;
        public AudioSource rustleSound;
        public UnityEvent onBranchesPushed;

        public bool Passed { get; private set; }
        public bool Opened { get; private set; }

        OVRCameraRig _rig;
        Quaternion _l0, _r0;

        void Start()
        {
            _rig = FindAnyObjectByType<OVRCameraRig>();
            if (leftHalf != null) _l0 = leftHalf.localRotation;
            if (rightHalf != null) _r0 = rightHalf.localRotation;
        }

        void Update()
        {
            var age = AgeController.Instance;
            if (age == null || age.character == null) return;
            var cc = age.character;
            Vector3 feet = cc.Pose.position + Vector3.down * (cc.Height * 0.5f);

            var story = StoryManager.Instance;
            if (!Passed && story != null && story.Is(StoryStep.BushTunnel) && feet.z > exitZ + 0.2f && Mathf.Abs(feet.x - pathX) < 2f)
            {
                Passed = true;
                story.Complete(StoryStep.BushTunnel);
            }

            if (!Opened && age.CurrentAge == Age.Adult && !age.IsTransitioning && _rig != null)
            {
                if (NearEntrance(_rig.leftHandAnchor) || NearEntrance(_rig.rightHandAnchor) || NearEntrance(_rig.centerEyeAnchor, 0.35f))
                    PushBranches();
            }
        }

        bool NearEntrance(Transform t, float extra = 0f)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return false;
            Vector3 p = t.position;
            if (Mathf.Abs(p.x - pathX) > 1.2f) return false;
            float r = reach + extra;
            return (p.z > entranceZ - r && p.z < entranceZ + 0.3f) || (p.z < exitZ + r && p.z > exitZ - 0.3f);
        }

        [ContextMenu("Push branches aside")]
        public void PushBranches()
        {
            if (Opened) return;
            Opened = true;
            if (adultBlocker != null) adultBlocker.enabled = false;
            if (rustleSound != null) rustleSound.Play();
            OVRInput.SetControllerVibration(0.25f, 0.3f, OVRInput.Controller.Touch);
            Invoke(nameof(StopHaptics), 0.3f);
            StartCoroutine(Bend());
            onBranchesPushed?.Invoke();
        }

        void StopHaptics() => OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);

        IEnumerator Bend()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, pushTime))
            {
                float k = 1f - (1f - t) * (1f - t);
                float over = Mathf.Sin(t * Mathf.PI) * 4f; // little springy overshoot
                if (leftHalf != null) leftHalf.localRotation = _l0 * Quaternion.Euler(0f, 0f, pushAngle * k + over);
                if (rightHalf != null) rightHalf.localRotation = _r0 * Quaternion.Euler(0f, 0f, -(pushAngle * k + over));
                yield return null;
            }
            if (leftHalf != null) leftHalf.localRotation = _l0 * Quaternion.Euler(0f, 0f, pushAngle);
            if (rightHalf != null) rightHalf.localRotation = _r0 * Quaternion.Euler(0f, 0f, -pushAngle);
        }

        public void ForceComplete() => Passed = true;
    }
}

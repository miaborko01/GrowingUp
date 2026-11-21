using UnityEngine;
using UnityEngine.Events;

namespace GrowingUp
{
    /// <summary>
    /// Main quest 3: the bush tunnel (step "BushTunnel").
    /// A big bush blocks the path. The CHILD crawls through a low leafy tunnel in it (~5 s). The leafy
    /// ceiling (adultBlocker) is too low for the ADULT's body: on the way back the adult GRABS a branch
    /// on each side (BranchPull) and pulls it aside. Each pull bends that half of the bush outwards;
    /// once both halves are open enough the blocker is removed and the adult can walk through.
    /// </summary>
    public class BushTunnel : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.BushTunnel;

        [Header("Tunnel (world z along the path)")]
        public float entranceZ = 18.5f;
        public float exitZ = 21.5f;
        public float pathX = 0f;

        [Header("Adult: pull the branches aside (BranchPull handles)")]
        public Transform leftHalf;
        public Transform rightHalf;
        [Tooltip("Collider that stops the adult body (the low leafy ceiling).")]
        public Collider adultBlocker;
        [Tooltip("How far each half bends outwards (degrees, around its outer bottom edge).")]
        public float pushAngle = 32f;
        [Tooltip("Both halves must be at least this open (0..1) for the adult to fit through.")]
        public float openThreshold = 0.65f;
        public AudioSource rustleSound;
        public UnityEvent onBranchesPushed;

        public bool Passed { get; private set; }
        public bool Opened { get; private set; }

        Quaternion _l0, _r0;
        float _openL, _openR, _nextRustle;

        public float OpenLeft => _openL;
        public float OpenRight => _openR;

        void Awake()
        {
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
        }

        /// Called by a BranchPull while the adult pulls a branch: side -1 = left half, +1 = right half,
        /// amount 0..1. Branches stay where they are pulled (they never spring fully back).
        public void SetOpen(int side, float amount)
        {
            amount = Mathf.Clamp01(amount);
            float before = side < 0 ? _openL : _openR;
            if (amount <= before) return;
            if (side < 0) { _openL = amount; if (leftHalf != null) leftHalf.localRotation = _l0 * Quaternion.Euler(0f, 0f, pushAngle * amount); }
            else { _openR = amount; if (rightHalf != null) rightHalf.localRotation = _r0 * Quaternion.Euler(0f, 0f, -pushAngle * amount); }

            if (rustleSound != null && amount - before > 0.002f && Time.time > _nextRustle)
            {
                _nextRustle = Time.time + 0.9f;
                rustleSound.Play();
            }
            if (!Opened && _openL >= openThreshold && _openR >= openThreshold)
            {
                Opened = true;
                if (adultBlocker != null) adultBlocker.enabled = false;
                onBranchesPushed?.Invoke();
            }
        }

        [ContextMenu("Debug: pull both branches aside")]
        public void PushBranches() { SetOpen(-1, 1f); SetOpen(1, 1f); }

        public void ForceComplete() => Passed = true;
    }
}

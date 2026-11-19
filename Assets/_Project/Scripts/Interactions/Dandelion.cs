using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Dandelion (child, step "Dandelion"). A big dandelion stands by the path (huge to the child).
    /// The child picks it and brings the seed head to their mouth (MouthZone) to blow: the fluffy head
    /// disappears and the seeds burst away as glowing particles drifting into the air, with a magical
    /// whoosh. The bare stem stays where the child leaves it (the adult passes it later).
    /// Optional later: real microphone breath detection could call Blow() too.
    /// </summary>
    public class Dandelion : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.Dandelion;

        public GrabbableItem item;
        [Tooltip("Centre of the fluffy seed head.")]
        public Transform head;
        [Tooltip("Visual of the fluffy head (hidden once blown).")]
        public GameObject puff;
        public float puffRadius = 0.06f;
        public ParticleSystem seedBurst;
        [Tooltip("SFX_DandelionWhoosh (placeholder sound slot).")]
        public AudioSource whoosh;

        public bool Blown { get; private set; }

        void Update()
        {
            if (Blown || head == null) return;
            var story = StoryManager.Instance;
            if (story == null || !story.Is(StoryStep.Dandelion)) return;
            if (MouthZone.IsAtMouth(head.position, puffRadius)) Blow();
        }

        /// Blow the seeds away (also callable from a future microphone/breath detector).
        public void Blow()
        {
            if (Blown) return;
            Blown = true;
            if (puff != null) puff.SetActive(false);
            if (seedBurst != null)
            {
                seedBurst.transform.position = head.position;
                // drift away from the player's face, up and forward
                var age = AgeController.Instance;
                if (age != null && age.Head != null)
                {
                    Vector3 away = (head.position - age.Head.position); away.y = 0f;
                    if (away.sqrMagnitude < 1e-4f) away = age.Head.forward;
                    seedBurst.transform.rotation = Quaternion.LookRotation((away.normalized + Vector3.up * 0.6f).normalized);
                }
                seedBurst.Play();
            }
            if (whoosh != null) { whoosh.transform.position = head.position; whoosh.Play(); }
            if (StoryManager.Instance != null) StoryManager.Instance.Complete(StoryStep.Dandelion);
        }

        public void ForceComplete()
        {
            if (Blown) return;
            Blown = true;
            if (puff != null) puff.SetActive(false);
        }
    }
}

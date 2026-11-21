using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Main quest 1: the fallen log (child, step "Log").
    /// A log lies across the path, too high for the tiny child to jump (top ~0.46 m, child jump 0.3 m).
    /// Both sides are blocked by a thicket. Rocks lie scattered along the way: the child pushes / pulls
    /// them (PushableRock) next to the log to build a little staircase, jumps up and climbs over.
    /// Reaching the far side completes the step. The rocks stay where the child left them.
    /// </summary>
    public class LogCrossing : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.Log;

        [Tooltip("Z of the log's axis (world).")]
        public float logZ = 4.5f;
        [Tooltip("Log radius (m).")]
        public float logRadius = 0.23f;
        public PushableRock[] rocks;
        [Tooltip("Where the rocks go when debug-skipping (staircase in front of the log, low to high).")]
        public Transform[] staircaseSpots;

        public bool Crossed { get; private set; }

        void Update()
        {
            if (Crossed) return;
            var story = StoryManager.Instance;
            var age = AgeController.Instance;
            if (story == null || age == null || age.character == null || !story.Is(StoryStep.Log)) return;
            var cc = age.character;
            Vector3 feet = cc.Pose.position + Vector3.down * (cc.Height * 0.5f);
            if (feet.z > logZ + logRadius + 0.15f)
            {
                Crossed = true;
                story.Complete(StoryStep.Log);
            }
        }

        public void ForceComplete()
        {
            Crossed = true;
            if (rocks == null || staircaseSpots == null) return;
            for (int i = 0; i < rocks.Length && i < staircaseSpots.Length; i++)
                if (rocks[i] != null && staircaseSpots[i] != null)
                    rocks[i].PlaceAt(staircaseSpots[i].position, staircaseSpots[i].eulerAngles.y);
        }
    }
}

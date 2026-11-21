using System.Collections;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Adult return (step "AdultReturn") -> Ending. The adult walks back home; the house is old and
    /// moldy and the door won't open (HouseDoor plays the stuck/locked rattle). After trying the door,
    /// the view fades to black and the end text appears.
    /// </summary>
    public class EndingSequence : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.AdultReturn;

        public HouseDoor door;
        [Tooltip("Seconds after the first try at the door before the fade starts.")]
        public float delayAfterDoor = 2.2f;
        public float fadeSeconds = 3.5f;
        [TextArea] public string endText = "The forest didn't change.\nGrowing up did.";
        public float endTextSeconds = 12f;

        bool _ending;

        void Start()
        {
            if (door != null) door.onAdultTriedDoor.AddListener(OnDoor);
        }

        void OnDestroy() { if (door != null) door.onAdultTriedDoor.RemoveListener(OnDoor); }

        void OnDoor()
        {
            var story = StoryManager.Instance;
            if (_ending || story == null || !story.Is(StoryStep.AdultReturn)) return;
            StartCoroutine(End());
        }

        [ContextMenu("Play ending now")]
        public void PlayEnding() { if (!_ending) StartCoroutine(End()); }

        IEnumerator End()
        {
            _ending = true;
            yield return new WaitForSeconds(delayAfterDoor);
            var overlay = ScreenOverlay.Instance;
            for (float t = 0f; t < 1f; t += Time.deltaTime / fadeSeconds)
            {
                if (overlay != null) overlay.Set(new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, t)));
                PerceptionMix.Nature = Mathf.Lerp(PerceptionMix.Nature, 0f, Time.deltaTime * 1.5f);
                yield return null;
            }
            if (overlay != null) overlay.Set(Color.black);
            var age = AgeController.Instance;
            if (age != null) age.enabled = false; // stop debug toggles; keeps the faded state
            if (Subtitles.Instance != null) { Subtitles.Instance.DrawOnTop(); Subtitles.Instance.Show(endText, endTextSeconds); }
            if (StoryManager.Instance != null) StoryManager.Instance.Complete(StoryStep.AdultReturn);
        }

        public void ForceComplete() { }
    }
}

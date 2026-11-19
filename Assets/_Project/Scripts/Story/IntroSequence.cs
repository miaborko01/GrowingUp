using System.Collections;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Intro (child): the player wakes up just outside the house, facing the path. Behind them the front
    /// door slams shut, then the mother calls from inside: "Go explore the forest, but come back before
    /// dark, and be careful!". Then the story moves on to the River step.
    ///
    /// PLACEHOLDER AUDIO: drop the real recording on the "VO_Mother_GoExplore" AudioSource (inside the house).
    /// While it is empty the line is only shown as a subtitle.
    /// </summary>
    public class IntroSequence : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.Intro;

        public HouseDoor door;
        [Tooltip("VO_Mother_GoExplore - the mother's voice line (placeholder slot, no clip yet).")]
        public AudioSource motherVoice;
        [TextArea] public string motherLine = "Mum: \"Go explore the forest, but come back before dark, and be careful!\"";
        public float delayBeforeDoor = 1.2f;
        public float delayBeforeVoice = 1.0f;
        [Tooltip("How long the subtitle stays up if there is no voice clip.")]
        public float subtitleSeconds = 5f;

        Coroutine _co;

        void Start()
        {
            var story = StoryManager.Instance;
            if (story != null && story.Is(StoryStep.Intro)) _co = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(delayBeforeDoor);
            if (door != null)
            {
                door.CloseBehindChild();
                yield return new WaitForSeconds(door.closeTime);
            }
            yield return new WaitForSeconds(delayBeforeVoice);

            float len = subtitleSeconds;
            if (motherVoice != null && motherVoice.clip != null)
            {
                motherVoice.Play();
                len = Mathf.Max(len, motherVoice.clip.length + 0.5f);
            }
            if (Subtitles.Instance != null) Subtitles.Instance.Show(motherLine, len);
            yield return new WaitForSeconds(len);

            _co = null;
            if (StoryManager.Instance != null) StoryManager.Instance.Complete(StoryStep.Intro);
        }

        public void ForceComplete()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            if (door != null) door.SetClosedImmediate();
            if (motherVoice != null) motherVoice.Stop();
            if (Subtitles.Instance != null) Subtitles.Instance.Hide();
        }
    }
}

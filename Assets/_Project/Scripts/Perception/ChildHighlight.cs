using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Soft magical glow (halo + rising sparkles) around something the CHILD can interact with, so it
    /// is clear what to use. Part of the child's magic: it fades for the adult. Optionally only during
    /// one story step, and it switches off once the thing is used (dandelion blown, item eaten/locked).
    /// </summary>
    public class ChildHighlight : MonoBehaviour
    {
        public ParticleSystem fx;
        [Tooltip("Only glow while the story is at this step.")]
        public bool onlyDuringStep;
        public StoryStep step;
        public GrabbableItem item;
        public Dandelion dandelion;
        [Tooltip("Keep the glow on the object while it is carried / dragged.")]
        public bool followObject = true;

        bool _on;

        void Start()
        {
            if (fx != null) fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Update()
        {
            var age = AgeController.Instance;
            bool on = age == null || (age.CurrentAge == Age.Child && !age.IsTransitioning);
            if (on && onlyDuringStep)
            {
                var story = StoryManager.Instance;
                on = story != null && story.Is(step);
            }
            if (on && item != null && (item.Locked || !item.gameObject.activeInHierarchy)) on = false;
            if (on && dandelion != null && dandelion.Blown) on = false;

            if (fx == null) return;
            if (followObject && item != null) fx.transform.position = item.transform.position;
            if (on != _on)
            {
                _on = on;
                if (on) fx.Play(true);
                else fx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}

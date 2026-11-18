using UnityEngine;
using UnityEngine.Events;

namespace GrowingUp
{
    /// <summary>
    /// Placed at the end-of-path clearing. When the CHILD's head enters the radius, the player grows up.
    /// Uses the head position (not physics) so it works for smooth locomotion and teleport alike.
    /// Later phases: set requireTasks = true and call MarkTasksDone() when the child tasks are finished.
    /// </summary>
    public class GrowUpTrigger : MonoBehaviour
    {
        [Tooltip("Horizontal radius (m) around this object that starts growing up.")]
        public float radius = 3.5f;
        [Tooltip("Later phase: only trigger once the child tasks are complete.")]
        public bool requireTasks = false;
        public bool tasksDone = false;
        public UnityEvent onGrowUp;

        bool _fired;
        bool _waitForExit;
        Transform _head;

        void Start()
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null) _head = rig.centerEyeAnchor;
            if (AgeController.Instance != null) AgeController.Instance.AgeChanged += OnAgeChanged;
        }

        void OnDestroy()
        {
            if (AgeController.Instance != null) AgeController.Instance.AgeChanged -= OnAgeChanged;
        }

        // Debug toggle back to child: re-arm, but only after the child has left the clearing.
        void OnAgeChanged(Age a)
        {
            if (a == Age.Child && _fired) { _fired = false; _waitForExit = true; }
        }

        public void MarkTasksDone() => tasksDone = true;

        void Update()
        {
            var age = AgeController.Instance;
            if (_fired || _head == null || age == null) return;
            if (age.CurrentAge != Age.Child || age.IsTransitioning) return;
            if (requireTasks && !tasksDone) return;

            Vector3 d = _head.position - transform.position; d.y = 0f;
            bool inside = d.sqrMagnitude <= radius * radius;
            if (_waitForExit) { if (!inside) _waitForExit = false; return; }
            if (inside)
            {
                _fired = true;
                age.GrowUp();
                onGrowUp?.Invoke();
            }
        }

        /// Re-arm (e.g. after the debug toggle sends the player back to being a child).
        public void ResetTrigger() { _fired = false; _waitForExit = false; }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

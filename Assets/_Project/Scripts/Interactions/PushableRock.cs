using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// A heavy rock the child can PUSH / PULL (grab it with the Meta Grab Interaction and drag it).
    /// While held it stays on the ground and upright (it only slides and turns around Y), so it can be
    /// pushed next to the log as a step. When let go it stays where it is (kinematic, no throw), so the
    /// child can stand on it.
    /// </summary>
    [RequireComponent(typeof(GrabbableItem))]
    public class PushableRock : MonoBehaviour
    {
        [Tooltip("Max distance (m) the rock can be dragged away from its spot per second (heavy feel).")]
        public float maxDragSpeed = 0.5f;
        [Tooltip("Slight wobble while dragged (degrees).")]
        public float wobble = 1.5f;

        public AudioSource scrapeSound;

        GrabbableItem _item;
        float _groundY;
        Vector3 _last;
        bool _wasHeld;

        void Awake()
        {
            _item = GetComponent<GrabbableItem>();
            _groundY = transform.position.y;
            _last = transform.position;
        }

        void Start()
        {
            // rocks rest kinematic: standing on them never pushes them, and Grabbable restores this on release
            _item.Body.isKinematic = true;
        }

        void LateUpdate()
        {
            bool held = _item.IsHeld;
            if (held)
            {
                Vector3 p = transform.position;
                Vector3 step = new Vector3(p.x - _last.x, 0f, p.z - _last.z);
                float max = maxDragSpeed * Time.deltaTime;
                if (step.magnitude > max) step = step.normalized * max;
                Vector3 target = new Vector3(_last.x + step.x, _groundY, _last.z + step.z);
                float yaw = transform.eulerAngles.y;
                float w = step.sqrMagnitude > 1e-8f ? Mathf.Sin(Time.time * 18f) * wobble : 0f;
                transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, w));
                _item.Body.position = target;
                _item.Body.rotation = transform.rotation;
                if (scrapeSound != null)
                {
                    bool moving = step.sqrMagnitude > 1e-7f;
                    if (moving && !scrapeSound.isPlaying) { scrapeSound.transform.position = target; scrapeSound.Play(); }
                }
            }
            else if (_wasHeld)
            {
                // let go: settle upright on the ground, no throw
                var body = _item.Body;
                body.isKinematic = true;
                transform.SetPositionAndRotation(new Vector3(transform.position.x, _groundY, transform.position.z),
                    Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            }
            _wasHeld = held;
            _last = transform.position;
        }

        /// Debug skip: put the rock at a spot (on the ground).
        public void PlaceAt(Vector3 groundPos, float yaw)
        {
            var p = new Vector3(groundPos.x, _groundY, groundPos.z);
            transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            if (_item != null && _item.Body != null) { _item.Body.position = p; _item.Body.rotation = transform.rotation; }
            _last = p;
        }
    }
}

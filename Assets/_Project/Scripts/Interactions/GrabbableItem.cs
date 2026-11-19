using Oculus.Interaction;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Small helper on every carryable object (rock, seeds, dandelion, mushrooms, berries).
    /// The actual grabbing is the Meta Grab Interaction building block (Grabbable + HandGrab/Grab
    /// interactables, throw on release). This adds:
    ///  - "planted": the object is held in place (dandelion in the ground, berry on the bush) until
    ///    the child grabs it for the first time;
    ///  - a respawn if it gets lost (falls through the world / thrown out of reach).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbableItem : MonoBehaviour
    {
        public bool planted = false;
        [Tooltip("Respawn at the start position if it falls below this height.")]
        public float killY = -2f;

        public Grabbable Grabbable { get; private set; }
        public Rigidbody Body { get; private set; }
        public bool IsHeld => Grabbable != null && Grabbable.SelectingPointsCount > 0;
        public bool WasEverGrabbed { get; private set; }
        public bool Locked { get; private set; }

        Vector3 _startPos; Quaternion _startRot;

        void Awake()
        {
            Grabbable = GetComponent<Grabbable>();
            Body = GetComponent<Rigidbody>();
            _startPos = transform.position; _startRot = transform.rotation;
            if (planted) Body.constraints = RigidbodyConstraints.FreezeAll;
        }

        void Update()
        {
            if (Locked) return;
            if (IsHeld && !WasEverGrabbed)
            {
                WasEverGrabbed = true;
                if (planted) { planted = false; Body.constraints = RigidbodyConstraints.None; }
            }
            if (transform.position.y < killY) ResetToStart();
        }

        public void ResetToStart()
        {
            if (Locked) return;
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_startPos + Vector3.up * 0.05f, _startRot);
        }

        /// Snap into a final place and stop being grabbable (placed rock, mushroom in the ring).
        public void LockInPlace(Vector3 pos, Quaternion rot)
        {
            Locked = true;
            Body.isKinematic = true;
            Body.constraints = RigidbodyConstraints.FreezeAll;
            transform.SetPositionAndRotation(pos, rot);
            SetGrabEnabled(false);
        }

        /// Remove from the world (eaten seed / berry).
        public void Consume()
        {
            Locked = true;
            SetGrabEnabled(false);
            gameObject.SetActive(false);
        }

        public void SetGrabEnabled(bool on)
        {
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                var n = mb.GetType().Name;
                if (mb is Grabbable || n == "HandGrabInteractable" || n == "GrabInteractable" || n == "DistanceGrabInteractable")
                    mb.enabled = on;
            }
        }
    }
}

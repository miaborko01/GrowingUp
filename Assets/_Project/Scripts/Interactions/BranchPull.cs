using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// A branch at the bush tunnel the ADULT grabs (Meta Grab Interaction) and pulls sideways to bend
    /// that half of the bush aside. The further it is pulled away from the path, the more the half
    /// opens (BushTunnel.SetOpen). When let go, the branch handle settles back onto the (now bent) bush.
    /// The child can't grab it (they simply crawl through).
    /// </summary>
    [RequireComponent(typeof(GrabbableItem))]
    public class BranchPull : MonoBehaviour
    {
        public BushTunnel tunnel;
        [Tooltip("-1 = left half, +1 = right half.")]
        public int side = -1;
        [Tooltip("Point on the bush half where the branch handle rests (moves with the half).")]
        public Transform attach;
        [Tooltip("Metres the hand has to pull sideways (away from the path) to open this half fully.")]
        public float fullPull = 0.55f;

        GrabbableItem _item;
        Vector3 _grabStart;
        bool _wasHeld, _grabOn = true;

        void Awake() => _item = GetComponent<GrabbableItem>();

        void Start()
        {
            _item.Body.isKinematic = true;
            _item.Body.useGravity = false;
        }

        void LateUpdate()
        {
            var age = AgeController.Instance;
            bool adult = age != null && age.CurrentAge == Age.Adult && !age.IsTransitioning;
            if (adult != _grabOn) { _grabOn = adult; _item.SetGrabEnabled(adult); }

            bool held = _item.IsHeld;
            if (held)
            {
                if (!_wasHeld) _grabStart = attach != null ? attach.position : transform.position;
                float sideways = (transform.position.x - _grabStart.x) * side;     // away from the path
                float back = Mathf.Abs(transform.position.z - _grabStart.z) * 0.3f; // pulling back counts a little
                float k = Mathf.Clamp01((sideways + back) / fullPull);
                if (tunnel != null) tunnel.SetOpen(side, Mathf.Max(side < 0 ? tunnel.OpenLeft : tunnel.OpenRight, k));
                if (k > 0.02f) OVRInput.SetControllerVibration(0.15f, 0.15f * k, OVRInput.Controller.Touch);
            }
            else
            {
                if (_wasHeld) OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
                _item.Body.isKinematic = true;
                if (attach != null)
                {
                    transform.SetPositionAndRotation(attach.position, attach.rotation);
                    _item.Body.position = attach.position; _item.Body.rotation = attach.rotation;
                }
            }
            _wasHeld = held;
        }
    }
}

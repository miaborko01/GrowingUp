using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// The player's mouth: a point just below and in front of the eyes (child of CenterEyeAnchor,
    /// so it scales with the body). Used to blow the dandelion and eat the berry.
    /// </summary>
    public class MouthZone : MonoBehaviour
    {
        public static MouthZone Instance { get; private set; }
        [Tooltip("Radius in rig-local units (scaled by the body: child 0.2 -> 0.03 m).")]
        public float radius = 0.15f;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public float WorldRadius => radius * transform.lossyScale.x;

        /// True if an object (centre + its own radius) is at the mouth.
        public static bool IsAtMouth(Vector3 worldPos, float objectRadius)
        {
            if (Instance == null) return false;
            return Vector3.Distance(worldPos, Instance.transform.position) <= Instance.WorldRadius + objectRadius;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius * transform.lossyScale.x);
        }
    }
}

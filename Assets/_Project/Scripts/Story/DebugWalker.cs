using Oculus.Interaction.Locomotion;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Testing helper: walks the player through the real Meta locomotor (velocity events, like the
    /// thumbstick) for a few seconds, optionally jumping. Used for automated checks in Play mode.
    /// </summary>
    public class DebugWalker : MonoBehaviour
    {
        static DebugWalker _inst;
        Vector3 _dir; float _until, _jumpAt = -1f;

        /// Walk in a world direction (normalised xz) at the current age's walk speed.
        public static void Walk(Vector3 worldDir, float seconds, float jumpAfter = -1f)
        {
            if (_inst == null) _inst = new GameObject("[DebugWalker]").AddComponent<DebugWalker>();
            worldDir.y = 0f;
            _inst._dir = worldDir.normalized;
            _inst._until = Time.time + seconds;
            _inst._jumpAt = jumpAfter >= 0f ? Time.time + jumpAfter : -1f;
        }

        void Update()
        {
            var age = AgeController.Instance;
            if (age == null || age.locomotor == null || Time.time > _until) return;
            var loco = age.locomotor;
            // the locomotor multiplies input velocity by SpeedFactor; send unit input like a full thumbstick
            loco.HandleLocomotionEvent(new LocomotionEvent(4242, new Pose(_dir, Quaternion.identity),
                LocomotionEvent.TranslationType.Velocity, LocomotionEvent.RotationType.None));
            if (_jumpAt > 0f && Time.time >= _jumpAt) { _jumpAt = -1f; loco.Jump(); }
        }
    }
}

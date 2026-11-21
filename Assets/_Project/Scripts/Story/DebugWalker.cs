using Oculus.Interaction.Locomotion;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Testing helper: walks the player through the real Meta locomotor (velocity events, like the
    /// thumbstick) for a few seconds. Optional jump after a delay, or auto-jump whenever something is
    /// in the way (like pressing A at the right moment). Used for checks in Play mode.
    /// </summary>
    public class DebugWalker : MonoBehaviour
    {
        static DebugWalker _inst;
        Vector3 _dir; float _until, _jumpAt = -1f; bool _autoJump; float _speed = 1f;

        /// Walk in a world direction (normalised xz) at the current age's walk speed.
        public static void Walk(Vector3 worldDir, float seconds, float jumpAfter = -1f, bool autoJump = false, float speed = 1f)
        {
            if (_inst == null) _inst = new GameObject("[DebugWalker]").AddComponent<DebugWalker>();
            worldDir.y = 0f;
            _inst._dir = worldDir.normalized;
            _inst._until = Time.time + seconds;
            _inst._jumpAt = jumpAfter >= 0f ? Time.time + jumpAfter : -1f;
            _inst._autoJump = autoJump;
            _inst._speed = speed;
        }

        void Update()
        {
            var age = AgeController.Instance;
            if (age == null || age.locomotor == null || Time.time > _until) return;
            var loco = age.locomotor;
            loco.HandleLocomotionEvent(new LocomotionEvent(4242, new Pose(_dir * _speed, Quaternion.identity),
                LocomotionEvent.TranslationType.Velocity, LocomotionEvent.RotationType.None));
            if (_jumpAt > 0f && Time.time >= _jumpAt) { _jumpAt = -1f; loco.Jump(); }
            if (_autoJump && age.character != null && age.character.IsGrounded)
            {
                var cc = age.character;
                Vector3 feet = cc.Pose.position + Vector3.down * (cc.Height * 0.5f);
                if (Physics.Raycast(feet + Vector3.up * 0.04f, _dir, cc.Radius + 0.12f, cc.LayerMask, QueryTriggerInteraction.Ignore))
                    loco.Jump();
            }
        }
    }
}

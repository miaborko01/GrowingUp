using System.Collections;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Main quest 2: the river (child, step "River").
    /// The path ends at the river with no way across. A little to the RIGHT along the bank a row of
    /// stepping stones crosses it; the child jumps stone to stone (A/X). Touching the water as the
    /// child = splash + respawn on the near bank next to the stones. Reaching the far bank completes
    /// the step. The adult is not affected by the water (it's a shallow brook to them).
    /// </summary>
    public class RiverCrossing : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.River;

        [Header("River (world)")]
        public float riverZ = 13f;
        public float halfWidth = 0.92f;
        [Tooltip("Water surface height: child feet below this inside the river = fell in.")]
        public float waterLevel = 0.035f;

        [Header("Falling in")]
        [Tooltip("Near bank, next to the stepping stones.")]
        public Transform respawnPoint;
        public AudioSource splashSound;
        public ParticleSystem splashParticles;
        public float respawnDelay = 0.7f;

        public bool Crossed { get; private set; }
        bool _respawning;

        void Update()
        {
            var age = AgeController.Instance;
            if (age == null || age.character == null || age.CurrentAge != Age.Child || age.IsTransitioning) return;

            var cc = age.character;
            Vector3 feet = cc.Pose.position + Vector3.down * (cc.Height * 0.5f);
            bool overWater = Mathf.Abs(feet.z - riverZ) < halfWidth - 0.03f;

            if (!_respawning && overWater && feet.y < waterLevel && cc.IsGrounded)
                StartCoroutine(FallIn(feet));

            var story = StoryManager.Instance;
            if (!Crossed && story != null && story.Is(StoryStep.River) && feet.z > riverZ + halfWidth + 0.2f && feet.y > -0.5f)
            {
                Crossed = true;
                story.Complete(StoryStep.River);
            }
        }

        IEnumerator FallIn(Vector3 where)
        {
            _respawning = true;
            if (splashParticles != null) { splashParticles.transform.position = where + Vector3.up * 0.04f; splashParticles.Play(); }
            if (splashSound != null) { splashSound.transform.position = where; splashSound.Play(); }
            yield return new WaitForSeconds(respawnDelay);
            var age = AgeController.Instance;
            if (age != null && respawnPoint != null) age.MovePlayerTo(respawnPoint.position, respawnPoint.rotation);
            yield return new WaitForSeconds(0.3f);
            _respawning = false;
        }

        public void ForceComplete() => Crossed = true;
    }
}

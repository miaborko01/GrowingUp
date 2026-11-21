using System.Collections;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// River crossing (child, step "River").
    /// Stepping stones cross the river with ONE gap too wide to jump. The child picks up the loose rock
    /// on the bank and throws/places it into the gap -> it snaps in and becomes a stepping stone.
    /// Then they jump rock to rock (A/X). Touching the water as the child = splash + respawn on the near
    /// bank. Reaching the far bank completes the step. The placed rock stays there for the adult.
    /// The adult is not affected by the water (it's a shallow brook to them).
    /// </summary>
    public class RiverCrossing : MonoBehaviour, IStoryStation
    {
        public StoryStep Step => StoryStep.River;

        [Header("River (world)")]
        public float riverZ = 8f;
        public float halfWidth = 0.92f;
        [Tooltip("Water surface height: child feet below this inside the river = fell in.")]
        public float waterLevel = 0.035f;

        [Header("Rock puzzle")]
        [Tooltip("On = the loose rock already sits in the gap at start, so the stepping-stone path is complete " +
                 "and the child only has to jump across. Off = the child must carry the rock into the gap.")]
        public bool startWithRockPlaced = true;
        public GrabbableItem looseRock;
        public Transform gapSlot;
        [Tooltip("Released within this horizontal distance (m) of the gap -> snaps in.")]
        public float snapRadius = 0.22f;
        public GameObject gapFoamRing;
        [Tooltip("Rock lying in the water (not in the gap) for this long goes back to the bank.")]
        public float lostRockSeconds = 3f;

        [Header("Falling in")]
        public Transform respawnPoint;
        public AudioSource splashSound;
        public ParticleSystem splashParticles;
        public float respawnDelay = 0.7f;

        public bool RockPlaced { get; private set; }
        public bool Crossed { get; private set; }

        float _rockInWaterTime;
        bool _respawning;

        void Start()
        {
            if (startWithRockPlaced && !RockPlaced && looseRock != null && gapSlot != null) PlaceRock(false);
        }

        void Update()
        {
            var age = AgeController.Instance;
            if (!RockPlaced && looseRock != null) UpdateRock();
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

        void UpdateRock()
        {
            if (looseRock.IsHeld) { _rockInWaterTime = 0f; return; }
            Vector3 p = looseRock.transform.position;
            Vector2 d = new Vector2(p.x - gapSlot.position.x, p.z - gapSlot.position.z);
            bool slow = looseRock.Body.linearVelocity.sqrMagnitude < 0.5f;
            if (d.magnitude < snapRadius && p.y < 0.4f && slow)
            {
                PlaceRock(true);
                return;
            }
            // lost in the water away from the gap -> back to the bank after a moment
            if (Mathf.Abs(p.z - riverZ) < halfWidth && p.y < 0.2f)
            {
                _rockInWaterTime += Time.deltaTime;
                if (_rockInWaterTime > lostRockSeconds) { _rockInWaterTime = 0f; looseRock.ResetToStart(); }
            }
            else _rockInWaterTime = 0f;
        }

        void PlaceRock(bool withFx)
        {
            RockPlaced = true;
            var rot = Quaternion.Euler(0f, looseRock.transform.eulerAngles.y, 0f);
            looseRock.LockInPlace(gapSlot.position, rot);
            if (gapFoamRing != null) gapFoamRing.SetActive(true);
            if (withFx)
            {
                if (splashParticles != null) { splashParticles.transform.position = gapSlot.position + Vector3.up * 0.04f; splashParticles.Play(); }
                if (splashSound != null) { splashSound.transform.position = gapSlot.position; splashSound.Play(); }
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

        public void ForceComplete()
        {
            if (!RockPlaced && looseRock != null) PlaceRock(false);
            Crossed = true;
        }
    }
}

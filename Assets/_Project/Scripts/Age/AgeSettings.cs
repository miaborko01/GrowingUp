using UnityEngine;
using UnityEngine.Rendering;

namespace GrowingUp
{
    public enum Age { Child, Adult }

    /// <summary>
    /// Everything that differs between the two Virtual Subjectivenesses. The world never changes;
    /// only these PLAYER values do (body, movement, perception filters, hearing, music).
    /// </summary>
    [System.Serializable]
    public class AgeProfile
    {
        [Header("Body")]
        [Tooltip("Uniform scale of the whole camera rig (head, hands, IPD). 0.65 = child, 1 = adult. " +
                 "Because hands live inside the rig, this also scales the player's REACH.")]
        public float rigScale = 1f;
        [Tooltip("Approximate eye height in metres for this body (real eye height x rigScale). Used for the " +
                 "collision capsule height (eyeHeight + 0.12 m), which decides what gaps the player fits through.")]
        public float eyeHeight = 1.65f;
        [Tooltip("Collision capsule radius (m). Narrower for the child.")]
        public float bodyRadius = 0.2f;

        [Header("Movement")]
        [Tooltip("Smooth-locomotion walk speed in m/s (converted to the Meta FirstPersonLocomotor speed factor).")]
        public float walkSpeed = 1.1f;
        [Tooltip("Run speed multiplier on top of walk speed (when the locomotor's run is used).")]
        public float runMultiplier = 1.5f;
        [Tooltip("Highest step the player can walk up (m).")]
        public float maxStep = 0.3f;
        [Tooltip("Jump impulse (Meta locomotor units).")]
        public float jumpForce = 2.5f;

        [Header("Perception - vision (part of the player, not the world)")]
        public VolumeProfile volumeProfile;
        public bool fog = false;
        public Color fogColor = new Color(0.62f, 0.66f, 0.70f);
        [Tooltip("Exponential-squared fog density. 0 = no fog.")]
        public float fogDensity = 0f;
        [Tooltip("Child-only magic: sparkle/firefly particles around the player.")]
        public bool sparkles = false;

        [Header("Perception - hearing")]
        [Tooltip("Low-pass cutoff on the listener (Hz). 22000 = open, ~1000 = muffled.")]
        public float lowPassCutoff = 22000f;
        [Tooltip("Perceived loudness of nature sounds (birds, river, wind).")]
        [Range(0f, 2f)] public float natureGain = 1f;
        [Tooltip("Volume of this age's music track.")]
        [Range(0f, 1f)] public float musicVolume = 0.4f;
        [Tooltip("Optional real music clip. Empty = procedural (music box for the child, drone for the adult).")]
        public AudioClip music;
    }

    [CreateAssetMenu(menuName = "Growing Up/Age Settings", fileName = "AgeSettings")]
    public class AgeSettings : ScriptableObject
    {
        public AgeProfile child = new AgeProfile
        {
            rigScale = 0.65f, eyeHeight = 1.05f, bodyRadius = 0.15f,
            walkSpeed = 1.6f, maxStep = 0.25f, jumpForce = 2.8f,
            fog = false, fogDensity = 0f, sparkles = true,
            lowPassCutoff = 22000f, natureGain = 1.4f, musicVolume = 0.35f,
        };

        public AgeProfile adult = new AgeProfile
        {
            rigScale = 1f, eyeHeight = 1.65f, bodyRadius = 0.22f,
            walkSpeed = 1.1f, maxStep = 0.3f, jumpForce = 2.0f,
            fog = true, fogColor = new Color(0.58f, 0.62f, 0.66f), fogDensity = 0.035f, sparkles = false,
            lowPassCutoff = 1400f, natureGain = 0.45f, musicVolume = 0.25f,
        };

        [Header("Growing up")]
        [Tooltip("Seconds for the child -> adult transition.")]
        public float transitionDuration = 3f;
        [Tooltip("Easing of the transition (x = time 0..1, y = blend 0..1).")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public AgeProfile Get(Age a) => a == Age.Child ? child : adult;
    }
}

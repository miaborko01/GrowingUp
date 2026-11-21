using UnityEngine;
using UnityEngine.Rendering;

namespace GrowingUp
{
    public enum Age { Child, Adult }

    /// <summary>
    /// Everything that differs between the two Virtual Subjectivenesses. The world never changes;
    /// only these PLAYER values do (body, movement, perception filters, hearing, music, particles).
    /// </summary>
    [System.Serializable]
    public class AgeProfile
    {
        [Header("Body")]
        [Tooltip("Uniform scale of the whole camera rig (head, hands, IPD). 0.2 = tiny child, 1 = adult. " +
                 "Hands live inside the rig, so this also scales REACH (child arm reach ~0.14 m in world units).")]
        public float rigScale = 1f;
        [Tooltip("Target eye height in metres (real eye height x rigScale). Sets the collision capsule height " +
                 "(eyeHeight / 0.93), which decides what the player fits under.")]
        public float eyeHeight = 1.65f;
        [Tooltip("Collision capsule radius (m).")]
        public float bodyRadius = 0.2f;

        [Header("Movement (world units)")]
        [Tooltip("Smooth-locomotion walk speed in m/s.")]
        public float walkSpeed = 1.2f;
        [Tooltip("Run speed multiplier (thumbstick click in the Meta locomotion block).")]
        public float runMultiplier = 1.5f;
        [Tooltip("Highest step walked up without jumping (m).")]
        public float maxStep = 0.3f;
        [Tooltip("Jump height in metres (A/X button, Meta locomotion block). 0 = jump off.")]
        public float jumpHeight = 0f;
        [Tooltip("Can this body teleport? The child has to walk and jump (river crossing).")]
        public bool teleportEnabled = true;

        [Header("Perception - vision (part of the player, not the world)")]
        public VolumeProfile volumeProfile;
        public bool fog = false;
        public Color fogColor = new Color(0.62f, 0.66f, 0.70f);
        [Tooltip("Exponential-squared fog density. 0 = no fog.")]
        public float fogDensity = 0f;

        [Header("Perception - particles around the player")]
        [Tooltip("Sparkles / pollen floating in the air around the head.")]
        public bool sparkles = false;
        [Tooltip("Butterflies fluttering around the player.")]
        public bool butterflies = false;

        [Header("Perception - hearing")]
        [Tooltip("Low-pass cutoff on the listener (Hz). 22000 = open, ~1000 = muffled.")]
        public float lowPassCutoff = 22000f;
        [Tooltip("Perceived loudness of nature sounds (birds, river, wind).")]
        [Range(0f, 2f)] public float natureGain = 1f;
        [Tooltip("Volume of this age's music track. 0 = music off.")]
        [Range(0f, 1f)] public float musicVolume = 0.4f;
        [Tooltip("Optional real music clip. Empty = procedural placeholder.")]
        public AudioClip music;
    }

    [CreateAssetMenu(menuName = "Growing Up/Age Settings", fileName = "AgeSettings")]
    public class AgeSettings : ScriptableObject
    {
        public AgeProfile child = ChildDefaults();
        public AgeProfile adult = AdultDefaults();

        [Header("Growing up")]
        [Tooltip("Seconds for the child -> adult transition.")]
        public float transitionDuration = 3.5f;
        [Tooltip("Easing of the transition (x = time 0..1, y = blend 0..1).")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public AgeProfile Get(Age a) => a == Age.Child ? child : adult;

        public static AgeProfile ChildDefaults() => new AgeProfile
        {
            // 60 cm tall child (1.8 m adult x 0.333): taller than every mushroom, small next to bushes and logs
            rigScale = 0.333f, eyeHeight = 0.56f, bodyRadius = 0.09f,
            walkSpeed = 1.5f, runMultiplier = 1.4f, maxStep = 0.06f, jumpHeight = 0.3f, teleportEnabled = false,
            fog = false, fogDensity = 0f, sparkles = true, butterflies = true,
            lowPassCutoff = 22000f, natureGain = 1.4f, musicVolume = 0.35f,
        };

        public static AgeProfile AdultDefaults() => new AgeProfile
        {
            // 1.8 m adult: slow and careful, big enough to jump the log, too big for the bush tunnel
            rigScale = 1f, eyeHeight = 1.68f, bodyRadius = 0.22f,
            walkSpeed = 0.8f, runMultiplier = 1.3f, maxStep = 0.35f, jumpHeight = 0.55f, teleportEnabled = false,
            fog = true, fogColor = new Color(0.66f, 0.58f, 0.54f), fogDensity = 0.022f, sparkles = false, butterflies = false,
            lowPassCutoff = 1400f, natureGain = 0.45f, musicVolume = 0.3f,
        };
    }
}

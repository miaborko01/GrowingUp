using System;
using System.Collections;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GrowingUp
{
    /// <summary>
    /// Applies the CHILD / ADULT profile (AgeSettings) to the player: rig scale (body + reach), locomotion
    /// (speed, step, jump, teleport, capsule), and perception (URP volumes, fog, sparkles, butterflies,
    /// hearing, music). Never touches the environment.
    ///
    /// Blend parameter: 0 = child, 1 = adult. GrowUp() animates 0 -> 1 over the configured duration.
    /// Debug: G key (Game view focused) or left controller Menu button toggles the age,
    /// or use the component's context menu (⋮) in the Inspector during Play mode.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class AgeController : MonoBehaviour
    {
        public static AgeController Instance { get; private set; }

        [Header("Data")]
        public AgeSettings settings;
        public Age startAge = Age.Child;

        [Header("Player rig (Meta Building Blocks)")]
        public OVRCameraRig cameraRig;
        public FirstPersonLocomotor locomotor;
        public Oculus.Interaction.Locomotion.CharacterController character;

        [Header("Perception (children of the player)")]
        public Volume childVolume;
        public Volume adultVolume;
        public ParticleSystem sparkles;
        public ParticleSystem butterflies;
        public AudioSource childMusic;
        public AudioSource adultMusic;
        public AudioLowPassFilter listenerLowPass;

        [Header("Debug")]
        public bool debugToggleEnabled = true;

        public Age CurrentAge { get; private set; }
        public bool IsTransitioning { get; private set; }
        /// 0 = child, 1 = adult
        public float Blend { get; private set; }
        public float RigScale => cameraRig != null ? cameraRig.transform.localScale.x : 1f;
        public Transform Head => cameraRig != null ? cameraRig.centerEyeAnchor : null;

        public event Action<Age> AgeChanged;
        public event Action GrowUpStarted;

        float _sparkleRate = -1f, _butterflyRate = -1f;
        Coroutine _transition;
        TeleportInteractor[] _teleporters;

        void Awake()
        {
            Instance = this;
            if (cameraRig == null) cameraRig = FindAnyObjectByType<OVRCameraRig>();
            if (locomotor == null) locomotor = FindAnyObjectByType<FirstPersonLocomotor>();
            if (character == null && locomotor != null) character = locomotor.GetComponent<Oculus.Interaction.Locomotion.CharacterController>();
            if (listenerLowPass == null && cameraRig != null)
            {
                var ear = cameraRig.centerEyeAnchor.gameObject;
                listenerLowPass = ear.GetComponent<AudioLowPassFilter>();
                if (listenerLowPass == null) listenerLowPass = ear.AddComponent<AudioLowPassFilter>();
            }
            if (cameraRig != null) _teleporters = cameraRig.GetComponentsInChildren<TeleportInteractor>(true);
            if (sparkles != null) _sparkleRate = sparkles.emission.rateOverTimeMultiplier;
            if (butterflies != null) _butterflyRate = butterflies.emission.rateOverTimeMultiplier;
            if (childMusic != null && settings != null && settings.child.music != null) childMusic.clip = settings.child.music;
            if (adultMusic != null && settings != null && settings.adult.music != null) adultMusic.clip = settings.adult.music;
        }

        void Start()
        {
            SetAgeImmediate(startAge);
            if (childMusic != null && !childMusic.isPlaying && childMusic.clip != null) childMusic.Play();
            if (adultMusic != null && !adultMusic.isPlaying && adultMusic.clip != null) adultMusic.Play();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PerceptionMix.Nature = 1f; PerceptionMix.Music = 1f;
        }

        void Update()
        {
            if (!debugToggleEnabled || IsTransitioning) return;
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) pressed = true;
#endif
            if (OVRInput.GetDown(OVRInput.Button.Start)) pressed = true;
            if (pressed) DebugToggle();
        }

        // ------------------------------------------------------------------ public API
        [ContextMenu("Debug: Toggle Child <-> Adult (animated)")]
        public void DebugToggle()
        {
            if (CurrentAge == Age.Child) GrowUp();
            else BecomeChildAgain();
        }

        [ContextMenu("Debug: Set Child (instant)")] void CtxChild() => SetAgeImmediate(Age.Child);
        [ContextMenu("Debug: Set Adult (instant)")] void CtxAdult() => SetAgeImmediate(Age.Adult);

        /// Starts the child -> adult transition (Phase 3: called when the child eats the berry).
        public void GrowUp()
        {
            if (CurrentAge == Age.Adult || IsTransitioning) return;
            GrowUpStarted?.Invoke();
            StartBlend(1f, Age.Adult);
        }

        /// Debug only: animate back to the child.
        public void BecomeChildAgain()
        {
            if (CurrentAge == Age.Child || IsTransitioning) return;
            StartBlend(0f, Age.Child);
        }

        public void SetAgeImmediate(Age age)
        {
            if (_transition != null) { StopCoroutine(_transition); _transition = null; IsTransitioning = false; }
            ApplyBlend(age == Age.Child ? 0f : 1f);
            CurrentAge = age;
            AgeChanged?.Invoke(age);
        }

        /// Moves the player (feet) to a world position and facing, through the Meta locomotor so the
        /// capsule and the camera rig stay in sync. Used by the river respawn and the debug step jumps.
        public void MovePlayerTo(Vector3 feetPosition, Quaternion facing)
        {
            if (locomotor != null)
            {
                var pose = new Pose(feetPosition, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
                locomotor.HandleLocomotionEvent(new LocomotionEvent(0, pose,
                    LocomotionEvent.TranslationType.Absolute, LocomotionEvent.RotationType.Absolute));
            }
            else if (cameraRig != null)
            {
                cameraRig.transform.SetPositionAndRotation(feetPosition, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
            }
        }

        void StartBlend(float target, Age resultAge)
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(BlendRoutine(target, resultAge));
        }

        IEnumerator BlendRoutine(float target, Age resultAge)
        {
            IsTransitioning = true;
            float from = Blend, dur = settings != null ? Mathf.Max(0.01f, settings.transitionDuration) : 3f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
            {
                float k = settings != null ? settings.transitionCurve.Evaluate(t) : t;
                ApplyBlend(Mathf.Lerp(from, target, k));
                yield return null;
            }
            ApplyBlend(target);
            CurrentAge = resultAge;
            IsTransitioning = false;
            _transition = null;
            AgeChanged?.Invoke(resultAge);
        }

        // ------------------------------------------------------------------ apply
        void ApplyBlend(float b)
        {
            Blend = b;
            if (settings == null) { Debug.LogWarning("[AgeController] No AgeSettings assigned."); return; }
            var c = settings.child; var a = settings.adult;

            ApplyBody(Mathf.Lerp(c.rigScale, a.rigScale, b), Mathf.Lerp(c.eyeHeight, a.eyeHeight, b), Mathf.Lerp(c.bodyRadius, a.bodyRadius, b));
            ApplyMovement(Mathf.Lerp(c.walkSpeed, a.walkSpeed, b), Mathf.Lerp(c.runMultiplier, a.runMultiplier, b),
                Mathf.Lerp(c.maxStep, a.maxStep, b), Mathf.Lerp(c.jumpHeight, a.jumpHeight, b),
                b < 0.5f ? c.teleportEnabled : a.teleportEnabled);

            // Vision: cross-fade the two global volumes (they live under the player, not the world)
            if (childVolume != null) { childVolume.sharedProfile = c.volumeProfile; childVolume.weight = 1f - b; }
            if (adultVolume != null) { adultVolume.sharedProfile = a.volumeProfile; adultVolume.weight = b; }

            float fogC = c.fog ? c.fogDensity : 0f, fogA = a.fog ? a.fogDensity : 0f;
            float fog = Mathf.Lerp(fogC, fogA, b);
            RenderSettings.fog = fog > 0.0001f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fog;
            RenderSettings.fogColor = Color.Lerp(c.fogColor, a.fogColor, b);

            FadeParticles(sparkles, ref _sparkleRate, Mathf.Lerp(c.sparkles ? 1f : 0f, a.sparkles ? 1f : 0f, b));
            FadeParticles(butterflies, ref _butterflyRate, Mathf.Lerp(c.butterflies ? 1f : 0f, a.butterflies ? 1f : 0f, b));

            // Hearing: listener low-pass (log blend), nature loudness, music cross-fade
            if (listenerLowPass != null)
            {
                listenerLowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(c.lowPassCutoff), Mathf.Log(a.lowPassCutoff), b));
                listenerLowPass.enabled = listenerLowPass.cutoffFrequency < 21000f;
            }
            PerceptionMix.Nature = Mathf.Lerp(c.natureGain, a.natureGain, b);
            if (childMusic != null) childMusic.volume = c.musicVolume * Mathf.Clamp01(1f - b / 0.6f);
            if (adultMusic != null) adultMusic.volume = a.musicVolume * Mathf.Clamp01((b - 0.4f) / 0.6f);
        }

        static void FadeParticles(ParticleSystem ps, ref float baseRate, float amount)
        {
            if (ps == null) return;
            var em = ps.emission;
            if (baseRate < 0f) baseRate = em.rateOverTimeMultiplier;
            em.rateOverTimeMultiplier = baseRate * amount;
            if (amount <= 0.001f) { if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            else if (!ps.isPlaying) ps.Play(true);
        }

        void ApplyBody(float scale, float eyeHeight, float radius)
        {
            if (cameraRig == null) return;
            var rig = cameraRig.transform;
            if (Mathf.Abs(rig.localScale.x - scale) > 0.0001f)
            {
                // Scale around the HEAD's ground position so the player doesn't slide sideways;
                // the rig origin is at floor level, so feet stay on the ground.
                Vector3 head = cameraRig.centerEyeAnchor.position;
                Vector3 capsulePos = character != null ? character.transform.position : Vector3.zero;
                rig.localScale = Vector3.one * scale;
                Vector3 headAfter = cameraRig.centerEyeAnchor.position;
                rig.position += new Vector3(head.x - headAfter.x, 0f, head.z - headAfter.z);
                // The locomotion capsule is a child of the rig; keep it where it was (it works in world units).
                if (character != null) character.transform.position = capsulePos;
            }
            if (locomotor != null)
            {
                locomotor.AutoUpdateHeight = false;
                locomotor.DefaultHeight = eyeHeight / 0.93f;
            }
            if (character != null)
            {
                var cap = character.GetComponent<CapsuleCollider>();
                if (cap != null) cap.radius = radius;
            }
        }

        void ApplyMovement(float walk, float runMul, float step, float jumpHeight, bool teleport)
        {
            if (locomotor != null)
            {
                // Terminal ground speed of FirstPersonLocomotor = Acceleration * SpeedFactor / GroundDamping
                float k = locomotor.GroundDamping / Mathf.Max(0.0001f, locomotor.Acceleration);
                locomotor.SpeedFactor = walk * k;
                locomotor.RunningSpeedFactor = walk * runMul * k;
                // Jump (A/X button in the Meta locomotion block) adds an upward velocity: v = sqrt(2 g h)
                float g = Mathf.Abs(Physics.gravity.y) * Mathf.Max(0.01f, locomotor.GravityFactor);
                locomotor.JumpForce = jumpHeight > 0.001f ? Mathf.Sqrt(2f * g * jumpHeight) : 0f;
            }
            if (character != null) character.MaxStep = step;
            if (_teleporters != null)
                foreach (var t in _teleporters) if (t != null) t.enabled = teleport;
        }
    }
}

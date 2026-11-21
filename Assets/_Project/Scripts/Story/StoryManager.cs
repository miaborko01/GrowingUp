using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GrowingUp
{
    public enum StoryStep { Intro, Log, River, BushTunnel, Berries, GrowingUp, AdultReturn, Ending }

    /// <summary>Implemented by every main quest along the path (log, river, bush tunnel, berries, ...).</summary>
    public interface IStoryStation
    {
        StoryStep Step { get; }
        /// Put the station into its "done" state instantly (used when debug-skipping past it).
        void ForceComplete();
    }

    /// <summary>
    /// One simple state machine for the whole experience: steps unlock strictly in order.
    /// Interactions call Complete(theirStep) when finished; only the CURRENT step can complete.
    ///
    /// Side quests (glowing mushrooms, dandelion field) are free and not steps.
    ///
    /// Debug (Game view focused): number keys 1-8 jump to Intro, Log, River, BushTunnel, Berries,
    /// GrowingUp, AdultReturn, Ending (earlier stations are auto-completed, player is moved to the
    /// station). N = complete the current step. Also available in the component's context menu.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class StoryManager : MonoBehaviour
    {
        public static StoryManager Instance { get; private set; }

        public StoryStep startStep = StoryStep.Intro;
        [Tooltip("Parent of the Station_* spawn points (World/Stations).")]
        public Transform stationsRoot;
        public bool debugKeys = true;

        public StoryStep Current { get; private set; }
        public event Action<StoryStep> StepChanged;

        readonly List<IStoryStation> _stations = new List<IStoryStation>();

        void Awake()
        {
            Instance = this;
            Current = startStep;
            if (stationsRoot == null)
            {
                var s = GameObject.Find("World/Stations");
                if (s != null) stationsRoot = s.transform;
            }
        }

        void Start()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (mb is IStoryStation st) _stations.Add(st);
            Debug.Log("[Story] Start at " + Current + " (" + _stations.Count + " stations)");
            if (startStep != StoryStep.Intro) GoTo(startStep);
            else StepChanged?.Invoke(Current);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool Is(StoryStep s) => Current == s;
        public bool Reached(StoryStep s) => Current >= s;

        /// Called by an interaction when it is finished. Ignored unless it is the current step.
        public void Complete(StoryStep s)
        {
            if (s != Current || Current == StoryStep.Ending) return;
            Current = s + 1;
            Debug.Log("[Story] " + s + " done -> " + Current);
            StepChanged?.Invoke(Current);
        }

        /// Debug / testing: jump to a step. Every station before it is put into its done state, the
        /// player is moved to that step's station spawn point, and the age matches the step.
        public void GoTo(StoryStep s)
        {
            foreach (var st in _stations)
                if (st.Step < s) st.ForceComplete();
            Current = s;

            var age = AgeController.Instance;
            if (age != null)
            {
                var want = s >= StoryStep.AdultReturn ? Age.Adult : Age.Child;
                if (age.CurrentAge != want) age.SetAgeImmediate(want);
                var spawn = SpawnFor(s);
                if (spawn != null) age.MovePlayerTo(spawn.position, spawn.rotation);
            }
            Debug.Log("[Story] Jumped to " + s);
            StepChanged?.Invoke(Current);
        }

        public Transform SpawnFor(StoryStep s)
        {
            if (stationsRoot == null) return null;
            string n = s == StoryStep.GrowingUp ? "Station_Berries" : "Station_" + s;
            return stationsRoot.Find(n);
        }

        void Update()
        {
            if (!debugKeys) return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };
            for (int i = 0; i < keys.Length; i++)
                if (kb[keys[i]].wasPressedThisFrame) GoTo((StoryStep)i);
            if (kb.nKey.wasPressedThisFrame) Complete(Current);
#endif
        }

        [ContextMenu("Debug/Go to Intro")] void D0() => GoTo(StoryStep.Intro);
        [ContextMenu("Debug/Go to Log")] void D1() => GoTo(StoryStep.Log);
        [ContextMenu("Debug/Go to River")] void D2() => GoTo(StoryStep.River);
        [ContextMenu("Debug/Go to Bush Tunnel")] void D3() => GoTo(StoryStep.BushTunnel);
        [ContextMenu("Debug/Go to Berries")] void D4() => GoTo(StoryStep.Berries);
        [ContextMenu("Debug/Go to Growing Up (transformation)")] void D5() => GoTo(StoryStep.GrowingUp);
        [ContextMenu("Debug/Go to Adult Return")] void D6() => GoTo(StoryStep.AdultReturn);
        [ContextMenu("Debug/Complete current step")] void DN() => Complete(Current);
    }
}

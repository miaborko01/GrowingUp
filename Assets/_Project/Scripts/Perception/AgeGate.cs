using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Shows things only to one age, following the age blend (0 child .. 1 adult):
    ///  - childOnly: the child's magic (fairy-ring glow, motes...) - hidden for the adult;
    ///  - adultOnlySounds: dusk sounds (owls) that fade in as the player grows up.
    /// </summary>
    public class AgeGate : MonoBehaviour
    {
        public GameObject[] childOnly;
        public AudioSource[] adultOnlySounds;
        public float adultVolume = 0.6f;

        float _last = -1f;

        void Update()
        {
            var age = AgeController.Instance;
            float b = age != null ? age.Blend : 0f;
            if (Mathf.Abs(b - _last) < 0.001f) return;
            _last = b;
            if (childOnly != null)
                foreach (var g in childOnly) if (g != null && g.activeSelf != (b < 0.5f)) g.SetActive(b < 0.5f);
            if (adultOnlySounds != null)
                foreach (var s in adultOnlySounds)
                {
                    if (s == null) continue;
                    s.volume = adultVolume * Mathf.Clamp01((b - 0.5f) * 2f);
                    if (s.volume > 0.001f && !s.isPlaying) s.Play();
                    if (s.volume <= 0.001f && s.isPlaying) s.Stop();
                }
        }
    }
}

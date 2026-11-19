using System.Collections;
using TMPro;
using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Head-locked subtitle line in front of the player (child of CenterEyeAnchor, so it scales with the
    /// player and is always readable). Used for the mother's voice line, also as a stand-in until the real
    /// recording is dropped in.
    /// </summary>
    public class Subtitles : MonoBehaviour
    {
        public static Subtitles Instance { get; private set; }
        public TextMeshPro text;
        public float fadeTime = 0.4f;
        Coroutine _co;

        void Awake()
        {
            Instance = this;
            if (text == null) text = GetComponent<TextMeshPro>();
            if (text != null) { text.text = ""; text.alpha = 0f; }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Show(string line, float seconds)
        {
            if (text == null) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(ShowRoutine(line, seconds));
        }

        public void Hide()
        {
            if (_co != null) StopCoroutine(_co);
            if (text != null) { text.alpha = 0f; text.text = ""; }
        }

        IEnumerator ShowRoutine(string line, float seconds)
        {
            text.text = line;
            for (float t = 0; t < 1f; t += Time.deltaTime / fadeTime) { text.alpha = t; yield return null; }
            text.alpha = 1f;
            yield return new WaitForSeconds(Mathf.Max(0f, seconds - 2f * fadeTime));
            for (float t = 1; t > 0f; t -= Time.deltaTime / fadeTime) { text.alpha = t; yield return null; }
            Hide();
        }
    }
}

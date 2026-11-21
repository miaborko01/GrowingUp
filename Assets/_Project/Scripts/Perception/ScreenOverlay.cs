using UnityEngine;

namespace GrowingUp
{
    /// <summary>
    /// Head-locked colour overlay (child of CenterEyeAnchor, part of the PLAYER): flashes during the
    /// transformation and the final fade to black. Uses GrowingUp/ScreenFade on a small inside-out sphere.
    /// </summary>
    public class ScreenOverlay : MonoBehaviour
    {
        public static ScreenOverlay Instance { get; private set; }
        public Renderer sphere;
        Material _mat;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public Color Current { get; private set; }

        void Awake()
        {
            Instance = this;
            if (sphere == null) sphere = GetComponent<Renderer>();
            if (sphere != null) _mat = sphere.material;
            Set(Color.clear);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Set(Color c)
        {
            Current = c;
            if (_mat != null) _mat.SetColor(ColorId, c);
            if (sphere != null) sphere.enabled = c.a > 0.002f;
        }
    }
}

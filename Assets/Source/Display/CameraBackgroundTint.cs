using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>
    ///     The scene light's diffuse term for the camera's solid clear colour — the same
    ///     albedo × light response Sprite/Diffuse gives sprites, applied to the background. Owns
    ///     the AUTHORED base colour (the camera's backgroundColor becomes derived state, so the
    ///     multiply can never compound on itself). All inputs (the authored colour, the light
    ///     influence, and the scene light settings asset) are static once injected, so the tint is
    ///     applied once in OnEnable; only the editor keeps re-applying every frame, so inspector
    ///     tweaks still preview live in edit mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraBackgroundTint : MonoBehaviour
    {
        [Tooltip("The authored background colour — what the sky looks like under a neutral " +
                 "(white × 1) light. The camera's backgroundColor is derived from this.")]
        [SerializeField] private Color _baseColor = Color.white;

        [Tooltip("0 = unlit (authored colour always), 1 = full albedo × light response.")]
        [Range(0f, 1f)] [SerializeField] private float _lightInfluence = 1f;

        [Inject] private ISceneLightRuntime _lightRuntime;

        private Camera _camera;

        private void OnEnable()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void Update()
        {
            // Keep re-applying every frame in the editor — edit OR play mode — so live tuning of the
            // base colour, light influence, or the light settings asset previews instantly (the
            // TimeOfDayService re-pushes its globals per-tick in-editor for the same reason; the sky must
            // track the sprites it sits behind). Device builds apply once in OnEnable: there the
            // inputs really are static.
            Apply();
        }
#endif

        private void Reset()
        {
            // Adding the component adopts the camera's current colour as the authored base, so
            // wiring it up is a no-op visually.
            _baseColor = GetComponent<Camera>().backgroundColor;
        }

        private void Apply()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            // In edit mode (no DI) fall back to neutral — shaders handle the same fallback via
            // their alpha-validity flag, so the sky won't be mis-tinted.
            var tint = _lightRuntime != null
                ? _lightRuntime.CurrentColor * _lightRuntime.CurrentIntensity
                : Color.white;

            var lit = Color.Lerp(Color.white, tint, _lightInfluence);
            var background = _baseColor * lit;
            background.a = _baseColor.a;
            _camera.backgroundColor = background;
        }
    }
}

using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>
    ///     The scene light's diffuse term for the camera's solid clear colour — the same
    ///     albedo × light response Sprite/Diffuse gives sprites, applied to the background. Owns
    ///     the AUTHORED base colour (the camera's backgroundColor becomes derived state, so the
    ///     multiply can never compound on itself). Re-applies every frame but only writes the camera
    ///     when the derived colour actually changes, so it tracks the night-mode time-of-day sweep
    ///     (<see cref="TimeOfDayService.CurrentColor"/> shifts at runtime as the level-up transition
    ///     sweeps the light) yet costs a colour compare — no camera write — while the light is static.
    ///     The same per-frame pass previews inspector tweaks live in edit mode.
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
        private Color _lastApplied;
        private bool _hasApplied;

        private void OnEnable()
        {
            _hasApplied = false;
            Apply();
        }

        private void Update()
        {
            Apply();
        }

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

            if (_hasApplied && background == _lastApplied)
            {
                return;
            }

            _camera.backgroundColor = background;
            _lastApplied = background;
            _hasApplied = true;
        }
    }
}

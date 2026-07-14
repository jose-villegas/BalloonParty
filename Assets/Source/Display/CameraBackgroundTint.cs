using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     The scene light's diffuse term for the camera's solid clear colour — the same
    ///     albedo × light response Sprite/Diffuse gives sprites, applied to the background. Owns
    ///     the AUTHORED base colour (the camera's backgroundColor becomes derived state, so the
    ///     multiply can never compound on itself). Applies in Update so every LateUpdate consumer
    ///     of camera.backgroundColor (the GI's ambient reference, the scene capture's clear)
    ///     reads the already-tinted value the same frame.
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

        private Camera _camera;
        private SceneLightService _sceneLight;

        private void Update()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            // The owner lives on a scene object (not this prefab) — resolve lazily and tolerate
            // scenes without one (neutral tint, authored colour shows as-is).
            if (_sceneLight == null)
            {
                _sceneLight = FindFirstObjectByType<SceneLightService>();
            }

            var tint = _sceneLight != null
                ? _sceneLight.LightColor * _sceneLight.Intensity
                : Color.white;

            var lit = Color.Lerp(Color.white, tint, _lightInfluence);
            var background = _baseColor * lit;
            background.a = _baseColor.a;
            _camera.backgroundColor = background;
        }

        private void Reset()
        {
            // Adding the component adopts the camera's current colour as the authored base, so
            // wiring it up is a no-op visually.
            _baseColor = GetComponent<Camera>().backgroundColor;
        }
    }
}

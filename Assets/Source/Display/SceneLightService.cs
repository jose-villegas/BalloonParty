using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Single owner of the scene's 2D light direction, published as the global shader property
    ///     <c>_SceneLightDir</c> (see @ref plan_lighting). Convention: normalized, screen/world XY
    ///     (+y up), pointing TOWARD the light — shadows extend the opposite way. Consumers whose math
    ///     wants the light-travel direction (the GI smear's march, the tough grain) negate it.
    ///     Every light-direction/specular consumer reads this one value; nothing else authors a light
    ///     direction. [ExecuteAlways] + OnValidate keep the global alive in edit mode, where shaders
    ///     would otherwise normalize a zero vector while authoring.
    /// </summary>
    [ExecuteAlways]
    public class SceneLightService : MonoBehaviour
    {
        private static readonly int SceneLightDirId = Shader.PropertyToID("_SceneLightDir");

        [Tooltip("Points TOWARD the light (normalized on push); shadows extend the opposite way. " +
                 "The canonical scene light sits upper-left.")]
        [SerializeField] private Vector2 _lightDirection = new(-0.707f, 0.707f);

        /// <summary>The normalized toward-the-light vector — for CPU consumers that derive their own
        /// shader params from it (the GI smear cannot read the global in-shader).</summary>
        public Vector2 Direction =>
            // A degenerate authored vector falls back to light-from-above rather than NaN.
            _lightDirection.sqrMagnitude > 0.0001f ? _lightDirection.normalized : Vector2.up;

        private void OnEnable()
        {
            Push();
        }

        // Pushed every frame so the knob stays live-tunable in play mode (the Display-service idiom);
        // deliberately unconditional — unlike the GI chain, this global must never go stale.
        private void LateUpdate()
        {
            Push();
        }

        private void OnValidate()
        {
            Push();
        }

        private void Push()
        {
            Shader.SetGlobalVector(SceneLightDirId, Direction);
        }
    }
}

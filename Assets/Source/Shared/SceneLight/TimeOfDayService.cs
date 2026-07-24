using BalloonParty.Configuration.Effects;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared.SceneLight
{
    /// <summary>
    ///     Owns the LIVE ambient scene light and is the single writer of its shader globals
    ///     (<c>_SceneLightDir</c>/<c>_SceneLightColor</c>/<c>_SceneLightIntensity</c>). A passive holder:
    ///     it publishes whichever toward-light direction it's given — the authored rest value, or the
    ///     swept target that the Game-layer <c>TimeOfDayCycle</c> pushes via <see cref="SetDirection"/>
    ///     while night mode is on (see @ref plan_night_mode) — deriving the colour/intensity and exposing
    ///     them both as globals and through <see cref="ISceneLightRuntime"/> for the CPU-side consumers
    ///     (sky tint, GI) that can't read the globals. Replaces the ambient push formerly bolted onto
    ///     <see cref="SceneLightFieldService"/>, which now owns only the local light field.
    /// </summary>
    internal sealed class TimeOfDayService : IStartable, ITickable, ISceneLightRuntime
    {
        private static readonly int SceneLightDirId = Shader.PropertyToID("_SceneLightDir");
        private static readonly int SceneLightColorId = Shader.PropertyToID("_SceneLightColor");
        private static readonly int SceneLightIntensityId = Shader.PropertyToID("_SceneLightIntensity");

        private readonly ISceneLightSettings _settings;
        private readonly ITimeOfDaySettings _timeOfDay;

        private Vector2 _currentDirection;

        public Vector2 CurrentDirection => _currentDirection;
        public Color CurrentColor => _settings.EvaluateColor(_currentDirection);
        public float CurrentIntensity => _settings.Intensity;

        internal TimeOfDayService(ISceneLightSettings settings, ITimeOfDaySettings timeOfDay)
        {
            _settings = settings;
            _timeOfDay = timeOfDay;

            // Seed the direction here, not in Start: VContainer runs IStartable.Start AFTER Unity's
            // Awake/OnEnable in the same frame, and CameraBackgroundTint reads CurrentColor from OnEnable —
            // a Start-only seed would let it sample the gradient at the wrong direction for one frame.
            _currentDirection = _settings.LightDirection;
        }

        void IStartable.Start()
        {
            PushGlobals();
        }

        void ITickable.Tick()
        {
#if UNITY_EDITOR
            // Editor-only re-push so live inspector tuning of the settings asset previews in play mode.
            // When night mode is on, the direction is owned by TimeOfDayCycle (the level sweep) — don't
            // clobber it; only re-push so gradient/intensity edits still preview. When off, re-read the
            // authored direction so dragging the UnitCircle relights live (the Phase-1 behaviour).
            if (!_timeOfDay.NightModeEnabled)
            {
                _currentDirection = _settings.LightDirection;
            }

            PushGlobals();
#endif
        }

        /// <summary>Sets the current toward-light direction and republishes the globals — the write path
        /// for the night-mode sweep (<see cref="TimeOfDayService"/> stays a passive owner; the cycle policy
        /// lives in TimeOfDayCycle).</summary>
        internal void SetDirection(Vector2 direction)
        {
            _currentDirection = direction;
            PushGlobals();
        }

        private void PushGlobals()
        {
            Shader.SetGlobalVector(SceneLightDirId, _currentDirection);

            // Alpha = 1 is the "owner has pushed" validity flag: shaders fall back to a neutral tint
            // when it's 0 (edit time without this service running).
            var color = _settings.EvaluateColor(_currentDirection);
            color.a = 1f;
            Shader.SetGlobalColor(SceneLightColorId, color);
            Shader.SetGlobalFloat(SceneLightIntensityId, _settings.Intensity);
        }
    }
}

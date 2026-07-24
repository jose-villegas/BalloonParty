using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Extensions;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared.SceneLight
{
    /// <summary>
    ///     Night-mode <see cref="TimeOfDaySource.Realtime"/> driver: rotates the ambient light direction
    ///     continuously on a wall-clock, one full circle per <see cref="ITimeOfDaySettings.SecondsPerCycle"/>
    ///     (unscaled time, so it runs through pause and the level-up freeze). Starts at the authored rest
    ///     direction and rotates forward from there. Inert unless night mode is on AND the source is
    ///     Realtime — the level-paced alternative is <c>TimeOfDayCycle</c> (<c>Game/Level</c>); only one is
    ///     ever live, gated by the source. Owns policy only; the ambient state/push lives on
    ///     <see cref="TimeOfDayService"/> (see @ref plan_night_mode).
    ///
    ///     The angle is per-scope: it resets to the rest direction whenever this scope loads (game or
    ///     launcher), rather than persisting a single clock across scene loads.
    /// </summary>
    internal sealed class TimeOfDayClock : IStartable, ITickable
    {
        private readonly ITimeOfDaySettings _settings;
        private readonly ISceneLightSettings _lightSettings;
        private readonly TimeOfDayService _service;

        private float _angleDegrees;
        private bool _active;

        internal TimeOfDayClock(
            ITimeOfDaySettings settings, ISceneLightSettings lightSettings, TimeOfDayService service)
        {
            _settings = settings;
            _lightSettings = lightSettings;
            _service = service;
        }

        void IStartable.Start()
        {
            _active = _settings.NightModeEnabled && _settings.Source == TimeOfDaySource.Realtime;
            if (!_active)
            {
                return;
            }

            _angleDegrees = _lightSettings.LightDirection.Angle01() * 360f;
            Apply();
        }

        void ITickable.Tick()
        {
            if (!_active)
            {
                return;
            }

            var period = _settings.SecondsPerCycle;
            if (period > 0f)
            {
                // Wrapped to [0,360) so a long session never drifts on float precision — the direction is
                // identical either way, and it still advances forward every frame (never reverses).
                _angleDegrees = Mathf.Repeat(_angleDegrees + Time.unscaledDeltaTime * (360f / period), 360f);
            }

            Apply();
        }

        private void Apply()
        {
            _service.SetDirection(VectorMathExtensions.DirectionFromAngle(_angleDegrees * Mathf.Deg2Rad));
        }
    }
}

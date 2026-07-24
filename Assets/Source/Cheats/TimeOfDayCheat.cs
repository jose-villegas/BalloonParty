#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

using System.Collections.Generic;
using BalloonParty.Shared.SceneLight;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Scrub the time-of-day angle and change how fast the Realtime clock advances it — including 0
    ///     to freeze. The angle drives <see cref="TimeOfDayClock" /> directly (works for any source, held
    ///     until the active driver next writes); the speed is a multiplier the clock reads from
    ///     <see cref="CheatState.TimeOfDaySpeedScale" />, so it only advances under the Realtime source.
    /// </summary>
    internal class TimeOfDayCheat : ICheat, ICheatControls
    {
        private static readonly string[] SpeedPresets = { "Freeze", "1x", "8x", "30x" };
        private static readonly float[] SpeedPresetValues = { 0f, 1f, 8f, 30f };

        private readonly TimeOfDayClock _clock;

        private float _angle;

        public string Name => "Time of Day";
        public string Section => "Lighting";
        public IReadOnlyList<string> Tags => new[] { "lighting", "time of day", "night" };
        public bool Compact => false;

        public TimeOfDayCheat(TimeOfDayClock clock)
        {
            _clock = clock;
        }

        public void Execute()
        {
            _clock.SetAngleDegrees(_angle);
        }

        public void DrawControls()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Angle {_angle:0}° (now {_clock.CurrentAngleDegrees:0}°)", GUILayout.Width(150));
            var newAngle = GUILayout.HorizontalSlider(_angle, 0f, 360f);
            if (!Mathf.Approximately(newAngle, _angle))
            {
                _angle = newAngle;
                _clock.SetAngleDegrees(_angle);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Speed {CheatState.TimeOfDaySpeedScale:0.##}x", GUILayout.Width(90));
            CheatState.TimeOfDaySpeedScale =
                GUILayout.HorizontalSlider(CheatState.TimeOfDaySpeedScale, 0f, 60f);
            GUILayout.EndHorizontal();

            var preset = CheatLayout.ButtonGrid(SpeedPresets);
            if (preset >= 0)
            {
                CheatState.TimeOfDaySpeedScale = SpeedPresetValues[preset];
            }
        }
    }
}
#endif

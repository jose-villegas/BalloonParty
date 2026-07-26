#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

using System.Collections.Generic;
using BalloonParty.Configuration.Effects;
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
        private static readonly Color NightBandColor = new Color(0.25f, 0.3f, 0.6f, 0.45f);

        private readonly TimeOfDayClock _clock;
        private readonly ITimeOfDayNight _night;
        private readonly ITimeOfDaySettings _settings;

        private float _angle;

        public string Name => "Time of Day";
        public string Section => "Lighting";
        public IReadOnlyList<string> Tags => new[] { "lighting", "time of day", "night" };
        public bool Compact => false;

        public TimeOfDayCheat(TimeOfDayClock clock, ITimeOfDayNight night, ITimeOfDaySettings settings)
        {
            _clock = clock;
            _night = night;
            _settings = settings;
        }

        public void Execute()
        {
            _clock.SetAngleDegrees(_angle);
        }

        public void DrawControls()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Angle {_angle:0}° (now {_clock.CurrentAngleDegrees:0}° · {(_night.IsNight ? "NIGHT" : "day")})",
                GUILayout.Width(190));
            var sliderRect =
                GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));
            DrawNightBand(sliderRect, _settings.NightStartAngle, _settings.NightEndAngle);
            var newAngle = GUI.HorizontalSlider(sliderRect, _angle, 0f, 360f);
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

        // Tints the slider region that maps to night (sampled from TimeOfDayService.IsNightAngle with the
        // authored arc bounds) behind the thumb, so the scrubber shows where night sits.
        private static void DrawNightBand(Rect sliderRect, float startAngle, float endAngle)
        {
            const int steps = 360;
            var previous = GUI.color;
            GUI.color = NightBandColor;

            var runStart = -1;
            for (var i = 0; i <= steps; i++)
            {
                var isNight = i < steps && TimeOfDayService.IsNightAngle(i, startAngle, endAngle);
                if (isNight && runStart < 0)
                {
                    runStart = i;
                }
                else if (!isNight && runStart >= 0)
                {
                    var x = sliderRect.x + (float)runStart / steps * sliderRect.width;
                    var width = (float)(i - runStart) / steps * sliderRect.width;
                    GUI.DrawTexture(new Rect(x, sliderRect.y, width, sliderRect.height), Texture2D.whiteTexture);
                    runStart = -1;
                }
            }

            GUI.color = previous;
        }
    }
}
#endif

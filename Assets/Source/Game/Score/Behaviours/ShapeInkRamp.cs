using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     The ink's look along its length as a strip of pixels the ink shader reads by the ink's age: red is the
    ///     width as a share of the widest, green the opacity, blue how far the colour has turned to the tail's.
    ///     The first pixel is at the pen, the last is the oldest ink.
    /// </summary>
    internal static class ShapeInkRamp
    {
        // Must match RAMP_SIZE in ShapeInk.shader.
        internal const int Size = 128;

        /// <summary>Fills <paramref name="pixels" /> (<see cref="Size" /> long) and returns the ink's widest width.</summary>
        internal static float Fill(in ShapeInkSettings settings, Color32[] pixels)
        {
            var widest = 0f;
            for (var i = 0; i < Size; i++)
            {
                widest = Mathf.Max(widest, settings.Width.Evaluate(Along(i)));
            }

            for (var i = 0; i < Size; i++)
            {
                var along = Along(i);
                var width = widest > 0f ? Mathf.Clamp01(settings.Width.Evaluate(along) / widest) : 0f;
                pixels[i] = new Color(
                    width,
                    Mathf.Clamp01(settings.Alpha.Evaluate(along)),
                    Mathf.Clamp01(settings.TailMix.Evaluate(along)),
                    1f);
            }

            return widest;
        }

        private static float Along(int pixel)
        {
            return pixel / (Size - 1f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Samples a colour ring for iridescent effects (rainbow lightning/laser/paint). <paramref name="t" />
    ///     in [0,1) walks the whole ring once, wrapping the last colour back to the first; drive it with
    ///     <c>Mathf.Repeat(progress * cycles, 1f)</c> to loop <c>cycles</c> times over an effect's duration.
    /// </summary>
    public static class ColorCycle
    {
        public static Color Sample(IReadOnlyList<Color> colors, float t)
        {
            if (colors == null || colors.Count == 0)
            {
                return Color.white;
            }

            if (colors.Count == 1)
            {
                return colors[0];
            }

            var pos = t * colors.Count;
            var index = Mathf.FloorToInt(pos) % colors.Count;
            return Color.Lerp(colors[index], colors[(index + 1) % colors.Count], pos - Mathf.Floor(pos));
        }
    }
}

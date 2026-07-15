using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Encapsulates the colour-cycling state shared by rainbow laser, lightning, and paint effects.
    ///     Store as a field; call <see cref="Set"/> to configure, <see cref="Sample"/> each frame, and
    ///     <see cref="Clear"/> on despawn.
    /// </summary>
    internal struct ColorCycleState
    {
        private IReadOnlyList<Color> _colors;
        private float _cycles;

        internal bool HasColors => _colors != null && _colors.Count > 0;

        internal void Set(IReadOnlyList<Color> colors, float cycles)
        {
            _colors = colors;
            _cycles = Mathf.Max(0f, cycles);
        }

        internal void Clear()
        {
            _colors = null;
            _cycles = 0f;
        }

        /// <summary>
        ///     Samples the colour ring at the given <paramref name="progress"/> (0–1 over the effect's
        ///     lifetime), cycling <see cref="_cycles"/> full loops. Returns <paramref name="fallback"/>
        ///     when no colours are set.
        /// </summary>
        internal Color Sample(float progress, Color fallback)
        {
            if (!HasColors)
            {
                return fallback;
            }

            return ColorCycle.Sample(_colors, Mathf.Repeat(progress * _cycles, 1f));
        }

        /// <summary>Overload without a fallback — returns <see cref="Color.white"/> when empty.</summary>
        internal Color Sample(float progress)
        {
            return Sample(progress, Color.white);
        }
    }
}

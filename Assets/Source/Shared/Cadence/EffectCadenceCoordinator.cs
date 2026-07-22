using System.Collections.Generic;
using System.Linq;
using VContainer.Unity;

namespace BalloonParty.Shared.Cadence
{
    /// <summary>
    ///     Assigns staggered phase offsets to all <see cref="ICadencedEffect"/> services at startup,
    ///     ensuring their periodic RT blits never cluster on the same frame. On tile-based mobile GPUs
    ///     (Adreno/Mali) each blit forces a tile flush to DRAM (~0.3 ms); spreading them reduces
    ///     worst-case per-frame cost from 10+ flushes to 2-3.
    /// </summary>
    /// <remarks>
    ///     The heaviest services (highest <see cref="ICadencedEffect.BlitWeight"/>) are placed at
    ///     maximum phase separation (0.0 and 0.5), lighter services fill the gaps evenly.
    /// </remarks>
    internal sealed class EffectCadenceCoordinator : IStartable
    {
        private readonly IReadOnlyList<ICadencedEffect> _effects;

        public EffectCadenceCoordinator(IEnumerable<ICadencedEffect> effects)
        {
            _effects = effects.ToList();
        }

        void IStartable.Start()
        {
            if (_effects.Count == 0)
            {
                return;
            }

            // Sort descending by weight so the two heaviest services get the widest separation.
            var sorted = _effects.OrderByDescending(e => e.BlitWeight).ToList();

            if (sorted.Count == 1)
            {
                sorted[0].ApplyPhaseOffset(0f);
                return;
            }

            // Heaviest pair at 0.0 and 0.5 (max separation in a cyclic [0,1) domain).
            sorted[0].ApplyPhaseOffset(0f);
            sorted[1].ApplyPhaseOffset(0.5f);

            // Remaining services fill gaps evenly between the two anchors.
            int remaining = sorted.Count - 2;

            for (int i = 0; i < remaining; i++)
            {
                // Distribute in the (0, 0.5) and (0.5, 1) intervals alternately.
                float offset = (i % 2 == 0)
                    ? 0.25f + i * 0.1f
                    : 0.75f + (i - 1) * 0.1f;

                // Wrap into [0, 1).
                offset -= (int)offset;
                sorted[i + 2].ApplyPhaseOffset(offset);
            }
        }
    }
}

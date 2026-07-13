using System.Collections.Generic;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    /// <summary>Shared colour resolution for score-scatter models (Tough/Cluster/rainbow-mode BalloonModel).</summary>
    internal readonly struct ColorSource
    {
        private readonly IGamePalette _palette;
        private readonly IReadOnlyList<string> _allowedColors;

        public ColorSource(IGamePalette palette, IReadOnlyList<string> allowedColors)
        {
            _palette = palette;
            _allowedColors = allowedColors;
        }

        /// <summary>Falls back to the full palette when constructed without a level context.</summary>
        public IReadOnlyList<string> Resolve()
        {
            return _allowedColors is { Count: > 0 } ? _allowedColors : _palette?.ProgressColorNames;
        }
    }
}

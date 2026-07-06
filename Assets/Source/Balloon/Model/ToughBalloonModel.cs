using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    internal class ToughBalloonModel : BalloonModelBase, IHasDurability, IHasScore, IHasScoreColor
    {
        private readonly IGamePalette _palette;
        private readonly IReadOnlyList<string> _allowedColors;
        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        protected override HitOutcome SurviveOutcome => HitOutcome.Deflect;

        internal ToughBalloonModel(
            BalloonModelConfig config, IGamePalette palette = null, IReadOnlyList<string> allowedColors = null)
            : base(config)
        {
            _palette = palette;
            _allowedColors = allowedColors;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            var colors = ResolveColorPool();
            if (colors == null || colors.Count == 0)
            {
                return;
            }

            for (var i = 0; i < ScoreValue; i++)
            {
                var colorId = colors[Random.Range(0, colors.Count)];
                results.Add(new ScoreAttribution(colorId, 1, true));
            }
        }

        // Falls back to the full palette when constructed without a level context.
        private IReadOnlyList<string> ResolveColorPool()
        {
            return _allowedColors is { Count: > 0 } ? _allowedColors : _palette?.ColorNames;
        }
    }
}

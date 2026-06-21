using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    internal class ToughBalloonModel : BalloonModelBase, IHasDurability, IHasScore, IHasScoreColor
    {
        private readonly IGamePalette _palette;
        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        // A tough balloon that survives a hit deflects the shot instead of letting it pass through.
        protected override HitOutcome SurviveOutcome => HitOutcome.Deflect;

        internal ToughBalloonModel(BalloonModelConfig config, IGamePalette palette = null) : base(config)
        {
            _palette = palette;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            var colors = _palette.Colors;
            if (colors == null || colors.Count == 0)
            {
                return;
            }

            for (var i = 0; i < ScoreValue; i++)
            {
                var colorId = colors[Random.Range(0, colors.Count)].Name;
                results.Add(new ScoreAttribution(colorId, 1, true));
            }
        }
    }
}

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
        private readonly GamePalette _palette;
        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal ToughBalloonModel(BalloonModelConfig config, GamePalette palette = null) : base(config)
        {
            _palette = palette;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            var colors = _palette.Colors;
            if (colors == null || colors.Length == 0)
            {
                return;
            }

            for (var i = 0; i < ScoreValue; i++)
            {
                var colorId = colors[Random.Range(0, colors.Length)].Name;
                results.Add(new ScoreAttribution(colorId, 1));
            }
        }

        protected override HitOutcome EvaluateNormalHit(DamageContext context)
        {
            var survives = HitsRemaining.Value - context.Damage > 0;
            HitsRemaining.Value -= context.Damage;
            return survives ? HitOutcome.Deflect : HitOutcome.Pop;
        }
    }
}

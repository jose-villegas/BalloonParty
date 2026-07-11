using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    internal class ToughBalloonModel : BalloonModelBase, IHasDurability, IHasScore, IHasScoreColor
    {
        private readonly ColorSource _colorSource;
        private readonly float _separationBias;

        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        protected override HitOutcome SurviveOutcome => HitOutcome.Deflect;

        internal ToughBalloonModel(
            BalloonModelConfig config, IGamePalette palette = null, IReadOnlyList<string> allowedColors = null)
            : base(config)
        {
            _colorSource = new ColorSource(palette, allowedColors);
            _separationBias = config.SeparationBias;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        // Keep apart from other toughs: candidates farther from the nearest one score higher, so between
        // two the bias weighs against the closest.
        public override int WeightBias(SlotGrid grid, Vector2Int candidate)
        {
            if (_separationBias <= 0f)
            {
                return 0;
            }

            var sqrDistance = this.NearestSameTypeSqrDistance(grid, candidate);
            return sqrDistance < float.MaxValue ? Mathf.RoundToInt(_separationBias * sqrDistance) : 0;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            var colors = _colorSource.Resolve();
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
    }
}

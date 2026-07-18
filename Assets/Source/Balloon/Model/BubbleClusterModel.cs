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
    /// <summary>At destruction, distributes one <see cref="ScoreAttribution"/> per remaining bubble to a random palette colour. Washes a projectile's stolen colour on contact.</summary>
    internal class BubbleClusterModel : BalloonModelBase, IHasDurability, IHasScoreColor, IWashesProjectileColor
    {
        private readonly ColorSource _colorSource;
        private readonly float _balanceBias;

        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
        public int ScoreValue { get; }

        public override PressureResponse PushResponse => PressureResponse.RelocateNearest;

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal BubbleClusterModel(
            BalloonModelConfig config, IGamePalette palette, IReadOnlyList<string> allowedColors = null)
            : base(config)
        {
            _colorSource = new ColorSource(palette, allowedColors);
            _balanceBias = config.BalanceBias;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        // Clumping: candidates nearer to a same-type sibling score higher (negative bias × distance²
        // means shorter distance = less negative = preferred). Over many balance rounds soap clusters
        // drift together from any direction.
        public override int WeightBias(SlotGrid grid, Vector2Int candidate)
        {
            if (_balanceBias == 0f)
            {
                return 0;
            }

            var sqrDistance = this.NearestSameTypeSqrDistance(grid, candidate);
            return sqrDistance < float.MaxValue ? Mathf.RoundToInt(-_balanceBias * sqrDistance) : 0;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            // Aggregated per colour (the attribution contract): one N-point entry per colour keeps the
            // pop as per-colour score GROUPS downstream, so a cluster pop can decompose into shapes
            // instead of fanning into per-point 1-point trails.
            ScoreAttributions.AddRandomPerColor(
                results, _colorSource.Resolve(), HitsRemaining.Value + 1, breaksStreak: true);
        }
    }
}

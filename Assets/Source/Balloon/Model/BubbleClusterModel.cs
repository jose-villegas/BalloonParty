using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    /// Model for <c>BalloonType.BubbleCluster</c>.
    /// Tracks only durability (bubble count) — the cluster carries no item and has no palette colour.
    /// At destruction, distributes one <see cref="ScoreAttribution"/> per remaining bubble,
    /// each going to a randomly chosen palette colour.
    /// </summary>
    internal class BubbleClusterModel : BalloonModelBase, IHasDurability, IHasScoreColor
    {
        private readonly IGamePalette _palette;

        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
        public int ScoreValue { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal BubbleClusterModel(BalloonModelConfig config, IGamePalette palette) : base(config)
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

            for (var i = 0; i < HitsRemaining.Value + 1; i++)
            {
                var colorId = colors[Random.Range(0, colors.Count)].Name;
                results.Add(new ScoreAttribution(colorId, 1, true));
            }
        }
    }
}

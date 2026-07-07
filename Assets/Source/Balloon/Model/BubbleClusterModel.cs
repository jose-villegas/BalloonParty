using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    /// <summary>At destruction, distributes one <see cref="ScoreAttribution"/> per remaining bubble to a random palette colour.</summary>
    internal class BubbleClusterModel : BalloonModelBase, IHasDurability, IHasScoreColor
    {
        private readonly ColorPool _colorPool;

        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
        public int ScoreValue { get; }

        public override PressureResponse PushResponse => PressureResponse.RelocateNearest;

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal BubbleClusterModel(
            BalloonModelConfig config, IGamePalette palette, IReadOnlyList<string> allowedColors = null)
            : base(config)
        {
            _colorPool = new ColorPool(palette, allowedColors);
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            var colors = _colorPool.Resolve();
            if (colors == null || colors.Count == 0)
            {
                return;
            }

            for (var i = 0; i < HitsRemaining.Value + 1; i++)
            {
                var colorId = colors[Random.Range(0, colors.Count)];
                results.Add(new ScoreAttribution(colorId, 1, true));
            }
        }
    }
}

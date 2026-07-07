using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    internal class BalloonModel : BalloonModelBase, IPaintable, IHasWriteableItemSlot, IHasWriteableRainbowMode,
        IHasDurability, IHasScore, IHasScoreColor
    {
        private readonly IGamePalette _palette;
        private readonly IReadOnlyList<string> _allowedColors;

        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);
        public ReactiveProperty<bool> IsRainbow { get; } = new(false);
        public int ScoreValue { get; }
        public float ItemActivationWeight { get; }
        public float Spillover { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<string> IHasColor.Color => Color;
        IReadOnlyReactiveProperty<ItemType> IHasItemSlot.Item => Item;
        IReadOnlyReactiveProperty<bool> IHasRainbowMode.IsRainbow => IsRainbow;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal BalloonModel() : this(new BalloonModelConfig()) { }

        internal BalloonModel(
            BalloonModelConfig config, IGamePalette palette = null, IReadOnlyList<string> allowedColors = null)
            : base(config)
        {
            _palette = palette;
            _allowedColors = allowedColors;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
            ItemActivationWeight = config.ItemActivationWeight;
            Spillover = config.Spillover;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            if (HitsRemaining.Value > 0)
            {
                return;
            }

            if (IsRainbow.Value)
            {
                ResolveRainbowAttribution(in context, results);
                return;
            }

            if (!string.IsNullOrEmpty(Color.Value))
            {
                results.Add(new ScoreAttribution(Color.Value, ScoreValue));
            }
        }

        // Full ScoreValue to the streak's current colour (falls back to this balloon's own colour as
        // primary if that colour isn't currently allowed); Spillover — a config tuning knob — to every
        // other allowed colour.
        private void ResolveRainbowAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            var colors = ResolveColorPool();
            if (colors == null || colors.Count == 0)
            {
                return;
            }

            var primaryColor = context.SourceColorId;
            var primaryIsAllowed = false;
            for (var i = 0; i < colors.Count; i++)
            {
                if (colors[i] == primaryColor)
                {
                    primaryIsAllowed = true;
                    break;
                }
            }

            if (!primaryIsAllowed)
            {
                primaryColor = colors[0];
            }

            results.Add(new ScoreAttribution(primaryColor, ScoreValue, breaksStreak: false, isPrimary: true));

            for (var i = 0; i < colors.Count; i++)
            {
                if (colors[i] == primaryColor)
                {
                    continue;
                }

                var spilloverPoints = Mathf.RoundToInt(ScoreValue * Spillover);
                if (spilloverPoints <= 0)
                {
                    continue;
                }

                results.Add(new ScoreAttribution(colors[i], spilloverPoints));
            }
        }

        // Falls back to the full palette when constructed without a level context (mirrors ToughBalloonModel).
        private IReadOnlyList<string> ResolveColorPool()
        {
            return _allowedColors is { Count: > 0 } ? _allowedColors : _palette?.ColorNames;
        }
    }
}

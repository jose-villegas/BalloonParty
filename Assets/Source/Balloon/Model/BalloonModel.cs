using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    internal class BalloonModel : BalloonModelBase, IPaintable, IHasWriteableItemSlot,
        IHasDurability, IHasScore, IHasScoreColor
    {
        private readonly ColorSource _colorSource;
        private readonly float _balanceBias;

        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);
        public int ScoreValue { get; }
        public float ItemActivationWeight { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<string> IHasColor.Color => Color;
        IReadOnlyReactiveProperty<ItemType> IHasItemSlot.Item => Item;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal BalloonModel() : this(new BalloonModelConfig(hitsToPop: 1)) { }

        internal BalloonModel(
            BalloonModelConfig config, IGamePalette palette = null, IReadOnlyList<string> allowedColors = null)
            : base(config)
        {
            _colorSource = new ColorSource(palette, allowedColors);
            _balanceBias = config.BalanceBias;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
            ItemActivationWeight = config.ItemActivationWeight;
        }

        // Prefer candidates with this color nearby off-row (hex radius 2, own row excluded) — over many
        // rebalances same-color balloons drift into diagonal lines.
        public override int WeightBias(SlotGrid grid, Vector2Int candidate)
        {
            if (_balanceBias <= 0f)
            {
                return 0;
            }

            return Mathf.RoundToInt(_balanceBias * this.CountSameColorDiagonals(grid, candidate));
        }

        // Single-colour attribution — ignores incompleteColors (only scatter pops avoid a completed bar;
        // a plain balloon still scores its own colour, banking as overflow if that colour's already full).
        public void ResolveScoreAttribution(
            in DamageContext context, IReadOnlyList<string> incompleteColors, IList<ScoreAttribution> results)
        {
            if (HitsRemaining.Value > 0)
            {
                return;
            }

            if (Color.Value == GamePalette.RainbowColorId)
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
        // primary if that colour isn't currently allowed). Every allowed colour is then scored its full
        // ScoreValue — a rainbow pop pays out to the whole active palette, not just the streak colour.
        private void ResolveRainbowAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            var colors = _colorSource.Resolve();
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

            // Primary anchors the streak (isPrimary); the rest each still score full ScoreValue.
            results.Add(new ScoreAttribution(primaryColor, ScoreValue, breaksStreak: false, isPrimary: true));

            for (var i = 0; i < colors.Count; i++)
            {
                if (colors[i] != primaryColor)
                {
                    results.Add(new ScoreAttribution(colors[i], ScoreValue));
                }
            }
        }
    }
}

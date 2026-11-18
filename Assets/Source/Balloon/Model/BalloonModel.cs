using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using UniRx;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    /// <summary>The plain balloon (and its silver, gold and rainbow skins): paintable, carries an item, scores its
    /// own colour. It can wear foam (<see cref="ICoatable" />): while coated, an ordinary hit takes a layer off and
    /// passes through, scoring nothing; a piercing or beam hit pops it, coat and all.</summary>
    internal class BalloonModel : BalloonModelBase, IPaintable, IHasWriteableItemSlot,
        IHasDurability, IHasScore, IHasScoreColor, ICoatable
    {
        private readonly ColorSource _colorSource;
        private readonly float _balanceBias;
        private readonly ReactiveProperty<int> _coat = new();

        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);
        public int ScoreValue { get; }
        public float ItemActivationWeight { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        // Prefer candidates with this color nearby off-row (hex radius 2, own row excluded) — over many
        // rebalances same-color balloons drift into diagonal lines.
        public override BalanceBiasKind BiasKind => BalanceBiasKind.ColorDiagonal;
        public override float BiasValue => _balanceBias;

        IReadOnlyReactiveProperty<string> IHasColor.Color => Color;
        IReadOnlyReactiveProperty<ItemType> IHasItemSlot.Item => Item;
        int IHasDurability.MaxHitPoints => MaxHitPoints;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        /// <summary>Layers of foam: each takes an ordinary hit of its own before the balloon can be hurt.</summary>
        public IReadOnlyReactiveProperty<int> Coat => _coat;

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

        // A coat comes off whole under a piercing or beam hit, which goes on to pop the balloon as it would have.
        // Any other hit that does damage only takes a layer off: HitsRemaining is untouched, so the surviving hit
        // scores nothing (ResolveScoreAttribution returns while HitsRemaining > 0).
        public override HitOutcome EvaluateHit(DamageContext context)
        {
            if (_coat.Value <= 0)
            {
                return base.EvaluateHit(context);
            }

            if ((context.Flags & (DamageFlags.Piercing | DamageFlags.Beam)) != 0)
            {
                _coat.Value = 0;
                return base.EvaluateHit(context);
            }

            if (context.Damage <= 0)
            {
                return SurviveOutcome;
            }

            _coat.Value--;
            return SurviveOutcome;
        }

        /// <summary>Adds a layer of foam unless the balloon already wears <paramref name="maxLayers" />; returns
        /// whether it did.</summary>
        internal bool TryCoat(int maxLayers)
        {
            if (_coat.Value >= maxLayers)
            {
                return false;
            }

            _coat.Value++;
            return true;
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

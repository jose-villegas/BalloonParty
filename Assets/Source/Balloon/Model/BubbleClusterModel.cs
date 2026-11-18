using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using UniRx;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    /// <summary>Each hit scores the bubbles the cluster held, spread over random palette colours, and breaks the
    /// streak. Washes a projectile's stolen colour on contact. At the merge cap <c>RipeBubbleService</c> ripens it:
    /// while <see cref="Ripe"/>, any hit bursts it whole.</summary>
    internal class BubbleClusterModel : BalloonModelBase, IHasDurability, IHasScore, IHasScoreColor,
        IWashesProjectileColor
    {
        private readonly ColorSource _colorSource;
        private readonly float _balanceBias;
        private readonly ReactiveProperty<bool> _ripe = new();
        private readonly ReactiveProperty<float> _fuseLeft = new();

        // The count before the latest hit: a hit that pops the cluster scores what it held, which the count
        // itself no longer shows.
        private int _heldAtHit;

        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
        public int ScoreValue { get; }

        /// <summary>At the merge cap with its fuse running: any hit bursts it whole.</summary>
        public IReadOnlyReactiveProperty<bool> Ripe => _ripe;

        /// <summary>Seconds of level clock until the fuse runs out; 0 when not ripe.</summary>
        public IReadOnlyReactiveProperty<float> FuseLeft => _fuseLeft;

        // Clumping: candidates nearer to a same-type sibling score higher (negative bias × distance²
        // means shorter distance = less negative = preferred). Over many balance rounds soap clusters
        // drift together from any direction.
        public override BalanceBiasKind BiasKind => BalanceBiasKind.Clump;
        public override float BiasValue => _balanceBias;

        public override PressureResponse PushResponse => PressureResponse.RelocateNearest;

        int IHasDurability.MaxHitPoints => MaxHitPoints;
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

        public override HitOutcome EvaluateHit(DamageContext context)
        {
            _heldAtHit = HitsRemaining.Value;
            var harms = context.Damage > 0 || (context.Flags & DamageFlags.Piercing) != 0;
            if (!_ripe.Value || !harms)
            {
                return base.EvaluateHit(context);
            }

            HitsRemaining.Value = 0;
            return HitOutcome.Pop;
        }

        public void ResolveScoreAttribution(
            in DamageContext context, IReadOnlyList<string> incompleteColors, IList<ScoreAttribution> results)
        {
            // Every hit scores the count it found, whatever its damage: a pop (the last bubble, a pierce, a ripe
            // burst) pays as much as chipping the cluster down would have. At least 1, for a hit dispatched without
            // being evaluated here (a cheat's), which leaves the count from an earlier hit or none.
            var points = System.Math.Max(1, _heldAtHit);

            // Aggregated per colour (the attribution contract): one N-point entry per colour keeps the
            // pop as per-colour score GROUPS downstream, so a cluster pop can decompose into shapes
            // instead of fanning into per-point 1-point trails. Confined to incompleteColors so a
            // completed bar never steals points that should land on one still in progress.
            ScoreAttributions.AddRandomPerColor(
                results, _colorSource.Resolve(), incompleteColors, points, breaksStreak: true);
        }

        /// <summary>Sets how many bubbles the cluster holds, at least 1: a water line's small cluster, or its count
        /// after a merge gave or took bubbles. Not capped by <c>MaxHitPoints</c>, the count it spawns with: a merge
        /// fills a cluster past that, up to its own cap.</summary>
        internal void SetBubbles(int count)
        {
            var bubbles = System.Math.Max(1, count);
            // Ripe means at the cap, so any drop leaves it below.
            if (bubbles < HitsRemaining.Value)
            {
                Unripen();
            }

            HitsRemaining.Value = bubbles;
        }

        internal void Ripen()
        {
            _ripe.Value = true;
        }

        internal void SetFuseLeft(float seconds)
        {
            _fuseLeft.Value = System.Math.Max(0f, seconds);
        }

        internal void Unripen()
        {
            _ripe.Value = false;
            _fuseLeft.Value = 0f;
        }
    }
}

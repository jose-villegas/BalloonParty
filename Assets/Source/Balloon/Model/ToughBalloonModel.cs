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
    internal class ToughBalloonModel : BalloonModelBase, IHasDeflectStamp, IHasDurability, IHasScore,
        IHasScoreColor, IResistsPaint
    {
        private readonly ColorSource _colorSource;
        private readonly float _balanceBias;

        public float DeflectStampScale { get; }
        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        // Candidates that extend the longest straight line of same-type neighbours along one of the
        // three hex axes score higher — steering tough balloons into walls rather than lumps.
        public override BalanceBiasKind BiasKind => BalanceBiasKind.Line;
        public override float BiasValue => _balanceBias;

        int IHasDurability.MaxHitPoints => MaxHitPoints;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        protected override HitOutcome SurviveOutcome => HitOutcome.Deflect;

        internal ToughBalloonModel(
            BalloonModelConfig config, IGamePalette palette = null, IReadOnlyList<string> allowedColors = null)
            : base(config)
        {
            _colorSource = new ColorSource(palette, allowedColors);
            _balanceBias = config.BalanceBias;
            DeflectStampScale = config.DeflectStampScale;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        public void ResolveScoreAttribution(
            in DamageContext context, IReadOnlyList<string> incompleteColors, IList<ScoreAttribution> results)
        {
            // Aggregated per colour (the attribution contract): one N-point entry per colour keeps the
            // pop as per-colour score GROUPS downstream, so a tough pop can decompose into shapes
            // instead of fanning into ScoreValue separate 1-point trails. Confined to incompleteColors so
            // a completed bar never steals points that should land on one still in progress.
            ScoreAttributions.AddRandomPerColor(
                results, _colorSource.Resolve(), incompleteColors, ScoreValue, breaksStreak: true);
        }
    }
}

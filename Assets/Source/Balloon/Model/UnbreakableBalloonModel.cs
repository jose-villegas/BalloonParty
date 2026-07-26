using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Nudge;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    internal class UnbreakableBalloonModel : BalloonModelBase, IHasDeflectStamp, IHasScore, IHasScoreColor,
        IPreBalanceRelocatable, IResistsPaint
    {
        private readonly ColorSource _colorSource;

        public float DeflectStampScale { get; }
        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        public override PressureResponse PushResponse => PressureResponse.RelocateFarthest;

        internal UnbreakableBalloonModel(
            BalloonModelConfig config, IGamePalette palette = null, IReadOnlyList<string> allowedColors = null)
            : base(config)
        {
            _colorSource = new ColorSource(palette, allowedColors);
            DeflectStampScale = config.DeflectStampScale;
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        public void ResolveScoreAttribution(
            in DamageContext context, IReadOnlyList<string> incompleteColors, IList<ScoreAttribution> results)
        {
            ScoreAttributions.AddRandomPerColor(
                results, _colorSource.Resolve(), incompleteColors, ScoreValue, breaksStreak: true);
        }

        public override HitOutcome EvaluateHit(DamageContext context)
        {
            // Deliberately does not mutate HitsRemaining — no durability state to track.
            if (context.Flags.HasFlag(DamageFlags.Piercing))
            {
                return HitOutcome.Pop;
            }

            return HitOutcome.Deflect;
        }

        // Roams toward the board's opposite side (one on the left wants the right, and vice versa),
        // preferring far slots — squared-distance weighted pick, so it stays unpredictable.
        public bool TryPickRelocation(SlotGrid grid, IReadOnlyList<Vector2Int> restingSlots, out Vector2Int target)
        {
            target = default;
            if (restingSlots.Count == 0)
            {
                return false;
            }

            var current = SlotIndex.Value;
            var mid = (grid.Columns - 1) * 0.5f;

            var totalWeight = 0f;
            for (var i = 0; i < restingSlots.Count; i++)
            {
                totalWeight += RelocationWeight(restingSlots[i], current, mid);
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            var roll = Random.Range(0f, totalWeight);
            var cumulative = 0f;
            for (var i = 0; i < restingSlots.Count; i++)
            {
                cumulative += RelocationWeight(restingSlots[i], current, mid);
                if (roll < cumulative)
                {
                    target = restingSlots[i];
                    return true;
                }
            }

            target = restingSlots[restingSlots.Count - 1];
            return true;
        }

        // Only slots past the board's middle (relative to where it sits) qualify; farther = heavier.
        private static float RelocationWeight(Vector2Int slot, Vector2Int current, float mid)
        {
            var oppositeSide = current.x < mid ? slot.x > mid : slot.x < mid;
            if (!oppositeSide || slot == current)
            {
                return 0f;
            }

            var delta = slot.x - current.x;
            return delta * delta;
        }
    }
}

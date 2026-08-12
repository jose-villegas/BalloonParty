using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Effects;
using BalloonParty.Item.Lightning;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Lightning's telegraph: the chain — an arc from the host to its nearest same-colour target, then
    ///     on from each target to the next.
    /// </summary>
    /// <remarks>
    ///     Targets and their order come from <c>LightningChain</c> itself, run over a live
    ///     <c>GridEffectBoard</c>, so the drawn chain visits exactly the balloons the effect would and in
    ///     the same nearest-first order. Positions are each occupant's <c>SlotPosition</c> (the lattice
    ///     home) because that is what the core sorts by — Lightning's selection is grid topology, not
    ///     physical overlap.
    ///     <para>
    ///         Resolved with <c>convertsToRainbow: false</c> even for a rainbow host. That flag decides
    ///         what a match SUFFERS (damage vs recolour), and a rainbow host's non-paintable match still
    ///         consumes a jump but emits no hit — reading the rainbow hit list would silently drop links
    ///         from the drawn path. False makes every match emit in jump order, which is the chain's shape.
    ///     </para>
    ///     <para>
    ///         Known gap: a rainbow host live matches the PROJECTILE's colour when it has a concrete one,
    ///         falling back to the nearest concrete colour on the board. The telegraph only does the
    ///         fallback, so a rainbow host under a coloured shot draws the wrong chain
    ///         (@ref plan_item_range_preview open questions).
    ///     </para>
    /// </remarks>
    internal sealed class LightningRangePreview : IItemRangePreview
    {
        // Bow of each arc as a fraction of the jump's own length, so long and short links read alike.
        private const float ArcBowFraction = 0.14f;
        private const int ArcSegments = 8;

        private readonly IGamePalette _palette;
        private readonly GridEffectBoard _board;
        private readonly List<EffectHit> _hits = new();

        public ItemType Type => ItemType.Lightning;

        internal LightningRangePreview(SlotGrid grid, IGamePalette palette)
        {
            _palette = palette;
            _board = new GridEffectBoard(grid);
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            // Excludes the host: its own chain never selects itself back, matching how the live board
            // rebuilds for an activation whose host has already popped.
            _board.Rebuild(context.Slot);

            var matchColorId = ResolveMatchColor(in context);
            if (string.IsNullOrEmpty(matchColorId))
            {
                return;
            }

            LightningChain.Resolve(
                _board, context.Origin, context.Slot, matchColorId, convertsToRainbow: false,
                GamePalette.RainbowColorId, _hits);

            var from = context.Origin;
            for (var i = 0; i < _hits.Count; i++)
            {
                var to = _board.Occupants[_hits[i].Handle].SlotPosition;
                AddArc(shape, from, to);
                from = to;
            }
        }

        private string ResolveMatchColor(in ItemPreviewContext context)
        {
            var hostColor = context.HostColorId;
            if (!_palette.IsRainbow(hostColor))
            {
                return hostColor;
            }

            return LightningChain.FindNearestConcreteColor(_board, context.Slot, GamePalette.RainbowColorId);
        }

        // A quadratic bend rather than a straight link, so a chain of jumps reads as lightning rather than
        // a polygon. Bowed to one side consistently (perpendicular to travel) so successive jumps fan the
        // same way instead of zig-zagging by accident.
        private static void AddArc(ItemPreviewShape shape, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var length = delta.magnitude;
            if (length < 1e-4f)
            {
                return;
            }

            var control = ((from + to) * 0.5f) + (Vector2.Perpendicular(delta / length) * (length * ArcBowFraction));

            shape.BeginStroke();
            for (var i = 0; i <= ArcSegments; i++)
            {
                var t = i / (float)ArcSegments;
                var inverse = 1f - t;
                var point = (inverse * inverse * from) + (2f * inverse * t * control) + (t * t * to);
                shape.AddPoint(point);
            }

            shape.EndStroke();
        }
    }
}

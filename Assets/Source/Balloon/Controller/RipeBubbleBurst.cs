using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     A ripe cluster's two endings. The ripple (<see cref="Ripple" />): one hit of
    ///     <see cref="IRipeBubbleSettings.RippleDamage" /> to each neighbouring balloon that takes it
    ///     (<see cref="RipeBubbleRule.RippleTakes" />), through <see cref="IHitDispatcher" /> as an item's hit goes, so
    ///     score, items and pops behave as for any other hit. The hits carry <see cref="DamageFlags.Ripple" /> and the
    ///     shot that burst the cluster, never <see cref="DamageFlags.DirectHit" />. The foam, when its fuse runs out
    ///     (<see cref="PlanFoam" />, then <see cref="HandOut" />): the cluster gives its bubbles to its neighbours
    ///     (<see cref="RipeBubbleRule.PlanHandout" />), coats on plain balloons and bubbles to clusters with room, pops
    ///     any left over and leaves the board, all without a hit. Either way a round press on the water. Only
    ///     <see cref="RipeBubbleService" /> calls it, from its own tick.
    /// </summary>
    internal sealed class RipeBubbleBurst
    {
        private readonly SlotGrid _grid;
        private readonly IHitDispatcher _hitDispatcher;
        private readonly IBalloonPopper _popper;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IRipeBubbleSettings _settings;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly Vector2Int[] _neighborSlots = new Vector2Int[6];
        private readonly IWriteableSlotActor[] _neighbors = new IWriteableSlotActor[6];
        private readonly int[] _coatRoom = new int[6];
        private readonly int[] _clusterRoom = new int[6];

        [Inject]
        internal RipeBubbleBurst(SlotGrid grid, IHitDispatcher hitDispatcher, IBalloonPopper popper,
            IBalloonsConfiguration balloonsConfig, DisturbanceFieldService disturbanceField)
        {
            _grid = grid;
            _hitDispatcher = hitDispatcher;
            _popper = popper;
            _balloonsConfig = balloonsConfig;
            _settings = balloonsConfig.RipeBubble;
            _disturbanceField = disturbanceField;
        }

        /// <summary>Hits the neighbours of <paramref name="slot" /> and returns the ones the hits reached (an outcome
        ///     other than <see cref="HitOutcome.None" />), a bit per direction as <see cref="RipeBubbleBurstMessage" />
        ///     carries them.</summary>
        internal int Ripple(Vector2Int slot, in DamageContext burstContext)
        {
            var context = new DamageContext(
                _settings.RippleDamage, DamageFlags.Ripple, string.Empty, burstContext.Projectile);
            var origin = _grid.IndexToWorldPosition(slot);
            CollectNeighbors(slot);

            var hits = 0;
            for (var i = 0; i < _neighbors.Length; i++)
            {
                var actor = _neighbors[i];
                var neighborSlot = _neighborSlots[i];
                // An earlier neighbour's hit can take this one off the board first (an item it carried going off).
                if (actor == null || !RipeBubbleRule.RippleTakes(actor)
                    || !ReferenceEquals(_grid.At(neighborSlot), actor))
                {
                    continue;
                }

                var position = _grid.IndexToWorldPosition(neighborSlot);
                var direction = (position - origin).normalized;
                var hit = ActorHitMessage.From((IHitable)actor, position, direction, context);
                _hitDispatcher.Dispatch(hit);
                if (hit.Outcome != HitOutcome.None)
                {
                    hits |= 1 << i;
                }
            }

            System.Array.Clear(_neighbors, 0, _neighbors.Length);
            _disturbanceField.Stamp(StampSource.BubbleRipple, origin, Vector2.zero);
            return hits;
        }

        /// <summary>
        ///     Where the <paramref name="bubbles" /> of the cluster at <paramref name="slot" /> go when its fuse runs out,
        ///     as <see cref="RipeBubbleBurstMessage.Handout" /> carries it: a coat for each neighbour that takes foam
        ///     (<see cref="RipeBubbleRule.FoamTakes" />), up to <see cref="IRipeBubbleSettings.CoatLayers" />, a bubble
        ///     for each neighbouring cluster with room below <see cref="IBalloonsConfiguration.BubbleMergeMax" />, and
        ///     leftovers for the rest. Changes nothing.
        /// </summary>
        internal int PlanFoam(Vector2Int slot, int bubbles)
        {
            var maxLayers = _settings.CoatLayers;
            var max = _balloonsConfig.BubbleMergeMax;
            CollectNeighbors(slot);
            for (var i = 0; i < _neighbors.Length; i++)
            {
                var actor = _neighbors[i];
                _coatRoom[i] = actor != null && RipeBubbleRule.FoamTakes(actor, maxLayers)
                    ? maxLayers - ((ICoatable)actor).Coat.Value
                    : 0;
                _clusterRoom[i] = actor != null ? RipeBubbleRule.ClusterRoom(actor, max) : 0;
            }

            System.Array.Clear(_neighbors, 0, _neighbors.Length);
            return RipeBubbleRule.PlanHandout(bubbles, _coatRoom, _clusterRoom);
        }

        /// <summary>
        ///     Carries out a <paramref name="handout" /> from <see cref="PlanFoam" />: each coat goes on, each
        ///     neighbouring cluster's count goes up, leftovers pop with no effect on anything, and the cluster leaves the
        ///     board as a merge's absorbed giver does, its view lingering while it plays the hand-out
        ///     (<see cref="RipeBubbleRule.FoamLeaveSeconds" />). State only: no hit is dispatched, nothing scores, so
        ///     nothing it does can reach back into <see cref="RipeBubbleService" />. Bubbles are only moved, never made.
        /// </summary>
        internal void HandOut(BubbleClusterModel cluster, int handout)
        {
            var slot = cluster.SlotIndex.Value;
            var maxLayers = _settings.CoatLayers;
            var max = _balloonsConfig.BubbleMergeMax;
            CollectNeighbors(slot);
            var bubbles = RipeBubbleBurstMessage.HandoutCount(handout);
            for (var bubble = 0; bubble < bubbles; bubble++)
            {
                var gift = RipeBubbleBurstMessage.HandoutGift(handout, bubble, out var direction);
                if (gift == FoamGift.Leftover)
                {
                    continue;
                }

                var actor = _neighbors[direction];
                if (actor == null || !ReferenceEquals(_grid.At(_neighborSlots[direction]), actor))
                {
                    continue;
                }

                if (gift == FoamGift.Coat && actor is BalloonModel balloon)
                {
                    balloon.TryCoat(maxLayers);
                }
                else if (gift == FoamGift.Cluster && actor is BubbleClusterModel neighbour
                         && neighbour.HitsRemaining.Value < max)
                {
                    neighbour.SetBubbles(neighbour.HitsRemaining.Value + 1);
                }
            }

            System.Array.Clear(_neighbors, 0, _neighbors.Length);
            var linger = RipeBubbleRule.FoamLeaveSeconds(bubbles, _settings.FoamStepSeconds);
            // A cluster with no controller (nothing spawned it: a test, a cheat) still leaves the grid.
            if (!_popper.TryLeave(cluster, linger) && ReferenceEquals(_grid.At(slot), cluster))
            {
                _grid.Remove(slot);
            }

            _disturbanceField.Stamp(StampSource.BubbleRipple, _grid.IndexToWorldPosition(slot), Vector2.zero);
        }

        // The six neighbours by direction, as the message's bits number them; an empty or off-board slot is null.
        // Taken before any hit lands, so a neighbour a hit moves or removes is checked against its own slot.
        private void CollectNeighbors(Vector2Int slot)
        {
            HexCoordinates.HexNeighborIndices(slot.x, slot.y, _neighborSlots);
            for (var i = 0; i < _neighborSlots.Length; i++)
            {
                var neighbor = _neighborSlots[i];
                _neighbors[i] = _grid.IsEmpty(neighbor.x, neighbor.y) ? null : _grid.At(neighbor);
            }
        }
    }
}

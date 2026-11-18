using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Nudge;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     A ripe cluster's two endings. The ripple (<see cref="Ripple" />) is a small bomb: one hit of
    ///     <see cref="IRipeBubbleSettings.RippleDamage" /> to each balloon that takes it
    ///     (<see cref="RipeBubbleRule.RippleTakes" />) on a slot within
    ///     <see cref="IRipeBubbleSettings.RippleRadius" /> of the burst's (<see cref="RipeBubbleRule.RippleReaches" />),
    ///     through <see cref="IHitDispatcher" /> as an item's hit goes, so score, items and pops behave as for any
    ///     other hit. The hits carry <see cref="DamageFlags.Ripple" /> and the shot that burst the cluster, never
    ///     <see cref="DamageFlags.DirectHit" />. With them go the pieces a bomb's blast has, each at its own smaller
    ///     scale: a shockwave nudge of the board, an inward pull then an outward shock on the disturbance field, and a
    ///     flash of light. The foam, when its fuse runs out (<see cref="PlanFoam" />, then <see cref="HandOut" />): the
    ///     cluster gives its bubbles to its six neighbours (<see cref="RipeBubbleRule.PlanHandout" />), coats on plain
    ///     balloons and bubbles to clusters with room, pops any left over and leaves the board, all without a hit,
    ///     with a round press on the water. Only <see cref="RipeBubbleService" /> calls it, from its own tick.
    /// </summary>
    internal sealed class RipeBubbleBurst
    {
        // Room for every slot a blast can reach without growing: two rings of a hex board are 18.
        private const int BlastCapacity = 32;

        private readonly SlotGrid _grid;
        private readonly IHitDispatcher _hitDispatcher;
        private readonly IBalloonPopper _popper;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IRipeBubbleSettings _settings;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly SceneLightFieldService _lightField;
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly Vector2Int[] _neighborSlots = new Vector2Int[6];
        private readonly IWriteableSlotActor[] _neighbors = new IWriteableSlotActor[6];
        private readonly int[] _coatRoom = new int[6];
        private readonly int[] _clusterRoom = new int[6];
        private readonly List<Vector2Int> _blastSlots = new(BlastCapacity);
        private readonly List<IWriteableSlotActor> _blastActors = new(BlastCapacity);

        [Inject]
        internal RipeBubbleBurst(SlotGrid grid, IHitDispatcher hitDispatcher, IBalloonPopper popper,
            IBalloonsConfiguration balloonsConfig, DisturbanceFieldService disturbanceField,
            SceneLightFieldService lightField, IPublisher<NudgeMessage> nudgePublisher)
        {
            _grid = grid;
            _hitDispatcher = hitDispatcher;
            _popper = popper;
            _balloonsConfig = balloonsConfig;
            _settings = balloonsConfig.RipeBubble;
            _disturbanceField = disturbanceField;
            _lightField = lightField;
            _nudgePublisher = nudgePublisher;
        }

        /// <summary>
        ///     Blasts round <paramref name="slot" />: hits every balloon within the ripple's radius that takes it, in
        ///     column then row order, and returns how many hits landed (an outcome other than
        ///     <see cref="HitOutcome.None" />), as <see cref="RipeBubbleBurstMessage.Reached" /> carries it. The
        ///     bursting cluster has left its slot by now; a balloon the board has since moved there is in the blast
        ///     like any other.
        /// </summary>
        internal int Ripple(Vector2Int slot, in DamageContext burstContext)
        {
            var context = new DamageContext(
                _settings.RippleDamage, DamageFlags.Ripple, string.Empty, burstContext.Projectile);
            var origin = _grid.IndexToWorldPosition(slot);

            // As a bomb does: the shockwave goes out before the hits' own neighbour nudges.
            _nudgePublisher.Publish(new NudgeMessage(
                null, origin, NudgeType.Shockwave, _settings.BlastNudgeOverrides));

            CollectBlast(origin, _settings.RippleRadius);
            var hits = 0;
            for (var i = 0; i < _blastActors.Count; i++)
            {
                var actor = _blastActors[i];
                var actorSlot = _blastSlots[i];
                // An earlier hit can take this one off the board first (an item it carried going off).
                if (!RipeBubbleRule.RippleTakes(actor) || !ReferenceEquals(_grid.At(actorSlot), actor))
                {
                    continue;
                }

                var position = _grid.IndexToWorldPosition(actorSlot);
                var direction = (position - origin).normalized;
                var hit = ActorHitMessage.From((IHitable)actor, position, direction, context);
                _hitDispatcher.Dispatch(hit);
                if (hit.Outcome != HitOutcome.None)
                {
                    hits++;
                }
            }

            _blastActors.Clear();
            _blastSlots.Clear();
            ShockTheField(origin);
            _lightField.Flash(
                origin, _settings.BlastLightRadius, _settings.BlastLightIntensity, _settings.BlastLightSeconds);
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

            _disturbanceField.Stamp(StampSource.BubbleFoam, _grid.IndexToWorldPosition(slot), Vector2.zero);
        }

        // Every occupied slot the blast reaches and who is in it, column by column. Taken before any hit lands, so
        // an actor a hit moves or removes is checked against its own slot, and the hits land in an order the shot
        // solver can repeat.
        private void CollectBlast(Vector3 origin, float radius)
        {
            _blastSlots.Clear();
            _blastActors.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (_grid.IsEmpty(col, row)
                        || !RipeBubbleRule.RippleReaches(origin, _grid.IndexToWorldPosition(slot), radius))
                    {
                        continue;
                    }

                    _blastSlots.Add(slot);
                    _blastActors.Add(_grid.At(slot));
                }
            }
        }

        // The bomb's pair at the ripe burst's own scale: a brief pull inward from wider than the blast, so the field
        // gathers, then the BubbleRipple profile's ramped shock throws it out.
        private void ShockTheField(Vector3 origin)
        {
            var pull = _settings.BlastImplosionStrengthScale;
            if (pull > 0f)
            {
                var profile = _disturbanceField.GetProfile(StampSource.BubbleRipple);
                _disturbanceField.Stamp(
                    origin,
                    profile.Radius * _settings.BlastImplosionRadiusScale,
                    -Mathf.Abs(profile.Strength) * pull,
                    Vector2.zero,
                    _settings.BlastImplosionSeconds);
            }

            _disturbanceField.Stamp(StampSource.BubbleRipple, origin, Vector2.zero);
        }

        // The six neighbours by direction, as the foam's message numbers them; an empty or off-board slot is null.
        // Taken before the hand-out changes anything, so a neighbour it moves is checked against its own slot.
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

using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Merges bubble clusters that sit in neighbouring slots (<see cref="BubbleMergeRule" />): the smaller gives
    ///     the larger as many bubbles as it has room for, up to a full cluster, and is absorbed once it has given
    ///     them all. How full a cluster may get comes from <see cref="RipeRoom" />: the merge cap while the board has
    ///     room for another ripe cluster, one bubble short of it otherwise, counted through the pass so that two
    ///     merges of one pass cannot both fill a cluster past that room. Merging keeps the board's rhythm
    ///     (<see cref="IBoardBeat" />): a pass runs only on the bubbles' off-beat,
    ///     <see cref="IRipeBubbleSettings.MergeOffBeat" /> of a beat after each beat, and at most once on each.
    ///     Anything that could open room only marks that there is something to look at on the next one: the grid
    ///     changing (a spawn, a pop, the board settling, a ripe cluster leaving once it has handed its bubbles out),
    ///     a cluster being hit, a merge of the last pass, or a merge the room for full clusters held back. With
    ///     nothing marked an off-beat scans nothing. Each cluster merges at most once a pass, so a group gathers a
    ///     step a beat. Each merge publishes <see cref="BubbleMergedMessage" /> just before the counts change, which
    ///     <c>BubbleMergeView</c> plays as bubbles hopping across.
    /// </summary>
    internal sealed class BubbleMergeService : IStartable, ITickable, IDisposable
    {
        private readonly SlotGrid _grid;
        private readonly BalloonControllerRegistry _registry;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IRipeBubbleSettings _settings;
        private readonly RipeRoom _room;
        private readonly IBoardBeat _beat;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly IPublisher<BubbleMergedMessage> _mergedPublisher;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<IWriteableSlotActor> _neighbors = new();
        private readonly List<BubbleMergeCandidate> _candidates = new();
        private readonly List<BubbleMergeCandidate> _chosen = new();
        private readonly HashSet<Vector2Int> _used = new();

        // The beat's off-beat count as last read: a pass runs only on a tick that finds it risen.
        private int _seenOffBeats;

        // What there is to look at on the next off-beat. The grid's MutationVersion as of the last pass: an
        // unchanged grid has nothing new to merge. And whatever changes a count or opens room without moving
        // anything: a hit on a cluster, a merge of the last pass, or a merge the room for full clusters held back
        // (that room opens with a ripe cluster's end and the gap after it, neither of which the grid shows).
        private int _seenVersion = -1;
        private bool _pending;

        // The pass under way: the board's full clusters plus those this pass has filled, and whether the room for
        // full clusters kept any pair from merging as far as the merge cap alone would let it.
        private int _fullClusters;
        private bool _heldBack;

        [Inject]
        internal BubbleMergeService(SlotGrid grid, BalloonControllerRegistry registry,
            IBalloonsConfiguration balloonsConfig, RipeRoom room, IBoardBeat beat,
            ISubscriber<ActorHitMessage> hitSubscriber, IPublisher<BubbleMergedMessage> mergedPublisher)
        {
            _grid = grid;
            _registry = registry;
            _balloonsConfig = balloonsConfig;
            _settings = balloonsConfig.RipeBubble;
            _room = room;
            _beat = beat;
            _hitSubscriber = hitSubscriber;
            _mergedPublisher = mergedPublisher;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);
        }

        public void Tick()
        {
            // The count is kept even when it has fallen: a new run starts it over, and that is no off-beat.
            var offBeats = _beat.OffBeats(_settings.MergeOffBeat);
            var onOffBeat = offBeats > _seenOffBeats;
            _seenOffBeats = offBeats;
            if (!onOffBeat || (_grid.MutationVersion == _seenVersion && !_pending))
            {
                return;
            }

            _seenVersion = _grid.MutationVersion;
            _pending = false;
            _heldBack = false;
            _fullClusters = _room.CountFull();
            var max = _balloonsConfig.BubbleMergeMax;
            var limit = _room.FillLimit(_fullClusters);
            _candidates.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    CollectCandidates(new Vector2Int(col, row), max, limit);
                }
            }

            if (_candidates.Count > 0)
            {
                BubbleMergeRule.Choose(_candidates, _chosen, _used);
                foreach (var merge in _chosen)
                {
                    // A merge can open room for another (a giver now beside a smaller neighbour): the next
                    // off-beat looks again, so a cluster takes part in one merge a beat.
                    _pending |= Merge(merge, max);
                }
            }

            _pending |= _heldBack;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnActorHit(ActorHitMessage message)
        {
            if (message.Actor is BubbleClusterModel)
            {
                _pending = true;
            }
        }

        // Each pair is found from both sides; only the side that would give adds it. A pair fills its receiver to
        // limit, what the board's room for full clusters allows as the pass starts: max, or a bubble short of it.
        private void CollectCandidates(Vector2Int slot, int max, int limit)
        {
            if (_grid.At(slot) is not BubbleClusterModel bubble)
            {
                return;
            }

            _grid.GetNeighbors(slot.x, slot.y, _neighbors);
            foreach (var neighbor in _neighbors)
            {
                if (neighbor is not BubbleClusterModel other)
                {
                    continue;
                }

                var otherSlot = other.SlotIndex.Value;
                var bubbles = bubble.HitsRemaining.Value;
                var otherBubbles = other.HitsRemaining.Value;
                // The configured cap, not the receiver's MaxHitPoints: clusters spawn with fewer hits than a merge
                // fills them to, and that limit would leave a spawned cluster full from the start.
                if (!BubbleMergeRule.IsGiver(slot, bubbles, otherSlot, otherBubbles))
                {
                    continue;
                }

                var given = BubbleMergeRule.Given(bubbles, otherBubbles, limit);
                if (given > 0)
                {
                    _candidates.Add(new BubbleMergeCandidate(slot, otherSlot, otherBubbles + given));
                }

                if (limit < max && BubbleMergeRule.Given(bubbles, otherBubbles, max) > given)
                {
                    _heldBack = true;
                }
            }
        }

        // False when nothing was given: the room for full clusters went to an earlier merge, or the giver would be
        // absorbed and has no controller to slide it away.
        private bool Merge(BubbleMergeCandidate merge, int max)
        {
            if (_grid.At(merge.Giver) is not BubbleClusterModel giver
                || _grid.At(merge.Receiver) is not BubbleClusterModel receiver)
            {
                return false;
            }

            // An earlier merge of this pass may have filled the last cluster the board has room for: this one then
            // stops a bubble short, or gives nothing.
            var bubbles = Math.Min(merge.Bubbles, _room.FillLimit(_fullClusters));
            if (bubbles < merge.Bubbles)
            {
                _heldBack = true;
            }

            var given = bubbles - receiver.HitsRemaining.Value;
            if (given <= 0)
            {
                return false;
            }

            var left = giver.HitsRemaining.Value - given;
            if (left > 0)
            {
                Publish(merge, given, absorbed: false);
                giver.SetBubbles(left);
                receiver.SetBubbles(bubbles);
            }
            else if (_registry.TryResolve(giver, out var controller))
            {
                Publish(merge, given, absorbed: true);
                receiver.SetBubbles(bubbles);
                controller.AbsorbInto(_grid.IndexToWorldPosition(merge.Receiver), _balloonsConfig.BubbleMergeSeconds);
            }
            else
            {
                return false;
            }

            if (RipeBubbleRule.IsRipe(bubbles, max))
            {
                _fullClusters++;
            }

            return true;
        }

        private void Publish(BubbleMergeCandidate merge, int given, bool absorbed)
        {
            _mergedPublisher.Publish(new BubbleMergedMessage(
                merge.Giver, merge.Receiver, _grid.IndexToWorldPosition(merge.Giver),
                _grid.IndexToWorldPosition(merge.Receiver), given, absorbed));
        }
    }
}

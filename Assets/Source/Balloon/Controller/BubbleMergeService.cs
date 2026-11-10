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
    ///     them all. Looks again the frame after anything could open room: the grid changing (a spawn, a pop, the
    ///     board settling), a cluster being hit, or a merge of its own. Each cluster merges at most once a pass, so
    ///     a group gathers a step a frame.
    /// </summary>
    internal sealed class BubbleMergeService : IStartable, ITickable, IDisposable
    {
        private readonly SlotGrid _grid;
        private readonly BalloonControllerRegistry _registry;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<IWriteableSlotActor> _neighbors = new();
        private readonly List<BubbleMergeCandidate> _candidates = new();
        private readonly List<BubbleMergeCandidate> _chosen = new();
        private readonly HashSet<Vector2Int> _used = new();

        // The grid's MutationVersion last looked at: an unchanged grid has nothing new to merge, unless a hit or a
        // merge that left its giver in place changed a count without moving anything.
        private int _seenVersion = -1;
        private bool _countsChanged;

        [Inject]
        internal BubbleMergeService(SlotGrid grid, BalloonControllerRegistry registry,
            IBalloonsConfiguration balloonsConfig, ISubscriber<ActorHitMessage> hitSubscriber)
        {
            _grid = grid;
            _registry = registry;
            _balloonsConfig = balloonsConfig;
            _hitSubscriber = hitSubscriber;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);
        }

        public void Tick()
        {
            if (_grid.MutationVersion == _seenVersion && !_countsChanged)
            {
                return;
            }

            _seenVersion = _grid.MutationVersion;
            _countsChanged = false;
            _candidates.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    CollectCandidates(new Vector2Int(col, row));
                }
            }

            if (_candidates.Count == 0)
            {
                return;
            }

            BubbleMergeRule.Choose(_candidates, _chosen, _used);
            foreach (var merge in _chosen)
            {
                Merge(merge);
            }

            // A merge can open room for another (a giver now beside a smaller neighbour): look again next frame.
            _countsChanged = true;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnActorHit(ActorHitMessage message)
        {
            if (message.Actor is BubbleClusterModel)
            {
                _countsChanged = true;
            }
        }

        // Each pair is found from both sides; only the side that would give adds it.
        private void CollectCandidates(Vector2Int slot)
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
                var given = BubbleMergeRule.Given(bubbles, otherBubbles, _balloonsConfig.BubbleMergeMax);
                if (given > 0 && BubbleMergeRule.IsGiver(slot, bubbles, otherSlot, otherBubbles))
                {
                    _candidates.Add(new BubbleMergeCandidate(slot, otherSlot, otherBubbles + given));
                }
            }
        }

        private void Merge(BubbleMergeCandidate merge)
        {
            if (_grid.At(merge.Giver) is not BubbleClusterModel giver
                || _grid.At(merge.Receiver) is not BubbleClusterModel receiver)
            {
                return;
            }

            var given = merge.Bubbles - receiver.HitsRemaining.Value;
            var left = giver.HitsRemaining.Value - given;
            if (left > 0)
            {
                giver.SetBubbles(left);
                receiver.SetBubbles(merge.Bubbles);
                return;
            }

            if (_registry.TryResolve(giver, out var controller))
            {
                receiver.SetBubbles(merge.Bubbles);
                controller.AbsorbInto(_grid.IndexToWorldPosition(merge.Receiver), _balloonsConfig.BubbleMergeSeconds);
            }
        }
    }
}

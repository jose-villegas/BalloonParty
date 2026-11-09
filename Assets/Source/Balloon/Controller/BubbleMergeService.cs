using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Nudge;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Merges neighbouring bubble clusters a nudge pushes together (<see cref="BubbleMergeRule" />). After every
    ///     nudge (<see cref="NudgeAppliedMessage" />), each pushed cluster is measured against its neighbouring
    ///     clusters at the push's peak; a pair that ends up closer, whose bubbles fit in one cluster, merges: the one
    ///     pushed toward the other flows into it, and the survivor holds both counts. Each cluster merges at most
    ///     once per nudge.
    /// </summary>
    internal sealed class BubbleMergeService : IStartable, IDisposable
    {
        private readonly SlotGrid _grid;
        private readonly BalloonControllerRegistry _registry;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly ISubscriber<NudgeAppliedMessage> _appliedSubscriber;
        private readonly Dictionary<Vector2Int, Vector2> _pushes = new();
        private readonly List<IWriteableSlotActor> _neighbors = new();
        private readonly List<BubbleMergeCandidate> _candidates = new();
        private readonly List<BubbleMergeCandidate> _chosen = new();
        private readonly HashSet<Vector2Int> _used = new();

        private IDisposable _subscription;

        [Inject]
        internal BubbleMergeService(
            SlotGrid grid, BalloonControllerRegistry registry, IBalloonsConfiguration balloonsConfig,
            ISubscriber<NudgeAppliedMessage> appliedSubscriber)
        {
            _grid = grid;
            _registry = registry;
            _balloonsConfig = balloonsConfig;
            _appliedSubscriber = appliedSubscriber;
        }

        public void Start()
        {
            _subscription = _appliedSubscriber.Subscribe(OnNudgeApplied);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void OnNudgeApplied(NudgeAppliedMessage msg)
        {
            _pushes.Clear();
            var anyBubble = false;
            foreach (var nudged in msg.Actors)
            {
                _pushes[nudged.Slot] = nudged.Displacement;
                anyBubble |= _grid.At(nudged.Slot) is BubbleClusterModel;
            }

            if (!anyBubble)
            {
                return;
            }

            _candidates.Clear();
            foreach (var nudged in msg.Actors)
            {
                CollectCandidates(nudged.Slot, nudged.Displacement);
            }

            BubbleMergeRule.Choose(_candidates, _chosen, _used);
            foreach (var merge in _chosen)
            {
                Merge(merge);
            }
        }

        private void CollectCandidates(Vector2Int slot, Vector2 push)
        {
            if (_grid.At(slot) is not BubbleClusterModel bubble)
            {
                return;
            }

            Vector2 here = _grid.IndexToWorldPosition(slot);
            _grid.GetNeighbors(slot.x, slot.y, _neighbors);
            foreach (var neighbor in _neighbors)
            {
                if (neighbor is not BubbleClusterModel other)
                {
                    continue;
                }

                var otherSlot = other.SlotIndex.Value;
                Vector2 there = _grid.IndexToWorldPosition(otherSlot);
                _pushes.TryGetValue(otherSlot, out var otherPush);
                var max = Mathf.Max(bubble.MaxHitPoints, other.MaxHitPoints);
                if (!BubbleMergeRule.Merges(here, push, there, otherPush, bubble.HitsRemaining.Value,
                        other.HitsRemaining.Value, max)
                    || !BubbleMergeRule.IsAbsorbed(slot, here, push, otherSlot, there, otherPush))
                {
                    continue;
                }

                _candidates.Add(new BubbleMergeCandidate(
                    slot, otherSlot, BubbleMergeRule.Approach(here, push, there, otherPush)));
            }
        }

        private void Merge(BubbleMergeCandidate merge)
        {
            if (_grid.At(merge.Absorbed) is not BubbleClusterModel absorbed
                || _grid.At(merge.Survivor) is not BubbleClusterModel survivor
                || !_registry.TryResolve(absorbed, out var controller))
            {
                return;
            }

            survivor.SetBubbles(survivor.HitsRemaining.Value + absorbed.HitsRemaining.Value);
            controller.AbsorbInto(_grid.IndexToWorldPosition(merge.Survivor), _balloonsConfig.BubbleMergeSeconds);
        }
    }
}

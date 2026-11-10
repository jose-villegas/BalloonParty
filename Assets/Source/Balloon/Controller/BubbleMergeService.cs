using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Merges bubble clusters that sit in neighbouring slots (<see cref="BubbleMergeRule" />). Whenever the grid
    ///     has changed (a spawn, a pop, the board settling), the next frame looks at every pair of neighbouring
    ///     clusters whose bubbles fit in one: the smaller flows into the larger, which holds both counts. Each
    ///     cluster merges at most once a pass; the absorb itself changes the grid, so a cluster that grew can merge
    ///     again on the next frame.
    /// </summary>
    internal sealed class BubbleMergeService : ITickable
    {
        private readonly SlotGrid _grid;
        private readonly BalloonControllerRegistry _registry;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly List<IWriteableSlotActor> _neighbors = new();
        private readonly List<BubbleMergeCandidate> _candidates = new();
        private readonly List<BubbleMergeCandidate> _chosen = new();
        private readonly HashSet<Vector2Int> _used = new();

        // The grid's MutationVersion last looked at: an unchanged grid has nothing new to merge.
        private int _seenVersion = -1;

        [Inject]
        internal BubbleMergeService(
            SlotGrid grid, BalloonControllerRegistry registry, IBalloonsConfiguration balloonsConfig)
        {
            _grid = grid;
            _registry = registry;
            _balloonsConfig = balloonsConfig;
        }

        public void Tick()
        {
            if (_grid.MutationVersion == _seenVersion)
            {
                return;
            }

            _seenVersion = _grid.MutationVersion;
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
        }

        // Each pair is found from both sides; only the side that would be absorbed adds it.
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
                var max = Mathf.Max(bubble.MaxHitPoints, other.MaxHitPoints);
                if (BubbleMergeRule.Merges(bubbles, otherBubbles, max)
                    && BubbleMergeRule.IsAbsorbed(slot, bubbles, otherSlot, otherBubbles))
                {
                    _candidates.Add(new BubbleMergeCandidate(slot, otherSlot, bubbles + otherBubbles));
                }
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

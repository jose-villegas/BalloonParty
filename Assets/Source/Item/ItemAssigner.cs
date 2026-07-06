using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item
{
    internal class ItemAssigner : IStartable
    {
        private readonly ISubscriber<ItemCheckMessage> _checkSubscriber;
        private readonly IActiveLevelParameters _levelParams;
        private readonly SlotGrid _grid;

        private readonly List<IHasWriteableItemSlot> _eligibleBuffer = new();
        private readonly Dictionary<string, int> _activeCountsBuffer = new();

        [Inject]
        internal ItemAssigner(
            IActiveLevelParameters levelParams,
            SlotGrid grid,
            ISubscriber<ItemCheckMessage> checkSubscriber)
        {
            _levelParams = levelParams;
            _grid = grid;
            _checkSubscriber = checkSubscriber;
        }

        public void Start()
        {
            _checkSubscriber.Subscribe(OnItemCheck);
        }

        private void OnItemCheck(ItemCheckMessage msg)
        {
            if (msg.NewBalloons == null || msg.NewBalloons.Count == 0)
            {
                return;
            }

            // Initial board fills skip the cadence gate; later spawns roll only on a cadence turn.
            AnimationCurve weights;
            if (msg.IsInitialSpawn)
            {
                weights = _levelParams.Current.InitialItemCountWeights;
            }
            else if (IsCadenceTurn(msg.TurnCount))
            {
                weights = _levelParams.Current.ItemCountWeights;
            }
            else
            {
                return;
            }

            var count = SampleCount(weights, Random.value);
            if (count <= 0)
            {
                return;
            }

            AssignItems(msg.NewBalloons, count);
        }

        // Tracks running per-type counts so caps hold within the batch, not just the pre-existing board.
        private void AssignItems(IReadOnlyList<IBalloonModel> newBalloons, int count)
        {
            var candidates = _levelParams.Current.Items;
            if (candidates.Count == 0)
            {
                return;
            }

            CollectEligibleSlots(newBalloons);
            if (_eligibleBuffer.Count == 0)
            {
                return;
            }

            _activeCountsBuffer.Clear();
            foreach (var c in candidates)
            {
                _activeCountsBuffer[c.Type.ToString()] = CountBalloonsWithItem(c.Type);
            }

            var grants = Mathf.Min(count, _eligibleBuffer.Count);
            for (var n = 0; n < grants; n++)
            {
                var picked = _levelParams.Current.PickItemEntry(_activeCountsBuffer);
                if (picked == null || picked.Type == ItemType.None)
                {
                    // Every candidate is at its cap.
                    break;
                }

                var indexOf = Random.Range(0, _eligibleBuffer.Count);
                _eligibleBuffer[indexOf].Item.Value = picked.Type;
                _eligibleBuffer.RemoveAt(indexOf);

                var key = picked.Type.ToString();
                _activeCountsBuffer[key] = _activeCountsBuffer[key] + 1;
            }
        }

        // Weighted-random draw from a curve keyed by integer count; empty/all-zero weights return 0.
        internal static int SampleCount(AnimationCurve weights, float roll01)
        {
            if (weights == null || weights.length == 0)
            {
                return 0;
            }

            var maxCount = Mathf.Max(0, Mathf.RoundToInt(weights[weights.length - 1].time));

            var total = 0f;
            for (var i = 0; i <= maxCount; i++)
            {
                total += Mathf.Max(0f, weights.Evaluate(i));
            }

            if (total <= 0f)
            {
                return 0;
            }

            var target = Mathf.Clamp01(roll01) * total;
            var accumulated = 0f;
            for (var i = 0; i <= maxCount; i++)
            {
                accumulated += Mathf.Max(0f, weights.Evaluate(i));
                if (target < accumulated)
                {
                    return i;
                }
            }

            return maxCount;
        }

        private bool IsCadenceTurn(int turns)
        {
            var cadence = _levelParams.Current.ItemCadence;
            return cadence > 0 && turns % cadence == 0;
        }

        private void CollectEligibleSlots(IReadOnlyList<IBalloonModel> newBalloons)
        {
            _eligibleBuffer.Clear();
            for (var i = 0; i < newBalloons.Count; i++)
            {
                if (newBalloons[i] is IHasWriteableItemSlot slot && slot.Item.Value == ItemType.None)
                {
                    _eligibleBuffer.Add(slot);
                }
            }
        }

        // Counts via the capability interface, not a concrete model type, or caps silently break.
        private int CountBalloonsWithItem(ItemType type)
        {
            var count = 0;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    if (_grid.At(new Vector2Int(col, row)) is IHasItemSlot host && host.Item.Value == type)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}

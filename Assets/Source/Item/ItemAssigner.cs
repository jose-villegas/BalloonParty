using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item
{
    internal class ItemAssigner : IStartable
    {
        private readonly ISubscriber<ItemCheckMessage> _checkSubscriber;
        private readonly IItemConfiguration _itemConfig;
        private readonly SlotGrid _grid;

        private readonly List<ItemSettings> _candidateBuffer = new();
        private readonly List<IHasWriteableItemSlot> _eligibleBuffer = new();
        private readonly Dictionary<string, int> _activeCountsBuffer = new();

        [Inject]
        internal ItemAssigner(
            IItemConfiguration itemConfig,
            SlotGrid grid,
            ISubscriber<ItemCheckMessage> checkSubscriber)
        {
            _itemConfig = itemConfig;
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

            CollectCandidates(msg.TurnCount);
            if (_candidateBuffer.Count == 0)
            {
                return;
            }

            _activeCountsBuffer.Clear();
            foreach (var c in _candidateBuffer)
            {
                _activeCountsBuffer[c.Type.ToString()] = CountBalloonsWithItem(c.Type);
            }

            var picked = _candidateBuffer.PickRandom(_activeCountsBuffer);
            if (picked == null || picked.Type == ItemType.None)
            {
                return;
            }

            CollectEligibleSlots(msg.NewBalloons);
            if (_eligibleBuffer.Count == 0)
            {
                return;
            }

            var indexOf = Random.Range(0, _eligibleBuffer.Count);
            _eligibleBuffer[indexOf].Item.Value = picked.Type;
        }

        // Items whose turn-check interval lands on this turn become drop candidates.
        private void CollectCandidates(int turns)
        {
            _candidateBuffer.Clear();
            var items = _itemConfig.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.TurnCheckEvery > 0 && turns % item.TurnCheckEvery == 0)
                {
                    _candidateBuffer.Add(item);
                }
            }
        }

        // Newly-spawned balloons that can actually carry an item.
        private void CollectEligibleSlots(IReadOnlyList<IBalloonModel> newBalloons)
        {
            _eligibleBuffer.Clear();
            for (var i = 0; i < newBalloons.Count; i++)
            {
                if (newBalloons[i] is IHasWriteableItemSlot slot)
                {
                    _eligibleBuffer.Add(slot);
                }
            }
        }

        // Counts by the same capability eligibility uses, so any actor type that can carry an
        // item is included — counting a concrete model type here would silently break the cap.
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

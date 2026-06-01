using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
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
        private readonly ItemConfiguration _itemConfig;
        private readonly SlotGrid _grid;

        private readonly List<ItemSettings> _candidateBuffer = new();
        private readonly List<IHasWriteableItemSlot> _eligibleBuffer = new();
        private readonly Dictionary<string, int> _activeCountsBuffer = new();

        [Inject]
        internal ItemAssigner(
            ItemConfiguration itemConfig,
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

            var turns = msg.TurnCount;
            var items = _itemConfig.Items;

            _candidateBuffer.Clear();
            for (var i = 0; i < items.Count; i++)
            {
                var x = items[i];
                if (x.TurnCheckEvery > 0 && turns % x.TurnCheckEvery == 0)
                {
                    _candidateBuffer.Add(x);
                }
            }

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

            _eligibleBuffer.Clear();
            for (var i = 0; i < msg.NewBalloons.Count; i++)
            {
                if (msg.NewBalloons[i] is IHasWriteableItemSlot slot)
                {
                    _eligibleBuffer.Add(slot);
                }
            }

            if (_eligibleBuffer.Count == 0)
            {
                return;
            }

            var indexOf = Random.Range(0, _eligibleBuffer.Count);
            _eligibleBuffer[indexOf].Item.Value = picked.Type;
        }

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

                    var model = _grid.ActorAt<BalloonModel>(new Vector2Int(col, row));
                    if (model != null && model.Item.Value == type)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}

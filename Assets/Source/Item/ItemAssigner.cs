using System.Collections.Generic;
using System.Linq;
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

            var candidates = items
                .Where(x => x.TurnCheckEvery > 0 && turns % x.TurnCheckEvery == 0)
                .ToArray();

            if (candidates.Length == 0)
            {
                return;
            }

            var activeCounts = new Dictionary<string, int>();
            foreach (var c in candidates)
            {
                activeCounts[c.Type.ToString()] = CountBalloonsWithItem(c.Type);
            }

            var picked = candidates.PickRandom(activeCounts);
            if (picked == null || picked.Type == ItemType.None)
            {
                return;
            }

            var eligible = msg.NewBalloons
                .OfType<IHasWriteableItemSlot>()
                .ToList();

            if (eligible.Count == 0)
            {
                return;
            }

            var indexOf = Random.Range(0, eligible.Count);
            eligible[indexOf].Item.Value = picked.Type;
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

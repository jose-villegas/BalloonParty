using System.Linq;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item
{
    public class ItemAssigner : IStartable
    {
        private readonly ISubscriber<ItemCheckMessage> _checkSubscriber;
        private readonly ItemConfiguration _itemConfig;
        private readonly SlotGrid _grid;

        [Inject]
        public ItemAssigner(
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

            var available = items
                .Where(x => x.TurnCheckEvery > 0 && turns % x.TurnCheckEvery == 0);

            available = available.Where(item =>
            {
                var currentActive = CountBalloonsWithItem(item.Type);
                return currentActive < item.MaximumAllowed;
            });

            var candidates = available.ToArray();

            if (candidates.Length == 0)
            {
                return;
            }

            var sumOfProbabilities = candidates.Sum(x => x.Weight);
            var probabilityCheck = Random.Range(0f, sumOfProbabilities);
            var shift = 0f;

            foreach (var candidate in candidates)
            {
                if (probabilityCheck <= candidate.Weight + shift)
                {
                    if (candidate.Type != ItemType.None)
                    {
                        var eligible = msg.NewBalloons
                            .Where(b => b.CanHoldItem)
                            .ToList();

                        if (eligible.Count == 0)
                        {
                            break;
                        }

                        var indexOf = Random.Range(0, eligible.Count);
                        var balloon = (IWriteableBalloonModel)eligible[indexOf];
                        balloon.Item.Value = candidate.Type;
                    }

                    break;
                }

                shift += candidate.Weight;
            }
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

                    var model = _grid.At(new Vector2Int(col, row));
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

#region

using System.Linq;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.Item
{
    public class ItemAssigner : IStartable
    {
        private readonly IGameConfiguration _config;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<ItemCheckMessage> _checkSubscriber;

        [Inject]
        public ItemAssigner(
            IGameConfiguration config,
            SlotGrid grid,
            ISubscriber<ItemCheckMessage> checkSubscriber)
        {
            _config = config;
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
            var items = _config.ItemConfiguration.Items;

            // Filter by turn frequency
            var available = items
                .Where(x => x.TurnCheckEvery > 0 && turns % x.TurnCheckEvery == 0);

            // Filter by maximum cap
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

            // Weighted random pick
            var sumOfProbabilities = candidates.Sum(x => x.Weight);
            var probabilityCheck = Random.Range(0f, sumOfProbabilities);
            var shift = 0f;

            foreach (var candidate in candidates)
            {
                if (probabilityCheck <= candidate.Weight + shift)
                {
                    if (candidate.Type != ItemType.None)
                    {
                        var indexOf = Random.Range(0, msg.NewBalloons.Count);
                        var balloon = msg.NewBalloons[indexOf];
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

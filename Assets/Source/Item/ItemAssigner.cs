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

            if (!IsCadenceTurn(msg.TurnCount))
            {
                return;
            }

            var candidates = _levelParams.Items;
            if (candidates.Count == 0)
            {
                return;
            }

            _activeCountsBuffer.Clear();
            foreach (var c in candidates)
            {
                _activeCountsBuffer[c.Type.ToString()] = CountBalloonsWithItem(c.Type);
            }

            var picked = _levelParams.PickItemEntry(_activeCountsBuffer);
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

        // The level's shared item-drop cadence replaces the old per-item TurnCheckEvery catalog
        // check — one shared frequency, item TYPE MIX decided by the weighted pick below.
        private bool IsCadenceTurn(int turns)
        {
            var cadence = _levelParams.ItemCadence;
            return cadence > 0 && turns % cadence == 0;
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

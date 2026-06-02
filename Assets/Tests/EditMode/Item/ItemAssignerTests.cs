using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class ItemAssignerTests
    {
        private SlotGrid _grid;
        private IItemConfiguration _itemConfig;
        private IMessageHandler<ItemCheckMessage> _handler;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<IGameConfiguration>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(gameConfig, new BalancePathHolder());

            _itemConfig = Substitute.For<IItemConfiguration>();
            _itemConfig.Items.Returns(new List<ItemSettings>());

            var subscriber = Substitute.For<ISubscriber<ItemCheckMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ItemCheckMessage>>(h => _handler = h),
                    Arg.Any<MessageHandlerFilter<ItemCheckMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var assigner = new ItemAssigner(_itemConfig, _grid, subscriber);
            assigner.Start();
        }

        [Test]
        public void OnItemCheck_EmptyNewBalloons_NoAssignment()
        {
            SetItems(CreateItemSettings(ItemType.Bomb, turnCheckEvery: 1, weight: 1f, maxAllowed: 5));

            var model = new BalloonModel();
            FireItemCheck(Array.Empty<IBalloonModel>(), turnCount: 1);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_TurnNotDivisible_NoAssignment()
        {
            SetItems(CreateItemSettings(ItemType.Bomb, turnCheckEvery: 3, weight: 1f, maxAllowed: 5));

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 2);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_AllItemsAtMax_NoAssignment()
        {
            SetItems(CreateItemSettings(ItemType.Bomb, turnCheckEvery: 1, weight: 1f, maxAllowed: 1));

            // Place an existing balloon with the item on the grid to hit the cap
            var existing = new BalloonModel();
            existing.Item.Value = ItemType.Bomb;
            _grid.Place(existing, null, new Vector2Int(0, 0));

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_NoEligibleBalloons_NonItemSlotActor_NoAssignment()
        {
            SetItems(CreateItemSettings(ItemType.Bomb, turnCheckEvery: 1, weight: 1f, maxAllowed: 5));

            var model = new ToughBalloonModel(new BalloonModelConfig());
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            // ToughBalloonModel does not implement IHasItemSlot — ItemAssigner skips it
            Assert.IsFalse(model is IHasItemSlot);
        }

        [Test]
        public void OnItemCheck_EligibleBalloon_GetsItemAssigned()
        {
            SetItems(CreateItemSettings(ItemType.Bomb, turnCheckEvery: 1, weight: 1f, maxAllowed: 5));

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            Assert.AreEqual(ItemType.Bomb, model.Item.Value);
        }

        private void FireItemCheck(IReadOnlyList<IBalloonModel> balloons, int turnCount)
        {
            _handler.Handle(new ItemCheckMessage(balloons, turnCount));
        }

        private void SetItems(params ItemSettings[] items)
        {
            _itemConfig.Items.Returns(new List<ItemSettings>(items));
        }

        private static ItemSettings CreateItemSettings(
            ItemType type,
            int turnCheckEvery,
            float weight,
            int maxAllowed)
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", type);
            SetField(settings, "_turnCheckEvery", turnCheckEvery);
            SetField(settings, "_weight", weight);
            SetField(settings, "_maximumAllowed", maxAllowed);
            return settings;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}

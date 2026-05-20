using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item.Lightning;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class LightningItemHandlerTests
    {
        private SlotGrid _grid;
        private ItemConfiguration _itemConfig;
        private IPublisher<ActorHitMessage> _hitPublisher;
        private LightningItemHandler _handler;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<IGameConfiguration>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(gameConfig);

            _itemConfig = ScriptableObject.CreateInstance<ItemConfiguration>();
            var lightningSettings = CreateItemSettings(ItemType.Lightning, damage: 1);
            SetField(_itemConfig, "_items", new List<ItemSettings> { lightningSettings });

            _hitPublisher = Substitute.For<IPublisher<ActorHitMessage>>();

            _handler = new LightningItemHandler(
                _itemConfig,
                _hitPublisher,
                _grid,
                new PoolManager());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_itemConfig);
        }

        [Test]
        public void Activate_NoSameColorBalloons_PublishesNoHits()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Blue");

            _handler.Setup(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));
            _handler.Activate();

            _hitPublisher.DidNotReceive().Publish(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_SameColorBalloons_PublishesHitForEach()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");
            PlaceBalloon(2, 0, "Red");

            _handler.Setup(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));
            _handler.Activate();

            _hitPublisher.Received(2).Publish(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_ExcludesSelfFromTargets()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");

            _handler.Setup(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));
            _handler.Activate();

            _hitPublisher.Received(1).Publish(Arg.Any<ActorHitMessage>());
            _hitPublisher.DidNotReceive().Publish(
                Arg.Is<ActorHitMessage>(m => ReferenceEquals(m.Actor, source)));
        }

        [Test]
        public void Activate_AppliesConfiguredDamage()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");

            _handler.Setup(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));
            _handler.Activate();

            _hitPublisher.Received(1).Publish(
                Arg.Is<ActorHitMessage>(m => m.Damage == 1));
        }

        private BalloonModel PlaceBalloon(int col, int row, string color)
        {
            var model = new BalloonModel();
            model.Color.Value = color;
            _grid.Place(model, null, new Vector2Int(col, row));
            return model;
        }

        private static ItemSettings CreateItemSettings(ItemType type, int damage)
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", type);
            SetField(settings, "_damage", damage);
            SetField(settings, "_turnCheckEvery", 0);
            SetField(settings, "_weight", 0f);
            SetField(settings, "_maximumAllowed", 0);
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


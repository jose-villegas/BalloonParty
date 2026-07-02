using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Item.Shield;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class ShieldItemHandlerTests
    {
        private IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private ShieldItemHandler _handler;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;

        [SetUp]
        public void SetUp()
        {
            var itemConfig = Substitute.For<IItemConfiguration>();
            var shieldSettings = CreateItemSettings(ItemType.Shield);
            itemConfig[ItemType.Shield].Returns(shieldSettings);
            itemConfig.Items.Returns(new List<ItemSettings> { shieldSettings });

            _shieldGainedPublisher = Substitute.For<IPublisher<ShieldGainedMessage>>();

            var loadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            loadedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ProjectileLoadedMessage>>(h => _loadedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());

            _handler = new ShieldItemHandler(
                itemConfig,
                _shieldGainedPublisher,
                loadedSubscriber,
                new ItemEffectPlayer(new PoolManager(), palette));

            _handler.Start();
        }

        [Test]
        public void Activate_WithProjectile_IncrementsShield()
        {
            var projectile = new ProjectileModel();
            projectile.ShieldsRemaining.Value = 1;
            SimulateProjectileLoaded(projectile);

            var balloon = CreateBalloon(new Vector2Int(2, 3));
            _handler.Activate(balloon, Vector3.zero);

            Assert.AreEqual(2, projectile.ShieldsRemaining.Value);
        }

        [Test]
        public void Activate_WithoutProjectile_DoesNotThrow()
        {
            var balloon = CreateBalloon(new Vector2Int(0, 0));

            Assert.DoesNotThrow(() => _handler.Activate(balloon, Vector3.zero));
        }

        [Test]
        public void Activate_PublishesShieldGainedWithCorrectSlot()
        {
            var slot = new Vector2Int(3, 5);
            var balloon = CreateBalloon(slot);
            _handler.Activate(balloon, Vector3.zero);

            _shieldGainedPublisher.Received(1).Publish(
                Arg.Is<ShieldGainedMessage>(m => m.SlotIndex == slot));
        }

        private void SimulateProjectileLoaded(ProjectileModel projectile)
        {
            _loadedHandler.Handle(new ProjectileLoadedMessage(projectile));
        }

        private static BalloonModel CreateBalloon(Vector2Int slot)
        {
            var model = new BalloonModel();
            model.SlotIndex.Value = slot;
            return model;
        }

        private static ItemSettings CreateItemSettings(ItemType type)
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", type);
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

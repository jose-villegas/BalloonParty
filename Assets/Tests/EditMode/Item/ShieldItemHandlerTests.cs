using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Item.Shield;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class ShieldItemHandlerTests
    {
        private IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private ShieldItemHandler _handler;
        private ProjectileRegistry _registry;
        private IGamePalette _palette;
        private IProjectileBuffs _buffs;

        [SetUp]
        public void SetUp()
        {
            var itemConfig = Substitute.For<IItemConfiguration>();
            var shieldSettings = CreateItemSettings(ItemType.Shield);
            itemConfig[ItemType.Shield].Returns(shieldSettings);
            itemConfig.Items.Returns(new List<ItemSettings> { shieldSettings });

            _shieldGainedPublisher = Substitute.For<IPublisher<ShieldGainedMessage>>();

            _registry = new ProjectileRegistry();

            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());
            _buffs = Substitute.For<IProjectileBuffs>();

            var wallBounces = Substitute.For<ISubscriber<ShieldLostMessage>>();
            wallBounces
                .Subscribe(
                    Arg.Any<IMessageHandler<ShieldLostMessage>>(),
                    Arg.Any<MessageHandlerFilter<ShieldLostMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _handler = new ShieldItemHandler(
                itemConfig,
                _shieldGainedPublisher,
                wallBounces,
                new ItemEffectPlayer(new PoolManager(), _palette),
                _palette,
                _buffs,
                _registry);
        }

        [Test]
        public void Activate_WithProjectile_IncrementsShield()
        {
            var projectile = new ProjectileModel();
            projectile.ShieldsRemaining.Value = 1;
            SimulateProjectileLoaded(projectile);

            var balloon = CreateBalloon(new Vector2Int(2, 3));
            _handler.Activate(new ItemActivationContext(balloon, Vector3.zero, Vector3.zero));

            Assert.AreEqual(2, projectile.ShieldsRemaining.Value);
        }

        [Test]
        public void Activate_ContextNamesAnInFlightBall_ShieldsThatBallNotTheLoadedOne()
        {
            var inFlight = new ProjectileModel();
            _registry.Load(inFlight, null);
            _registry.Launch(inFlight);
            var loaded = new ProjectileModel();
            _registry.Load(loaded, null);

            var balloon = CreateBalloon(new Vector2Int(2, 3));
            _handler.Activate(new ItemActivationContext(
                balloon, Vector3.zero, Vector3.zero, new DamageContext(1, DamageFlags.Normal, "Red", inFlight)));

            Assert.AreEqual(1, inFlight.ShieldsRemaining.Value);
            Assert.AreEqual(0, loaded.ShieldsRemaining.Value);
        }

        [Test]
        public void Activate_ContextNamesABallAlreadyGone_FallsBackToTheCurrentBall()
        {
            var dead = new ProjectileModel();
            _registry.Load(dead, null);
            _registry.Launch(dead);
            _registry.Land(dead);
            var loaded = new ProjectileModel();
            _registry.Load(loaded, null);

            var balloon = CreateBalloon(new Vector2Int(2, 3));
            _handler.Activate(new ItemActivationContext(
                balloon, Vector3.zero, Vector3.zero, new DamageContext(1, DamageFlags.Normal, "Red", dead)));

            Assert.AreEqual(1, loaded.ShieldsRemaining.Value, "the reward isn't lost with the ball");
            Assert.AreEqual(0, dead.ShieldsRemaining.Value);
        }

        [Test]
        public void Activate_WithoutProjectile_DoesNotThrow()
        {
            var balloon = CreateBalloon(new Vector2Int(0, 0));

            Assert.DoesNotThrow(() => _handler.Activate(new ItemActivationContext(balloon, Vector3.zero, Vector3.zero)));
        }

        [Test]
        public void Activate_PublishesShieldGainedWithCorrectSlot()
        {
            var slot = new Vector2Int(3, 5);
            var balloon = CreateBalloon(slot);
            _handler.Activate(new ItemActivationContext(balloon, Vector3.zero, Vector3.zero));

            _shieldGainedPublisher.Received(1).Publish(
                Arg.Is<ShieldGainedMessage>(m => m.SlotIndex == slot));
        }

        // The speed buff was deliberately dropped from rainbow shield activation (6caa776d) —
        // only the rainbow-shield buff remains.
        [Test]
        public void Activate_RainbowHolder_AppliesOnlyRainbowShieldBuff()
        {
            _palette.IsRainbow(GamePalette.RainbowColorId).Returns(true);
            var projectile = new ProjectileModel();
            SimulateProjectileLoaded(projectile);
            var balloon = CreateBalloon(new Vector2Int(1, 1));
            balloon.Color.Value = GamePalette.RainbowColorId;

            _handler.Activate(new ItemActivationContext(balloon, Vector3.zero, Vector3.zero));

            _buffs.Received(1).Apply(Arg.Any<IProjectileModel>(), Arg.Any<ProjectileBuff>());
            _buffs.Received(1).Apply(projectile, Arg.Is<ProjectileBuff>(b => b.Id == ProjectileBuffId.RainbowShield));
        }

        [Test]
        public void Activate_NormalHolder_DoesNotApplyBuff()
        {
            SimulateProjectileLoaded(new ProjectileModel());
            var balloon = CreateBalloon(new Vector2Int(1, 1));
            balloon.Color.Value = "Red";

            _handler.Activate(new ItemActivationContext(balloon, Vector3.zero, Vector3.zero));

            _buffs.DidNotReceive().Apply(Arg.Any<IProjectileModel>(), Arg.Any<ProjectileBuff>());
        }

        private void SimulateProjectileLoaded(ProjectileModel projectile)
        {
            _registry.Load(projectile, null);
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

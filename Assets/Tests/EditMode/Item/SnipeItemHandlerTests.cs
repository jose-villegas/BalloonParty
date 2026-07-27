using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item;
using BalloonParty.Item.Snipe;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     The Snipe item's banking rule: two pierces can't overlap, so a pickup landing on an
    ///     already-armed shot is saved whole (pierce, speed and rainbow) and activates at the discharge
    ///     that spends the running pierce — instead of being silently swallowed, which is what a plain
    ///     "re-arm the idempotent flag" did before (the flag was already true and the discharge was
    ///     already inevitable, so the item bought nothing).
    /// </summary>
    [TestFixture]
    public class SnipeItemHandlerTests
    {
        private const float SpeedMultiplier = 1.6f;

        private SnipeItemHandler _handler;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;
        private IMessageHandler<PierceDischargedMessage> _dischargedHandler;
        private IGamePalette _palette;
        private IProjectileBuffs _buffs;

        [SetUp]
        public void SetUp()
        {
            var itemConfig = Substitute.For<IItemConfiguration>();
            var snipeSettings = CreateSnipeSettings();
            itemConfig[ItemType.Snipe].Returns(snipeSettings);
            itemConfig.Items.Returns(new List<ItemSettings> { snipeSettings });

            var loadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            loadedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ProjectileLoadedMessage>>(h => _loadedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var dischargedSubscriber = Substitute.For<ISubscriber<PierceDischargedMessage>>();
            dischargedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<PierceDischargedMessage>>(h => _dischargedHandler = h),
                    Arg.Any<MessageHandlerFilter<PierceDischargedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());
            _buffs = Substitute.For<IProjectileBuffs>();

            _handler = new SnipeItemHandler(
                itemConfig,
                _palette,
                new ItemEffectPlayer(new PoolManager(), _palette),
                _buffs,
                loadedSubscriber,
                dischargedSubscriber);

            _handler.Start();
        }

        [Test]
        public void Activate_UnarmedShot_ArmsTheLanceAndGrantsSpeedImmediately()
        {
            var projectile = LoadProjectile();

            Activate(projectile);

            Assert.IsTrue(projectile.IsPiercing.Value);
            Assert.AreEqual(0, projectile.Flight.BankedPierceCharges, "an unarmed shot arms now, banks nothing");
            _buffs.Received(1).Apply(Arg.Is<ProjectileBuff>(b => b.Id == ProjectileBuffId.Speed));
        }

        [Test]
        public void Activate_AlreadyPiercing_BanksTheWholeGrantInsteadOfApplyingIt()
        {
            var projectile = LoadProjectile();
            projectile.IsPiercing.Value = true;

            Activate(projectile);

            Assert.AreEqual(1, projectile.Flight.BankedPierceCharges);
            Assert.IsTrue(projectile.IsPiercing.Value, "the running pierce is untouched");
            _buffs.DidNotReceive().Apply(Arg.Any<ProjectileBuff>());
        }

        [Test]
        public void Activate_AlreadyPiercingRainbowHost_BanksARainbowCharge()
        {
            var projectile = LoadProjectile();
            projectile.IsPiercing.Value = true;

            Activate(projectile, isRainbowHost: true);

            Assert.AreEqual(1, projectile.Flight.BankedRainbowPierceCharges);
            Assert.AreEqual(0, projectile.Flight.BankedPierceCharges);
            _buffs.DidNotReceive().Apply(Arg.Any<ProjectileBuff>());
        }

        // The discharge leaves IsPiercing armed when a charge is banked (ProjectileModelExtensions
        // .SpendPierce), so the handler's job here is the GRANT the pickup deferred.
        [Test]
        public void PierceDischarged_WithBankedCharge_SpendsItAndGrantsTheSpeedBuff()
        {
            var projectile = LoadProjectile();
            projectile.IsPiercing.Value = true;
            Activate(projectile);

            Discharge();

            Assert.AreEqual(0, projectile.Flight.BankedPierceCharges, "the charge is spent, not re-usable");
            Assert.IsTrue(projectile.IsPiercing.Value, "the lance is re-armed");
            _buffs.Received(1).Apply(Arg.Is<ProjectileBuff>(
                b => b.Id == ProjectileBuffId.Speed && Mathf.Approximately(b.Value, SpeedMultiplier)));
        }

        // Stacking: pickups accumulate, and each discharge spends exactly one — so three mid-pierce
        // pickups buy three more lances, not one refreshed lance and two wasted items.
        [Test]
        public void PierceDischarged_MultipleBankedCharges_SpendsOnePerDischarge()
        {
            var projectile = LoadProjectile();
            projectile.IsPiercing.Value = true;
            Activate(projectile);
            Activate(projectile);
            Activate(projectile);
            Assert.AreEqual(3, projectile.Flight.BankedPierceCharges);

            Discharge();
            Assert.AreEqual(2, projectile.Flight.BankedPierceCharges);

            Discharge();
            Assert.AreEqual(1, projectile.Flight.BankedPierceCharges);
        }

        // Rainbow charges are the stronger grant, so they go first.
        [Test]
        public void PierceDischarged_MixedBankedCharges_SpendsTheRainbowOneFirst()
        {
            var projectile = LoadProjectile();
            projectile.IsPiercing.Value = true;
            Activate(projectile);
            Activate(projectile, isRainbowHost: true);

            Discharge();

            Assert.AreEqual(0, projectile.Flight.BankedRainbowPierceCharges);
            Assert.AreEqual(1, projectile.Flight.BankedPierceCharges, "the plain charge waits for the next discharge");
            _buffs.Received(1).Apply(Arg.Is<ProjectileBuff>(b => b.Id == ProjectileBuffId.RainbowShield));
        }

        // "Piercing re-arms as long as the projectile still has shields": the discharge flush also runs
        // for a shot that died on the wall it discharged at (ProjectileView.DestroyProjectile), and that
        // one must not resurrect a lance.
        [Test]
        public void PierceDischarged_ShotOutOfShields_LeavesTheChargeUnspent()
        {
            var projectile = LoadProjectile();
            projectile.IsPiercing.Value = true;
            Activate(projectile);
            projectile.ShieldsRemaining.Value = -1;

            Discharge();

            Assert.AreEqual(1, projectile.Flight.BankedPierceCharges);
            _buffs.DidNotReceive().Apply(Arg.Any<ProjectileBuff>());
        }

        // A rainbow Shield item's grant ends at the next plain wall bounce; a rainbow lance's must last the
        // pierce. Skipping the grant because SOME RainbowShield was already riding left the lance on the
        // wall-ended one, so its iridescence died early — the flag buff is applied regardless now.
        [Test]
        public void Activate_RainbowHostWithAWallEndedRainbowAlreadyRiding_StillGrantsThePierceTiedOne()
        {
            var projectile = LoadProjectile();
            var wallBounces = Substitute.For<ISubscriber<ShieldLostMessage>>();
            wallBounces
                .Subscribe(
                    Arg.Any<IMessageHandler<ShieldLostMessage>>(),
                    Arg.Any<MessageHandlerFilter<ShieldLostMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            projectile.AddBuff(new ProjectileBuff(
                ProjectileBuffId.RainbowShield, 0f, BuffModifierOp.Flat,
                new WallBounceEndCondition(wallBounces)));

            Activate(projectile, isRainbowHost: true);

            _buffs.Received(1).Apply(Arg.Is<ProjectileBuff>(
                b => b.Id == ProjectileBuffId.RainbowShield && b.EndCondition is PierceEndedEndCondition));
        }

        [Test]
        public void PierceDischarged_NothingBanked_GrantsNothing()
        {
            var projectile = LoadProjectile();
            projectile.IsPiercing.Value = true;

            Discharge();

            _buffs.DidNotReceive().Apply(Arg.Any<ProjectileBuff>());
        }

        private ProjectileModel LoadProjectile()
        {
            var projectile = new ProjectileModel();
            projectile.ShieldsRemaining.Value = 3;
            _loadedHandler.Handle(new ProjectileLoadedMessage(projectile));
            return projectile;
        }

        private void Activate(IProjectileModel projectile, bool isRainbowHost = false)
        {
            var balloon = new BalloonModel();
            balloon.SlotIndex.Value = new Vector2Int(1, 1);
            balloon.Color.Value = isRainbowHost ? GamePalette.RainbowColorId : "Red";
            _palette.IsRainbow(balloon.Color.Value).Returns(isRainbowHost);

            // The handler reports no buff as riding until the test's own substitute says otherwise; a
            // real shot's buff list is owned by ProjectileBuffService, which these tests substitute out.
            _handler.Activate(new ItemActivationContext(balloon, Vector3.zero, Vector3.zero));
        }

        private void Discharge()
        {
            _dischargedHandler.Handle(new PierceDischargedMessage(Vector3.zero, 1, false));
        }

        private static ItemSettings CreateSnipeSettings()
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", ItemType.Snipe);
            SetField(settings, "_maximumAllowed", 0);
            SetField(settings.Snipe, "_snipeSpeedBuffMultiplier", SpeedMultiplier);
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

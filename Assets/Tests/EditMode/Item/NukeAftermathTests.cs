using System;
using BalloonParty.Shared;
using BalloonParty.Item.Snipe;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     The Snipe roll's lift after a nuke: the card's chance is scaled for a stretch of game time from the blast,
    ///     and not before it, after it or in the next run.
    /// </summary>
    [TestFixture]
    public class NukeAftermathTests
    {
        private IMessageHandler<NukeDetonatedMessage> _detonated;
        private IUpgradeSettings _settings;
        private NukeAftermath _aftermath;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            var subscriber = Substitute.For<ISubscriber<NukeDetonatedMessage>>();
            subscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<NukeDetonatedMessage>>(),
                    Arg.Any<MessageHandlerFilter<NukeDetonatedMessage>[]>())
                .Returns(call =>
                {
                    _detonated = call.Arg<IMessageHandler<NukeDetonatedMessage>>();
                    return Substitute.For<IDisposable>();
                });
            _settings = Substitute.For<IUpgradeSettings>();
            _settings.NukeAftermathSnipeChanceScale.Returns(1.5f);
            _settings.NukeAftermathSnipeSeconds.Returns(20f);
            _now = 100f;
            _aftermath = new NukeAftermath(subscriber, _settings) { Now = () => _now };
            _aftermath.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _aftermath.Dispose();
        }

        [Test]
        public void NoNukeYet_LeavesTheChanceAlone()
        {
            Assert.AreEqual(1f, _aftermath.SnipeChanceScale);
        }

        [Test]
        public void AfterANukeGoesOff_TheChanceIsScaled_ForTheAftermathsSeconds()
        {
            Detonate();
            Assert.AreEqual(1.5f, _aftermath.SnipeChanceScale, "at the blast");

            _now += 19.9f;
            Assert.AreEqual(1.5f, _aftermath.SnipeChanceScale, "just inside the stretch");

            _now += 0.2f;
            Assert.AreEqual(1f, _aftermath.SnipeChanceScale, "past it");
        }

        [Test]
        public void ASecondNuke_StartsTheStretchAgain()
        {
            Detonate();
            _now += 15f;
            Detonate();

            _now += 15f;

            Assert.AreEqual(1.5f, _aftermath.SnipeChanceScale, "15 s after the second, 30 after the first");
        }

        [Test]
        public void AScaleBelowOne_NeverLowersTheChance()
        {
            _settings.NukeAftermathSnipeChanceScale.Returns(0.2f);

            Detonate();

            Assert.AreEqual(1f, _aftermath.SnipeChanceScale);
        }

        [Test]
        public void NoSeconds_NeverScales()
        {
            _settings.NukeAftermathSnipeSeconds.Returns(0f);

            Detonate();

            Assert.AreEqual(1f, _aftermath.SnipeChanceScale);
        }

        [Test]
        public void ANewRun_StartsWithNoAftermath()
        {
            Detonate();

            _aftermath.ResetRun(1);

            Assert.AreEqual(1f, _aftermath.SnipeChanceScale);
        }

        private void Detonate()
        {
            _detonated.Handle(new NukeDetonatedMessage(Vector3.zero));
        }
    }
}

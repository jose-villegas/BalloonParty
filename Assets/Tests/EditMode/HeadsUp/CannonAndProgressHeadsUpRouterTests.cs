using System.Linq;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.HeadsUp;
using BalloonParty.Game.HeadsUp.Routing;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using static BalloonParty.Tests.HeadsUp.HeadsUpRouterTestKit;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     The heads-ups at the HUD's endpoints: the cannon's held <c>ready</c> (<c>snipe!</c> for a ball with the
    ///     lance) released by the shot, the heart's <c>-1</c>, and a colour's <c>done</c> at its bar.
    /// </summary>
    [TestFixture]
    public class CannonAndProgressHeadsUpRouterTests
    {
        [Test]
        public void TheCannonReady_IsHeldAtTheCannon_UntilTheShot()
        {
            var headsUps = new RecordingHeadsUps();
            var requests = headsUps.Requests;
            IMessageHandler<ThrowerReadyMessage> onReady = null;
            IMessageHandler<ProjectileFiredMessage> onFired = null;
            var router = new CannonHeadsUpRouter(headsUps,
                CaptureSubscriber<ThrowerReadyMessage>(h => onReady = h),
                CaptureSubscriber<ProjectileFiredMessage>(h => onFired = h));
            router.Start();
            var ball = Substitute.For<IProjectileModel>();

            onReady.Handle(new ThrowerReadyMessage(ball, false));
            onFired.Handle(new ProjectileFiredMessage(ball, Vector3.zero, Vector3.up));

            Assert.AreEqual(HeadsUpStyleId.Ready, requests[0].Style);
            Assert.IsTrue(requests[0].Anchor.IsEndpoint);
            Assert.AreEqual(TrailEndpointKeys.Cannon, requests[0].Anchor.EndpointKey);
            Assert.AreEqual(new[] { new HeadsUpHandle(0, 1) }, headsUps.Released);
            router.Dispose();
        }

        [Test]
        public void ABallLoadedWithTheLance_SaysSnipe_InsteadOfReady()
        {
            var headsUps = new RecordingHeadsUps();
            IMessageHandler<ThrowerReadyMessage> onReady = null;
            var router = new CannonHeadsUpRouter(headsUps,
                CaptureSubscriber<ThrowerReadyMessage>(h => onReady = h),
                CaptureSubscriber<ProjectileFiredMessage>(_ => { }));
            router.Start();

            onReady.Handle(new ThrowerReadyMessage(Ball(new ReactiveProperty<bool>(true)), false));

            Assert.AreEqual(1, headsUps.Requests.Count);
            Assert.AreEqual(HeadsUpStyleId.SnipeReady, headsUps.Requests[0].Style);
            Assert.AreEqual(TrailEndpointKeys.Cannon, headsUps.Requests[0].Anchor.EndpointKey);
            router.Dispose();
        }

        // A Snipe popped while the ball waits arms it: the ready already showing gives way to snipe.
        [Test]
        public void ABallArmedWhileItWaits_SwapsReadyForSnipe()
        {
            var headsUps = new RecordingHeadsUps();
            IMessageHandler<ThrowerReadyMessage> onReady = null;
            var router = new CannonHeadsUpRouter(headsUps,
                CaptureSubscriber<ThrowerReadyMessage>(h => onReady = h),
                CaptureSubscriber<ProjectileFiredMessage>(_ => { }));
            router.Start();
            var lance = new ReactiveProperty<bool>(false);

            onReady.Handle(new ThrowerReadyMessage(Ball(lance), false));
            lance.Value = true;

            Assert.AreEqual(new[] { HeadsUpStyleId.Ready, HeadsUpStyleId.SnipeReady },
                headsUps.Requests.Select(r => r.Style).ToArray());
            Assert.AreEqual(new[] { new HeadsUpHandle(0, 1) }, headsUps.Released);
            router.Dispose();
        }

        // Once fired, the ball is not the cannon's any more: a lance it gets in flight writes nothing there,
        // and losing it back in the pool doesn't bring ready back.
        [Test]
        public void ALanceAfterTheShot_LeavesTheCannonAlone()
        {
            var headsUps = new RecordingHeadsUps();
            IMessageHandler<ThrowerReadyMessage> onReady = null;
            IMessageHandler<ProjectileFiredMessage> onFired = null;
            var router = new CannonHeadsUpRouter(headsUps,
                CaptureSubscriber<ThrowerReadyMessage>(h => onReady = h),
                CaptureSubscriber<ProjectileFiredMessage>(h => onFired = h));
            router.Start();
            var lance = new ReactiveProperty<bool>(false);
            var ball = Ball(lance);

            onReady.Handle(new ThrowerReadyMessage(ball, false));
            onFired.Handle(new ProjectileFiredMessage(ball, Vector3.zero, Vector3.up));
            lance.Value = true;
            lance.Value = false;

            Assert.AreEqual(1, headsUps.Requests.Count);
            Assert.AreEqual(HeadsUpStyleId.Ready, headsUps.Requests[0].Style);
            router.Dispose();
        }

        // A second shot before the next ready has nothing of its own to release.
        [Test]
        public void AShotWithNoReadyShowing_ReleasesNothing()
        {
            var headsUps = new RecordingHeadsUps();
            IMessageHandler<ProjectileFiredMessage> onFired = null;
            var router = new CannonHeadsUpRouter(headsUps, CaptureSubscriber<ThrowerReadyMessage>(_ => { }),
                CaptureSubscriber<ProjectileFiredMessage>(h => onFired = h));
            router.Start();

            onFired.Handle(new ProjectileFiredMessage(null, Vector3.zero, Vector3.up));

            Assert.AreEqual(1, headsUps.Released.Count);
            Assert.IsFalse(headsUps.Released[0].IsValid);
            router.Dispose();
        }

        [Test]
        public void AStrikethrough_WritesMinusOneAtTheHeart()
        {
            var headsUps = new RecordingHeadsUps();
            IMessageHandler<StrikethroughArrivedMessage> onStrikethrough = null;
            var router = new ProgressHeadsUpRouter(headsUps,
                CaptureSubscriber<StrikethroughArrivedMessage>(h => onStrikethrough = h),
                CaptureSubscriber<ProgressBarCompletedMessage>(_ => { }));
            router.Start();

            onStrikethrough.Handle(new StrikethroughArrivedMessage(0));

            Assert.AreEqual(HeadsUpStyleId.HeartLost, headsUps.Requests[0].Style);
            Assert.AreEqual(TrailEndpointKeys.Heart, headsUps.Requests[0].Anchor.EndpointKey);
            router.Dispose();
        }

        [Test]
        public void ABarFilling_WritesItsColourDone_AtThatBar()
        {
            var headsUps = new RecordingHeadsUps();
            IMessageHandler<ProgressBarCompletedMessage> onBar = null;
            var router = new ProgressHeadsUpRouter(headsUps,
                CaptureSubscriber<StrikethroughArrivedMessage>(_ => { }),
                CaptureSubscriber<ProgressBarCompletedMessage>(h => onBar = h));
            router.Start();

            onBar.Handle(new ProgressBarCompletedMessage("Blue"));

            var request = headsUps.Requests[0];
            Assert.AreEqual(HeadsUpStyleId.ColorDone, request.Style);
            Assert.AreEqual("Blue", request.Anchor.EndpointKey);
            Assert.AreEqual("Blue", request.Word);
            router.Dispose();
        }

        private static IProjectileModel Ball(IReadOnlyReactiveProperty<bool> lance)
        {
            var ball = Substitute.For<IProjectileModel>();
            ball.IsPiercing.Returns(lance);
            return ball;
        }
    }
}

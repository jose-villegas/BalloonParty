using System;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    /// <summary>
    ///     The meadow's mushrooms in sound (phase 4 of <c>Plans/PLAN-MeadowMushrooms.md</c>): the mushrooms' own
    ///     soft bump on every bounce, climbing a whole tone with each mushroom the chain counts, the Snipe's cue
    ///     when a chain pays the lance, a stroke for each cap a lance cuts, and nothing for a cap a blast blew off.
    /// </summary>
    [TestFixture]
    public class MushroomSoundRouterTests
    {
        private static readonly Vector3 Point = new(1f, 2f, 0f);

        private ISoundPlayer _player;
        private IMessageHandler<MushroomBouncedMessage> _bounced;
        private IMessageHandler<MushroomLanceMessage> _lance;
        private IMessageHandler<MushroomCutMessage> _cut;
        private MushroomSoundRouter _router;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();
            _router = new MushroomSoundRouter(_player,
                CaptureSubscriber<MushroomBouncedMessage>(h => _bounced = h),
                CaptureSubscriber<MushroomLanceMessage>(h => _lance = h),
                CaptureSubscriber<MushroomCutMessage>(h => _cut = h));
            _router.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _router.Dispose();
        }

        [Test]
        public void TheFirstMushroom_BumpsAtTheRoot_WhereTheBallTouched_UnderThePops()
        {
            Bounce(count: 1);

            _player.Received(1).Play(GameSoundId.MushroomBounce, Point, null, 0, MushroomSoundRouter.BounceVolume);
            Assert.AreEqual(GameSoundId.MushroomBounce, MushroomSoundRouter.BounceSound, "the mushrooms' own clip");
            Assert.Less(MushroomSoundRouter.BounceVolume, 1f, "quieter than its clip at full");
        }

        [Test]
        public void EachMushroomCounted_KnocksAWholeToneHigher()
        {
            Bounce(count: 1);
            Bounce(count: 2);
            Bounce(count: 3);

            Received.InOrder(() =>
            {
                _player.Play(MushroomSoundRouter.BounceSound, Point, null, 0, MushroomSoundRouter.BounceVolume);
                _player.Play(MushroomSoundRouter.BounceSound, Point, null, 2, MushroomSoundRouter.BounceVolume);
                _player.Play(MushroomSoundRouter.BounceSound, Point, null, 4, MushroomSoundRouter.BounceVolume);
            });
        }

        [Test]
        public void ABounceThatDidNotCount_RepeatsItsNote_AndAWallStartsTheClimbOver()
        {
            Bounce(count: 2);
            Bounce(count: 2);
            Bounce(count: 1);

            _player.Received(2).Play(MushroomSoundRouter.BounceSound, Point, null, 2, MushroomSoundRouter.BounceVolume);
            _player.Received(1).Play(MushroomSoundRouter.BounceSound, Point, null, 0, MushroomSoundRouter.BounceVolume);
        }

        [Test]
        public void TheClimb_StopsAtItsTopNote_AndNeverGoesUnderTheRoot()
        {
            Bounce(count: 9);
            Bounce(count: 0);

            _player.Received(1).Play(
                MushroomSoundRouter.BounceSound, Point, null, MushroomSoundRouter.MaxBounceSteps * 2,
                MushroomSoundRouter.BounceVolume);
            _player.Received(1).Play(MushroomSoundRouter.BounceSound, Point, null, 0, MushroomSoundRouter.BounceVolume);
        }

        // The bump has an entry of its own in the bank, so that entry's voice limit is the bump's and nobody
        // else's: the router plays every bounce and ends none of them itself.
        [Test]
        public void ARunOfBumps_IsEachPlayed_AndTheRouterStopsNone()
        {
            _player.Play(default, default).ReturnsForAnyArgs(
                new SoundHandle(1, 1u), new SoundHandle(2, 1u), new SoundHandle(3, 1u), new SoundHandle(4, 1u),
                new SoundHandle(5, 1u), new SoundHandle(6, 1u));
            _player.IsAlive(Arg.Any<SoundHandle>()).Returns(true);

            for (var count = 1; count <= 6; count++)
            {
                Bounce(count);
            }

            _player.ReceivedWithAnyArgs(6).Play(default, default);
            _player.DidNotReceiveWithAnyArgs().Stop(default);
        }

        [Test]
        public void ALanceEarned_PlaysTheSnipesCue_AtItsOwnPitch()
        {
            var ball = Substitute.For<IProjectileModel>();

            _lance.Handle(new MushroomLanceMessage(ball, Point, count: 3));

            _player.Received(1).Play(GameSoundId.ItemSnipe, null, null, 0, 1f);
            _player.ReceivedWithAnyArgs(1).Play(default, default);
        }

        // A cut is not a bump: it has a sound of its own (the strikethrough's stroke, until the mushrooms have
        // theirs), at the cap, once a cap.
        [Test]
        public void ACutCap_PlaysTheStroke_WhereTheCapWasCut_AndStopsNoBump()
        {
            Assert.AreEqual(GameSoundId.Strikethrough, MushroomSoundRouter.CutSound);
            Assert.AreNotEqual(MushroomSoundRouter.BounceSound, MushroomSoundRouter.CutSound);
            _player.Play(default, default).ReturnsForAnyArgs(new SoundHandle(1, 1u));
            _player.IsAlive(Arg.Any<SoundHandle>()).Returns(true);
            Bounce(count: 1);
            _player.ClearReceivedCalls();

            _cut.Handle(new MushroomCutMessage(new Vector2Int(2, 4), new Vector2(1.5f, 2.5f), Vector2.up));
            _cut.Handle(new MushroomCutMessage(new Vector2Int(3, 2), new Vector2(-1f, 3f), Vector2.up));

            Received.InOrder(() =>
            {
                _player.Play(MushroomSoundRouter.CutSound, new Vector3(1.5f, 2.5f, 0f), null, 0, MushroomSoundRouter.CutVolume);
                _player.Play(MushroomSoundRouter.CutSound, new Vector3(-1f, 3f, 0f), null, 0, MushroomSoundRouter.CutVolume);
            });
            _player.ReceivedWithAnyArgs(2).Play(default, default);
            _player.DidNotReceiveWithAnyArgs().Stop(default);
        }

        // Decision K: every cap a lance cuts banks its flight a charge, and each is heard in its cut. The stroke
        // climbs a whole tone with each cap the flight has cut; the Snipe's own cue rings once a flight, on the
        // first charge, a whole tone above the pitch an arming plays it at, and not again for the caps after
        // it: a lance through a wall of three would otherwise ring it three times in as many frames.
        [Test]
        public void TheCapsAFlightCuts_ClimbTheStroke_AndTheSnipesCueRingsOnceForTheFirstCharge()
        {
            for (var charge = 1; charge <= 3; charge++)
            {
                _cut.Handle(new MushroomCutMessage(
                    new Vector2Int(charge, 4), new Vector2(charge, 2f), Vector2.up, MushroomCutCause.Lance, charge));
            }

            Received.InOrder(() =>
            {
                _player.Play(MushroomSoundRouter.CutSound, new Vector3(1f, 2f, 0f), null, 0, MushroomSoundRouter.CutVolume);
                _player.Play(MushroomSoundRouter.BankSound, null, null, 2, 1f);
                _player.Play(MushroomSoundRouter.CutSound, new Vector3(2f, 2f, 0f), null, 2, MushroomSoundRouter.CutVolume);
                _player.Play(MushroomSoundRouter.CutSound, new Vector3(3f, 2f, 0f), null, 4, MushroomSoundRouter.CutVolume);
            });
            _player.ReceivedWithAnyArgs(4).Play(default, default);
            Assert.AreEqual(GameSoundId.ItemSnipe, MushroomSoundRouter.BankSound, "the cue a banked charge always rang");
        }

        [Test]
        public void TheStrokesClimb_StopsAtItsTopNote()
        {
            _cut.Handle(new MushroomCutMessage(
                new Vector2Int(2, 4), new Vector2(1f, 2f), Vector2.up, MushroomCutCause.Lance, 12));

            _player.Received(1).Play(
                MushroomSoundRouter.CutSound, new Vector3(1f, 2f, 0f), null, MushroomSoundRouter.MaxCutSteps * 2,
                MushroomSoundRouter.CutVolume);
            _player.ReceivedWithAnyArgs(1).Play(default, default);
        }

        // A cap a bomb or a nuke blew off is heard in the blast: no stroke, however many go at once. A lance's
        // cut in the same frame still plays its own.
        [Test]
        public void ACapBlownOffByABlast_PlaysNothing_AndALancesCutBesideItStillPlays()
        {
            _cut.Handle(new MushroomCutMessage(
                new Vector2Int(2, 4), new Vector2(1.5f, 2.5f), Vector2.up, MushroomCutCause.Blast));
            _cut.Handle(new MushroomCutMessage(
                new Vector2Int(3, 4), new Vector2(2.2f, 2.5f), Vector2.right, MushroomCutCause.Blast));

            _player.DidNotReceiveWithAnyArgs().Play(default, default);

            _cut.Handle(new MushroomCutMessage(
                new Vector2Int(3, 2), new Vector2(-1f, 3f), Vector2.up, MushroomCutCause.Lance));

            _player.Received(1).Play(
                MushroomSoundRouter.CutSound, new Vector3(-1f, 3f, 0f), null, 0, MushroomSoundRouter.CutVolume);
            _player.ReceivedWithAnyArgs(1).Play(default, default);
        }

        private void Bounce(int count)
        {
            _bounced.Handle(new MushroomBouncedMessage(
                Substitute.For<IProjectileModel>(), new Vector2Int(2, 4), 0, Point, Vector2.up, 9.87f, count,
                MushroomReward.None));
        }

        private static ISubscriber<T> CaptureSubscriber<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(Arg.Do(capture), Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}

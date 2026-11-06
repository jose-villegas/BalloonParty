using System;
using System.Collections.Generic;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Flight;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class CombatSoundRouterTests
    {
        private ISoundPlayer _player;
        private ProjectileModel _ball;
        private ProjectileRegistry _registry;
        private IMessageHandler<ActorHitMessage> _hitHandler;
        private IMessageHandler<ProjectileFiredMessage> _firedHandler;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;
        private IMessageHandler<ThrowerReadyMessage> _readyHandler;
        private IMessageHandler<ProjectileCruiseStartedMessage> _cruiseStartedHandler;
        private IMessageHandler<ProjectileCruiseEndedMessage> _cruiseEndedHandler;
        private IMessageHandler<ShieldGainedMessage> _shieldGainedHandler;
        private IMessageHandler<ShieldLostMessage> _shieldLostHandler;
        private IMessageHandler<WallHitMessage> _wallHitHandler;
        private IMessageHandler<SpeedTapMintedMessage> _speedTapHandler;
        private IMessageHandler<ProjectileDestroyedMessage> _destroyedHandler;
        private IMessageHandler<StreakChangedMessage> _streakHandler;

        private IMessageHandler<PierceDischargedMessage> _pierceHandler;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();
            _ball = new ProjectileModel();
            _registry = new ProjectileRegistry();

            var hitFanout = new Fanout<ActorHitMessage>();
            var firedFanout = new Fanout<ProjectileFiredMessage>();
            var loadedFanout = new Fanout<ProjectileLoadedMessage>();
            var destroyedFanout = new Fanout<ProjectileDestroyedMessage>();
            var wallHitFanout = new Fanout<WallHitMessage>();
            var pierceFanout = new Fanout<PierceDischargedMessage>();

            var hitSubscriber = CaptureSubscriber<ActorHitMessage>(hitFanout.Add);
            var firedSubscriber = CaptureSubscriber<ProjectileFiredMessage>(firedFanout.Add);
            var loadedSubscriber = CaptureSubscriber<ProjectileLoadedMessage>(loadedFanout.Add);
            var readySubscriber = CaptureSubscriber<ThrowerReadyMessage>(h => _readyHandler = h);
            var cruiseStartedSubscriber = CaptureSubscriber<ProjectileCruiseStartedMessage>(h => _cruiseStartedHandler = h);
            var cruiseEndedSubscriber = CaptureSubscriber<ProjectileCruiseEndedMessage>(h => _cruiseEndedHandler = h);
            var doomedSubscriber = CaptureSubscriber<ProjectileDoomedStartedMessage>(_ => { });
            var destroyedSubscriber = CaptureSubscriber<ProjectileDestroyedMessage>(destroyedFanout.Add);
            var pierceSubscriber = CaptureSubscriber<PierceDischargedMessage>(pierceFanout.Add);
            var shieldGainedSubscriber = CaptureSubscriber<ShieldGainedMessage>(h => _shieldGainedHandler = h);
            var shieldLostSubscriber = CaptureSubscriber<ShieldLostMessage>(h => _shieldLostHandler = h);
            var wallHitSubscriber = CaptureSubscriber<WallHitMessage>(wallHitFanout.Add);
            var speedTapSubscriber = CaptureSubscriber<SpeedTapMintedMessage>(h => _speedTapHandler = h);
            var streakChangedSubscriber = CaptureSubscriber<StreakChangedMessage>(h => _streakHandler = h);

            var flightConfig = Substitute.For<IProjectileFlightConfig>();
            flightConfig.ShieldToneThreshold.Returns(5);

            var activePierce = Substitute.For<IActiveProjectilePierce>();
            activePierce.IsPiercing.Returns(new ReactiveProperty<bool>(false));

            var palette = Substitute.For<IGamePalette>();
            palette.ProgressColorNames.Returns(new[] { "Red", "Blue", "Green", "Yellow", "Purple" });

            // Real, not a stub: the pitch ramps are now a function of what the flight actually
            // recorded, so a canned IFlightStats would assert against the fake instead of the ramp.
            // Started first so it counts a wall hit or discharge before the router reads the total —
            // the same order registration guarantees in the game.
            var flightStats = new FlightStatsService(
                _registry, loadedSubscriber, firedSubscriber, destroyedSubscriber);
            flightStats.Start();

            var router = new CombatSoundRouter(
                _player, hitSubscriber, firedSubscriber, loadedSubscriber, readySubscriber,
                cruiseStartedSubscriber, cruiseEndedSubscriber, doomedSubscriber, destroyedSubscriber,
                pierceSubscriber, shieldGainedSubscriber, shieldLostSubscriber, wallHitSubscriber,
                speedTapSubscriber, streakChangedSubscriber, flightConfig, activePierce, flightStats, _registry, palette);
            router.Start();

            // Each of these mirrors its real publisher, which records into flight stats immediately
            // before publishing — so the router always reads a total that includes the event it is
            // reacting to, with no dependence on subscription order.
            _hitHandler = new HitPipelineStub(flightStats, hitFanout, () => _ball);
            _wallHitHandler = new RecordingPublisher<WallHitMessage>(
                m => flightStats.RecordWallHit(m.Projectile), wallHitFanout);
            _pierceHandler = new RecordingPublisher<PierceDischargedMessage>(
                m => flightStats.RecordPierceDischarge(m.Projectile), pierceFanout);
            _firedHandler = firedFanout;
            _loadedHandler = loadedFanout;
            _destroyedHandler = destroyedFanout;

            // The thrower's ball, loaded and fired as in a real turn; the stats and the per-ball pitch
            // ramps below all key off it.
            LoadAndFire(_ball);
            _player.ClearReceivedCalls();
        }

        [Test]
        public void SpeedTapMinted_PlaysTheLadderCueWithTheTapCountAsItsMelodicStep()
        {
            // Authored as MelodicMode.ScaleWalkUp, so the tap count IS the scale degree — each rung of the
            // speed ladder sounds a degree higher than the last.
            _speedTapHandler.Handle(new SpeedTapMintedMessage(_ball, Vector3.zero, 1));
            _speedTapHandler.Handle(new SpeedTapMintedMessage(_ball, Vector3.zero, 2));

            _player.Received(1).Play(GameSoundId.SpeedTap, Arg.Any<Vector3?>(), 1, 0, 1f);
            _player.Received(1).Play(GameSoundId.SpeedTap, Arg.Any<Vector3?>(), 2, 0, 1f);
        }

        [Test]
        public void OnActorHit_PopOutcome_PlaysBalloonPopAtWorldPosition()
        {
            var position = new Vector3(1f, 2f, 0f);

            _hitHandler.Handle(new ActorHitMessage(null, position, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPop, position);
        }

        [Test]
        public void OnActorHit_ToughBalloonPop_PlaysToughPopId()
        {
            var position = new Vector3(1f, 2f, 0f);
            var tough = Substitute.For<IBalloonModel>();
            tough.TypeName.Returns(BalloonType.Tough);

            _hitHandler.Handle(new ActorHitMessage(tough, position, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopTough, position);
        }

        [Test]
        public void OnActorHit_RainbowBalloonPop_PlaysRainbowPop()
        {
            var rainbow = Substitute.For<IBalloonModel>();
            rainbow.TypeName.Returns(BalloonType.Rainbow);

            _hitHandler.Handle(new ActorHitMessage(rainbow, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopRainbow, Vector3.zero);
        }

        [Test]
        public void OnActorHit_UnbreakablePop_PlaysUnbreakablePop()
        {
            var unbreakable = Substitute.For<IBalloonModel>();
            unbreakable.TypeName.Returns(BalloonType.Unbreakable);

            _hitHandler.Handle(new ActorHitMessage(unbreakable, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopUnbreakable, Vector3.zero);
        }

        [Test]
        public void OnActorHit_UnbreakableDeflect_PlaysUnbreakableDeflect()
        {
            var unbreakable = Substitute.For<IBalloonModel>();
            unbreakable.TypeName.Returns(BalloonType.Unbreakable);

            _hitHandler.Handle(new ActorHitMessage(unbreakable, Vector3.zero, Vector3.zero, HitOutcome.Deflect));

            _player.Received(1).Play(GameSoundId.BalloonDeflectUnbreakable, Vector3.zero);
        }

        [Test]
        public void OnActorHit_SilverBalloonPop_PlaysSilverPop()
        {
            var silver = Substitute.For<IBalloonModel>();
            silver.TypeName.Returns(BalloonType.SimpleSilver);

            _hitHandler.Handle(new ActorHitMessage(silver, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopSilver, Vector3.zero);
        }

        [Test]
        public void OnActorHit_GoldBalloonPop_PlaysGoldPop()
        {
            var gold = Substitute.For<IBalloonModel>();
            gold.TypeName.Returns(BalloonType.SimpleGold);

            _hitHandler.Handle(new ActorHitMessage(gold, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopGold, Vector3.zero);
        }

        [Test]
        public void OnActorHit_UnmappedBalloonType_FallsBackToBalloonPop()
        {
            var cluster = Substitute.For<IBalloonModel>();
            cluster.TypeName.Returns(BalloonType.BubbleCluster);

            _hitHandler.Handle(new ActorHitMessage(cluster, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero);
        }

        // A type with no dedicated pop id has no voice of its own to climb, so it never ramps —
        // even though flight stats count it like every other type. One pop cannot tell the
        // difference; two can.
        [Test]
        public void OnActorHit_UnmappedBalloonTypePoppedTwice_StaysAtTheRootBothTimes()
        {
            var cluster = Substitute.For<IBalloonModel>();
            cluster.TypeName.Returns(BalloonType.BubbleCluster);

            _hitHandler.Handle(new ActorHitMessage(cluster, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(cluster, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(2).Play(GameSoundId.BalloonPop, Vector3.zero, null, 0, 1f);
        }

        [Test]
        public void OnActorHit_DeflectOutcome_PlaysBalloonDeflect()
        {
            _hitHandler.Handle(new ActorHitMessage(null, Vector3.zero, Vector3.zero, HitOutcome.Deflect));

            _player.Received(1).Play(GameSoundId.BalloonDeflect, Vector3.zero);
            _player.DidNotReceive().Play(GameSoundId.BalloonPop, Arg.Any<Vector3?>());
        }

        [Test]
        public void OnActorHit_AbsorbOutcome_PlaysBalloonResist()
        {
            _hitHandler.Handle(new ActorHitMessage(null, Vector3.zero, Vector3.zero, HitOutcome.Absorb));

            _player.Received(1).Play(GameSoundId.BalloonResist, Vector3.zero);
        }

        [Test]
        public void OnActorHit_PassThroughOutcome_PlaysBalloonResist()
        {
            _hitHandler.Handle(new ActorHitMessage(null, Vector3.zero, Vector3.zero, HitOutcome.PassThrough));

            _player.Received(1).Play(GameSoundId.BalloonResist, Vector3.zero);
        }

        [Test]
        public void OnActorHit_PopCombinedWithDeflect_ResolvesToPop()
        {
            // Pop takes precedence over Deflect when both flags are set on the same outcome —
            // the branch order in OnActorHit IS the precedence contract.
            _hitHandler.Handle(new ActorHitMessage(
                null, Vector3.zero, Vector3.zero, HitOutcome.Pop | HitOutcome.Deflect));

            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero);
            _player.DidNotReceive().Play(GameSoundId.BalloonDeflect, Arg.Any<Vector3?>());
        }

        [Test]
        public void OnProjectileDestroyed_PlaysProjectileDeath()
        {
            _destroyedHandler.Handle(new ProjectileDestroyedMessage(_ball));

            _player.Received(1).Play(GameSoundId.ProjectileDeath, null);
        }

        [Test]
        public void OnFired_ForwardsShotFiredAtWorldPosition()
        {
            var position = new Vector3(3f, 4f, 0f);

            _firedHandler.Handle(new ProjectileFiredMessage(_ball, position, Vector3.right));

            _player.Received(1).Play(GameSoundId.ShotFired, position);
        }

        [Test]
        public void ThrowerReady_ForwardsShotReloadWithNullPosition()
        {
            _readyHandler.Handle(new ThrowerReadyMessage(new ProjectileModel()));

            _player.Received(1).Play(GameSoundId.ShotReload, null);
        }

        [Test]
        public void OnLoaded_PlaysNoReloadCue_TheBallIsNotReadyYet()
        {
            _loadedHandler.Handle(new ProjectileLoadedMessage(new ProjectileModel()));

            _player.DidNotReceive().Play(
                GameSoundId.ShotReload, Arg.Any<Vector3?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<float>());
        }

        [Test]
        public void OnCruiseStarted_ThenEnded_StopsTheStoredHandle()
        {
            var handle = new SoundHandle(7, 1u);
            _player.Play(GameSoundId.CruiseLoopStart, Arg.Any<Vector3?>()).Returns(handle);

            _cruiseStartedHandler.Handle(new ProjectileCruiseStartedMessage(_ball, Vector3.zero, Vector3.right, 1));
            _cruiseEndedHandler.Handle(new ProjectileCruiseEndedMessage(_ball, Vector3.zero));

            _player.Received(1).Stop(handle);
        }

        [Test]
        public void OnShieldLost_PlaysPlainWithoutAMelodicOverride()
        {
            var position = new Vector3(1f, 2f, 0f);

            _shieldLostHandler.Handle(new ShieldLostMessage(_ball, position));

            // The melodic descent lives on WallHit now, so ShieldLost just plays (no streak override).
            _player.Received(1).Play(GameSoundId.ShieldLost, position);
        }

        [Test]
        public void OnShieldGained_PlaysPlain()
        {
            _shieldGainedHandler.Handle(new ShieldGainedMessage(_ball, Vector2Int.zero));

            _player.Received(1).Play(GameSoundId.ShieldGained, null);
        }

        [Test]
        public void OnWallHit_BelowThreshold_StepsDownByShieldsBelowThreshold()
        {
            // Threshold is 5: depth = threshold - shieldsRemaining. 4 left = one step down, 2 left = three.
            // The bounce ramp is a second, independent descent layered on top — one semitone per wall
            // hit of the flight — so the second hit also carries offset -1.
            var position = new Vector3(3f, -1f, 0f);

            _firedHandler.Handle(new ProjectileFiredMessage(_ball, position, Vector3.up));
            _wallHitHandler.Handle(new WallHitMessage(_ball, position, 4));
            _wallHitHandler.Handle(new WallHitMessage(_ball, position, 2));

            _player.Received(1).Play(GameSoundId.WallHit, position, 1);
            _player.Received(1).Play(GameSoundId.WallHit, position, 3, -1);
        }

        [Test]
        public void OnWallHit_AtOrAboveThreshold_StaysAtRoot()
        {
            // At or above 5 shields the shields depth stays on the root — that descent hasn't started.
            // The bounce ramp is independent of shields and does descend, so only the first hit of the
            // flight sounds at the root.
            var position = new Vector3(3f, -1f, 0f);

            _firedHandler.Handle(new ProjectileFiredMessage(_ball, position, Vector3.up));
            _wallHitHandler.Handle(new WallHitMessage(_ball, position, 5));
            _wallHitHandler.Handle(new WallHitMessage(_ball, position, 9));

            _player.Received(1).Play(GameSoundId.WallHit, position, 0);
            _player.Received(1).Play(GameSoundId.WallHit, position, 0, -1);
        }

        // --- Pitch-ramp per-flight counters ---

        [Test]
        public void OnActorHit_RainbowPop_PitchRampsAcrossConsecutivePops()
        {
            var rainbow = Substitute.For<IBalloonModel>();
            rainbow.TypeName.Returns(BalloonType.Rainbow);

            _hitHandler.Handle(new ActorHitMessage(rainbow, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(rainbow, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(rainbow, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopRainbow, Vector3.zero, null, 0, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPopRainbow, Vector3.zero, null, 2, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPopRainbow, Vector3.zero, null, 4, 1f);
        }

        [Test]
        public void OnActorHit_ToughPop_PitchRampsAcrossConsecutivePops()
        {
            var tough = Substitute.For<IBalloonModel>();
            tough.TypeName.Returns(BalloonType.Tough);

            _hitHandler.Handle(new ActorHitMessage(tough, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(tough, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopTough, Vector3.zero, null, 0, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPopTough, Vector3.zero, null, 2, 1f);
        }

        [Test]
        public void OnActorHit_UnbreakablePop_PitchRampsAcrossConsecutivePops()
        {
            var unbreakable = Substitute.For<IBalloonModel>();
            unbreakable.TypeName.Returns(BalloonType.Unbreakable);

            _hitHandler.Handle(new ActorHitMessage(unbreakable, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(unbreakable, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopUnbreakable, Vector3.zero, null, 0, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPopUnbreakable, Vector3.zero, null, 2, 1f);
        }

        [Test]
        public void OnActorHit_PopCounters_AreIndependentPerType()
        {
            var rainbow = Substitute.For<IBalloonModel>();
            rainbow.TypeName.Returns(BalloonType.Rainbow);
            var tough = Substitute.For<IBalloonModel>();
            tough.TypeName.Returns(BalloonType.Tough);

            // Rainbow pop, then Tough pop — each starts at offset 0.
            _hitHandler.Handle(new ActorHitMessage(rainbow, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(tough, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            _player.Received(1).Play(GameSoundId.BalloonPopRainbow, Vector3.zero, null, 0, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPopTough, Vector3.zero, null, 0, 1f);
        }

        [Test]
        public void OnLoaded_ResetsAllPopPitchCounters()
        {
            var rainbow = Substitute.For<IBalloonModel>();
            rainbow.TypeName.Returns(BalloonType.Rainbow);
            var tough = Substitute.For<IBalloonModel>();
            tough.TypeName.Returns(BalloonType.Tough);

            _hitHandler.Handle(new ActorHitMessage(rainbow, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(rainbow, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(tough, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            // New flight — all counters reset.
            NextBall();

            _hitHandler.Handle(new ActorHitMessage(rainbow, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(tough, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            // After reset, the third rainbow call and the second tough call should be at offset 0.
            _player.Received().Play(GameSoundId.BalloonPopRainbow, Vector3.zero, null, 0, 1f);
            _player.Received().Play(GameSoundId.BalloonPopTough, Vector3.zero, null, 0, 1f);
        }

        [Test]
        public void OnActorHit_SilverAndGoldPops_PitchRampsAcrossConsecutivePops()
        {
            var silver = Substitute.For<IBalloonModel>();
            silver.TypeName.Returns(BalloonType.SimpleSilver);
            var gold = Substitute.For<IBalloonModel>();
            gold.TypeName.Returns(BalloonType.SimpleGold);

            _hitHandler.Handle(new ActorHitMessage(silver, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(silver, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(gold, Vector3.zero, Vector3.zero, HitOutcome.Pop));

            // Silver ramps: first at 0, second at +2 semitones (whole tone).
            _player.Received(1).Play(GameSoundId.BalloonPopSilver, Vector3.zero, null, 0, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPopSilver, Vector3.zero, null,
                MusicalPitchExtensions.WholeToneSemitones, 1f);
            // Gold first pop at 0.
            _player.Received(1).Play(GameSoundId.BalloonPopGold, Vector3.zero, null, 0, 1f);
        }

        [Test]
        public void OnPierceDischarged_PitchRampsAcrossConsecutiveDischarges()
        {
            _pierceHandler.Handle(new PierceDischargedMessage(_ball, Vector3.zero, 2, false, Vector3.zero, Vector3.zero));
            _pierceHandler.Handle(new PierceDischargedMessage(_ball, Vector3.zero, 3, false, Vector3.zero, Vector3.zero));
            _pierceHandler.Handle(new PierceDischargedMessage(_ball, Vector3.zero, 1, false, Vector3.zero, Vector3.zero));

            // First discharge at offset 0, second at +2 (whole tone), third at +4.
            _player.Received(1).Play(GameSoundId.PierceDischarge, Vector3.zero, null, 0, 1f);
            _player.Received(1).Play(GameSoundId.PierceDischarge, Vector3.zero, null,
                MusicalPitchExtensions.WholeToneSemitones, 1f);
            _player.Received(1).Play(GameSoundId.PierceDischarge, Vector3.zero, null,
                MusicalPitchExtensions.WholeToneSemitones * 2, 1f);
        }

        [Test]
        public void OnLoaded_ResetsPierceDischargeCounter()
        {
            _pierceHandler.Handle(new PierceDischargedMessage(_ball, Vector3.zero, 2, false, Vector3.zero, Vector3.zero));
            _pierceHandler.Handle(new PierceDischargedMessage(_ball, Vector3.zero, 3, false, Vector3.zero, Vector3.zero));

            // New flight — counter resets.
            NextBall();

            _pierceHandler.Handle(new PierceDischargedMessage(_ball, Vector3.zero, 1, false, Vector3.zero, Vector3.zero));

            // After reset, discharge should be at offset 0 again.
            _player.Received().Play(GameSoundId.PierceDischarge, Vector3.zero, null, 0, 1f);
        }

        [Test]
        public void OnActorHit_TwoBallsInFlight_PopRampsAreIndependentPerBall()
        {
            var tough = Substitute.For<IBalloonModel>();
            tough.TypeName.Returns(BalloonType.Tough);
            var other = LaunchSecondBall();

            _hitHandler.Handle(new ActorHitMessage(tough, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(tough, Vector3.zero, Vector3.zero, HitOutcome.Pop));
            _hitHandler.Handle(new ActorHitMessage(
                tough, Vector3.zero, Vector3.zero, HitOutcome.Pop, new DamageContext(1, DamageFlags.Normal, "", other)));

            // The second ball's first Tough pop sounds at the root, not as the third rung of the first ball's ramp.
            _player.Received(2).Play(GameSoundId.BalloonPopTough, Vector3.zero, null, 0, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPopTough, Vector3.zero, null, 2, 1f);
        }

        [Test]
        public void OnWallHit_TwoBallsInFlight_BounceDescentIsPerBall()
        {
            var other = LaunchSecondBall();
            var position = new Vector3(3f, -1f, 0f);

            _wallHitHandler.Handle(new WallHitMessage(_ball, position, 9));
            _wallHitHandler.Handle(new WallHitMessage(_ball, position, 9));
            _wallHitHandler.Handle(new WallHitMessage(other, position, 9));

            _player.Received(2).Play(GameSoundId.WallHit, position, 0);
            _player.Received(1).Play(GameSoundId.WallHit, position, 0, -1);
        }

        [Test]
        public void OnCruiseEnded_TwoCruisingBalls_OnlyStopsThatBallsLoop()
        {
            var other = LaunchSecondBall();
            var handleA = new SoundHandle(7, 1u);
            var handleB = new SoundHandle(8, 1u);
            _player.Play(GameSoundId.CruiseLoopStart, Arg.Any<Vector3?>()).Returns(handleA, handleB);
            _cruiseStartedHandler.Handle(new ProjectileCruiseStartedMessage(_ball, Vector3.zero, Vector3.right, 1));
            _cruiseStartedHandler.Handle(new ProjectileCruiseStartedMessage(other, Vector3.zero, Vector3.right, 1));

            _cruiseEndedHandler.Handle(new ProjectileCruiseEndedMessage(_ball, Vector3.zero));

            _player.Received(1).Stop(handleA);
            _player.DidNotReceive().Stop(handleB);
        }

        [Test]
        public void OnProjectileDestroyed_CruisingBall_StopsItsOwnLoopOnly()
        {
            var other = LaunchSecondBall();
            var handleA = new SoundHandle(7, 1u);
            var handleB = new SoundHandle(8, 1u);
            _player.Play(GameSoundId.CruiseLoopStart, Arg.Any<Vector3?>()).Returns(handleA, handleB);
            _cruiseStartedHandler.Handle(new ProjectileCruiseStartedMessage(_ball, Vector3.zero, Vector3.right, 1));
            _cruiseStartedHandler.Handle(new ProjectileCruiseStartedMessage(other, Vector3.zero, Vector3.right, 1));

            _destroyedHandler.Handle(new ProjectileDestroyedMessage(_ball));

            _player.Received(1).Stop(handleA);
            _player.DidNotReceive().Stop(handleB);
        }

        [Test]
        public void OnBallRetired_WithoutCruiseEndedOrDestroyed_StopsItsLoop()
        {
            var handle = new SoundHandle(7, 1u);
            _player.Play(GameSoundId.CruiseLoopStart, Arg.Any<Vector3?>()).Returns(handle);
            _cruiseStartedHandler.Handle(new ProjectileCruiseStartedMessage(_ball, Vector3.zero, Vector3.right, 1));

            _registry.Retire(_ball);

            _player.Received(1).Stop(handle);
        }

        // --- Colour-pop ramps per ball ---

        [Test]
        public void OnActorHit_ColorPops_TwoBallsClimbTheirOwnRamps()
        {
            var other = LaunchSecondBall();
            var root = BlueRoot();

            Pop("Blue", _ball);
            Pop("Blue", _ball);
            Pop("Blue", other);

            // The second ball's first Blue pop sounds at the colour root, not as the third rung of the first ball's ramp.
            _player.Received(2).Play(GameSoundId.BalloonPop, Vector3.zero, null, root, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root + 2, 1f);
        }

        [Test]
        public void OnStreakChanged_OneBallsBreak_ClearsOnlyThatBallsRamp()
        {
            var other = LaunchSecondBall();
            var root = BlueRoot();
            Pop("Blue", _ball);
            Pop("Blue", _ball);
            Pop("Blue", other);
            Pop("Blue", other);
            _player.ClearReceivedCalls();

            _streakHandler.Handle(new StreakChangedMessage(_ball, null, 0));
            Pop("Blue", _ball);
            Pop("Blue", other);

            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root + 4, 1f);
        }

        [Test]
        public void OnStreakChanged_ShortOfABreak_LeavesTheRamp()
        {
            var root = BlueRoot();
            Pop("Blue", _ball);
            _player.ClearReceivedCalls();

            _streakHandler.Handle(new StreakChangedMessage(_ball, "Blue", 2));
            Pop("Blue", _ball);

            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root + 2, 1f);
        }

        [Test]
        public void OnStreakChanged_BallLessBreak_ClearsOnlyTheSharedRamp()
        {
            var untracked = new ProjectileModel();
            var root = BlueRoot();
            Pop("Blue", untracked);
            Pop("Blue", untracked);
            Pop("Blue", _ball);
            _player.ClearReceivedCalls();

            // The shared streak resets on every load (every shot) — that must not undo a flying ball's ramp.
            _streakHandler.Handle(new StreakChangedMessage(null, null, 0));
            Pop("Blue", untracked);
            Pop("Blue", _ball);

            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root + 2, 1f);
        }

        [Test]
        public void OnLoaded_ClearsOnlyTheSharedRamp_NotAFlyingBallsRamp()
        {
            var untracked = new ProjectileModel();
            var root = BlueRoot();
            Pop("Blue", untracked);
            Pop("Blue", _ball);
            _player.ClearReceivedCalls();

            LoadAndFire(new ProjectileModel());
            _player.ClearReceivedCalls();
            Pop("Blue", untracked);
            Pop("Blue", _ball);

            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root, 1f);
            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root + 2, 1f);
        }

        [Test]
        public void OnBallRetired_ItsRampIsDropped_SoTheNextBallStartsAtTheRoot()
        {
            var root = BlueRoot();
            var first = _ball;
            Pop("Blue", first);
            Pop("Blue", first);

            _registry.Land(first);
            _registry.Retire(first);
            var second = new ProjectileModel();
            LoadAndFire(second);
            _player.ClearReceivedCalls();
            Pop("Blue", second);

            // A pooled map handed to the next ball must come back empty.
            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root, 1f);
        }

        [Test]
        public void OnActorHit_PopCreditedToARetiredBall_UsesTheSharedRampWithoutCreatingAnEntry()
        {
            var root = BlueRoot();
            var first = _ball;
            _registry.Land(first);
            _registry.Retire(first);

            Pop("Blue", first);
            Pop("Blue", first);
            _registry.Load(first, null);
            _registry.Launch(first);
            _player.ClearReceivedCalls();
            Pop("Blue", first);

            // Had the late pops created an entry for the retired ball, re-registering it would resume at +4.
            _player.Received(1).Play(GameSoundId.BalloonPop, Vector3.zero, null, root, 1f);
        }


        // Retires the ball in play and loads a fresh one, as the thrower does between turns.
        private void NextBall()
        {
            _destroyedHandler.Handle(new ProjectileDestroyedMessage(_ball));
            _registry.Retire(_ball);
            _ball = new ProjectileModel();
            LoadAndFire(_ball);
        }

        private void LoadAndFire(IProjectileModel ball)
        {
            _registry.Load(ball, null);
            _loadedHandler.Handle(new ProjectileLoadedMessage(ball));
            _registry.Launch(ball);
            _firedHandler.Handle(new ProjectileFiredMessage(ball, Vector3.zero, Vector3.up));
        }

        // A second ball loaded and fired while the first is still airborne.
        private IProjectileModel LaunchSecondBall()
        {
            var other = new ProjectileModel();
            LoadAndFire(other);
            _player.ClearReceivedCalls();
            return other;
        }

        private int BlueRoot()
        {
            return MusicalPitchExtensions.ColorRootOffset(
                new[] { "Red", "Blue", "Green", "Yellow", "Purple" }, "Blue");
        }

        // A plain colour pop credited to the given ball, as the hit pipeline would stamp it.
        private void Pop(string color, IProjectileModel ball)
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = color;
            _hitHandler.Handle(new ActorHitMessage(
                balloon, Vector3.zero, Vector3.zero, HitOutcome.Pop,
                new DamageContext(1, DamageFlags.Normal, "", ball)));
        }

        private static ISubscriber<T> CaptureSubscriber<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(
                    Arg.Do(capture),
                    Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }

        // Two systems now subscribe to the same messages — FlightStatsService owns the per-flight
        // counts, CombatSoundRouter reads them for pitch — so a capture that kept only the last
        // handler would silently drop one. Delivers to both in subscription order, as the bus does.
        private sealed class Fanout<T> : IMessageHandler<T>
        {
            private readonly List<IMessageHandler<T>> _handlers = new();

            public void Add(IMessageHandler<T> handler)
            {
                _handlers.Add(handler);
            }

            public void Handle(T message)
            {
                foreach (var handler in _handlers)
                {
                    handler.Handle(message);
                }
            }
        }

        // Stands in for a publisher that records before it publishes.
        private sealed class RecordingPublisher<T> : IMessageHandler<T>
        {
            private readonly Action<T> _record;
            private readonly IMessageHandler<T> _subscribers;

            public RecordingPublisher(Action<T> record, IMessageHandler<T> subscribers)
            {
                _record = record;
                _subscribers = subscribers;
            }

            public void Handle(T message)
            {
                _record(message);
                _subscribers.Handle(message);
            }
        }

        // Stands in for HitPipeline, which is what wires these together in the game: it records the
        // hit into flight stats and only then publishes, so every subscriber reads a count that
        // already includes the hit it is reacting to. Without this the router would see zero pops.
        private sealed class HitPipelineStub : IMessageHandler<ActorHitMessage>
        {
            private readonly IFlightStatsWriter _flightStats;
            private readonly IMessageHandler<ActorHitMessage> _subscribers;
            private readonly Func<IProjectileModel> _currentBall;

            public HitPipelineStub(
                IFlightStatsWriter flightStats, IMessageHandler<ActorHitMessage> subscribers,
                Func<IProjectileModel> currentBall)
            {
                _flightStats = flightStats;
                _subscribers = subscribers;
                _currentBall = currentBall;
            }

            // A hit that names no ball is attributed to the ball in play, as the shot that caused it would be.
            public void Handle(ActorHitMessage message)
            {
                if (message.Context.Projectile == null)
                {
                    message = new ActorHitMessage(
                        message.Actor, message.WorldPosition, message.ProjectileDirection, message.Outcome,
                        new DamageContext(1, DamageFlags.Normal, "", _currentBall()));
                }

                _flightStats.Record(message);
                _subscribers.Handle(message);
            }
        }
    }
}

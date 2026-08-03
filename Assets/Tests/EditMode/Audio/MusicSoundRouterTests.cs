using System;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Game.Danger;
using BalloonParty.Game.Flight;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class MusicSoundRouterTests
    {
        private ISoundPlayer _player;
        private ReactiveProperty<NavigationState> _state;
        private ReactiveProperty<float> _dangerLevel;
        private ITimeOfDayNight _timeOfDayNight;
        private MusicSoundRouter _router;
        private SoundHandle _launchHandle;
        private SoundHandle _dayHandle;
        private SoundHandle _nightHandle;

        // MessagePipe subscriber-captured handlers (README pattern).
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;
        private IMessageHandler<ProjectileDestroyedMessage> _destroyedHandler;
        private IMessageHandler<ProjectileFiredMessage> _firedHandler;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();

            _launchHandle = new SoundHandle(0, 1u);
            _dayHandle = new SoundHandle(1, 1u);
            _nightHandle = new SoundHandle(2, 1u);

            _player.Play(GameSoundId.LaunchMusic, null).Returns(_launchHandle);
            _player.Play(GameSoundId.GameplayLoopDay, null).Returns(_dayHandle);
            _player.Play(GameSoundId.GameplayLoopNight, null).Returns(_nightHandle);

            // Self-healing in Tick checks IsAlive; return true for valid handles by default.
            _player.IsAlive(_dayHandle).Returns(true);
            _player.IsAlive(_nightHandle).Returns(true);
            _player.IsAlive(_launchHandle).Returns(true);

            _state = new ReactiveProperty<NavigationState>(NavigationState.Launch);
            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(_state);

            _dangerLevel = new ReactiveProperty<float>(0f);
            var danger = Substitute.For<IDangerLevel>();
            danger.Level.Returns(_dangerLevel);

            _timeOfDayNight = Substitute.For<ITimeOfDayNight>();
            _timeOfDayNight.IsNight.Returns(false);

            // The router now reads IFlightScope.IsAirborne rather than subscribing to the three
            // projectile messages itself. These relays keep the tests firing the same messages: fired
            // opens the airborne window, loaded and destroyed close it — exactly what
            // FlightStatsService does.
            var flightScope = new FakeFlightScope();
            _loadedHandler = new Relay<ProjectileLoadedMessage>(() => flightScope.Airborne.Value = false);
            _destroyedHandler = new Relay<ProjectileDestroyedMessage>(() => flightScope.Airborne.Value = false);
            _firedHandler = new Relay<ProjectileFiredMessage>(() => flightScope.Airborne.Value = true);

            _router = new MusicSoundRouter(
                _player, navigation, danger, _timeOfDayNight, flightScope);
            _router.Start();
        }

        private void EnterGame()
        {
            _state.Value = NavigationState.Game;
        }

        // -------------------------------------------------------------------
        // Launch music (existing, kept as-is)
        // -------------------------------------------------------------------

        [Test]
        public void StartingAtLaunch_PlaysLaunchMusic()
        {
            _player.Received(1).Play(GameSoundId.LaunchMusic, null);
        }

        [Test]
        public void PressingPlayIntoGame_StopsLaunchMusic()
        {
            EnterGame();

            _player.Received(1).Stop(_launchHandle);
        }

        [Test]
        public void ReturningToLaunch_PlaysLaunchMusicAgain()
        {
            EnterGame();
            _state.Value = NavigationState.Launch;

            _player.Received(2).Play(GameSoundId.LaunchMusic, null);
        }

        [Test]
        public void WhileInLaunch_DoesNotRetrigger()
        {
            // Only Launch is entered (once, in SetUp); no state change means no second play.
            _player.Received(1).Play(GameSoundId.LaunchMusic, null);
        }

        // -------------------------------------------------------------------
        // Gameplay loop start/stop
        // -------------------------------------------------------------------

        [Test]
        public void EnteringGame_PlaysBothGameplayLoops()
        {
            EnterGame();

            _player.Received(1).Play(GameSoundId.GameplayLoopDay, null);
            _player.Received(1).Play(GameSoundId.GameplayLoopNight, null);
        }

        [Test]
        public void ReturningToLaunch_StopsBothGameplayLoops()
        {
            EnterGame();
            _state.Value = NavigationState.Launch;

            _player.Received(1).Stop(_dayHandle);
            _player.Received(1).Stop(_nightHandle);
        }

        // -------------------------------------------------------------------
        // Day/night crossfade — conditional branching: which loop gets factor
        // 1 vs 0 depends on IsNight. Wrong factor = wrong loop audible.
        // -------------------------------------------------------------------

        [Test]
        public void EnteringGame_DuringDay_DayLoopFull_NightLoopDucked()
        {
            _timeOfDayNight.IsNight.Returns(false);
            EnterGame();

            _player.Received().SetVolumeFactor(_dayHandle, 1f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        [Test]
        public void EnteringGame_DuringNight_NightLoopFull_DayLoopDucked()
        {
            _timeOfDayNight.IsNight.Returns(true);
            EnterGame();

            _player.Received().SetVolumeFactor(_dayHandle, 0f);
            _player.Received().SetVolumeFactor(_nightHandle, 1f);
        }

        [Test]
        public void Tick_IsNightFlips_SwapsVolume()
        {
            _timeOfDayNight.IsNight.Returns(false);
            EnterGame();
            _player.ClearReceivedCalls();

            // Simulate night falling.
            _timeOfDayNight.IsNight.Returns(true);
            _router.Tick();

            _player.Received().SetVolumeFactor(_dayHandle, 0f);
            _player.Received().SetVolumeFactor(_nightHandle, 1f);
        }

        [Test]
        public void Tick_IsNightSame_DoesNotCallSetVolumeFactor()
        {
            _timeOfDayNight.IsNight.Returns(false);
            EnterGame();
            _player.ClearReceivedCalls();

            // IsNight unchanged — Tick should be a no-op.
            _router.Tick();

            _player.DidNotReceive().SetVolumeFactor(Arg.Any<SoundHandle>(), Arg.Any<float>());
        }

        [Test]
        public void Tick_BeforeEnteringGame_DoesNothing()
        {
            // Still in Launch — _isPlaying is false.
            _player.ClearReceivedCalls();
            _router.Tick();

            _player.DidNotReceive().SetVolumeFactor(Arg.Any<SoundHandle>(), Arg.Any<float>());
        }

        // -------------------------------------------------------------------
        // Flight ducking — fired → duck, loaded/destroyed → restore.
        // Conditional side effects: wrong flag flip = audible gap or stuck duck.
        // -------------------------------------------------------------------

        [Test]
        public void ProjectileFired_DucksBothLoops()
        {
            EnterGame();
            _player.ClearReceivedCalls();

            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));

            _player.Received().SetVolumeFactor(_dayHandle, 0f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        [Test]
        public void ProjectileLoaded_AfterFire_RestoresVolume()
        {
            _timeOfDayNight.IsNight.Returns(false);
            EnterGame();
            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _player.ClearReceivedCalls();

            _loadedHandler.Handle(new ProjectileLoadedMessage(null));

            _player.Received().SetVolumeFactor(_dayHandle, 1f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        [Test]
        public void ProjectileDestroyed_AfterFire_RestoresVolume()
        {
            _timeOfDayNight.IsNight.Returns(false);
            EnterGame();
            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _player.ClearReceivedCalls();

            _destroyedHandler.Handle(new ProjectileDestroyedMessage());

            _player.Received().SetVolumeFactor(_dayHandle, 1f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        [Test]
        public void ProjectileFired_DuringNight_NightLoopAlsoDucked()
        {
            _timeOfDayNight.IsNight.Returns(true);
            EnterGame();
            _player.ClearReceivedCalls();

            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));

            // Both loops duck, even though night is the active one.
            _player.Received().SetVolumeFactor(_dayHandle, 0f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        [Test]
        public void FlightEndDuringNight_RestoresNightLoop()
        {
            _timeOfDayNight.IsNight.Returns(true);
            EnterGame();
            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _player.ClearReceivedCalls();

            _loadedHandler.Handle(new ProjectileLoadedMessage(null));

            _player.Received().SetVolumeFactor(_dayHandle, 0f);
            _player.Received().SetVolumeFactor(_nightHandle, 1f);
        }

        // -------------------------------------------------------------------
        // Popup ducking — LevelUp/GameOver nav states duck the gameplay loops.
        // The default branch in OnNavigationChanged handles this.
        // -------------------------------------------------------------------

        [Test]
        public void LevelUpState_DucksBothGameplayLoops()
        {
            EnterGame();
            _player.ClearReceivedCalls();

            _state.Value = NavigationState.LevelUp;

            _player.Received().SetVolumeFactor(_dayHandle, 0f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        [Test]
        public void GameOverState_DucksBothGameplayLoops()
        {
            EnterGame();
            _player.ClearReceivedCalls();

            _state.Value = NavigationState.GameOver;

            _player.Received().SetVolumeFactor(_dayHandle, 0f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        [Test]
        public void ReturningToGame_AfterLevelUp_RestoresVolume()
        {
            _timeOfDayNight.IsNight.Returns(false);
            EnterGame();
            _state.Value = NavigationState.LevelUp;
            _player.ClearReceivedCalls();

            _state.Value = NavigationState.Game;

            _player.Received().SetVolumeFactor(_dayHandle, 1f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        // -------------------------------------------------------------------
        // Danger pitch modulation — math formula: Pow(2, -danger * 6 / 12).
        // Wrong sign, constant, or formula = broken pitch.
        // -------------------------------------------------------------------

        [Test]
        public void DangerZero_PitchIsOne()
        {
            EnterGame();

            // danger = 0 → semitones = 0 → pitch = 2^0 = 1
            _player.Received().SetPitch(_dayHandle, 1f, 0.4f);
            _player.Received().SetPitch(_nightHandle, 1f, 0.4f);
        }

        [Test]
        public void DangerMax_PitchIsTritoneDown()
        {
            EnterGame();
            _player.ClearReceivedCalls();

            _dangerLevel.Value = 1f;

            // danger = 1 → 6 semitones → pitch = 2^(-6/12) = 2^(-0.5) ≈ 0.7071
            var expected = Mathf.Pow(2f, -0.5f);
            _player.Received().SetPitch(_dayHandle, expected, 0.4f);
            _player.Received().SetPitch(_nightHandle, expected, 0.4f);
        }

        [Test]
        public void DangerHalf_PitchIsThreeSemitonesDown()
        {
            EnterGame();
            _player.ClearReceivedCalls();

            _dangerLevel.Value = 0.5f;

            // danger = 0.5 → 3 semitones → pitch = 2^(-3/12) = 2^(-0.25)
            var expected = Mathf.Pow(2f, -3f / 12f);
            _player.Received().SetPitch(_dayHandle, expected, 0.4f);
            _player.Received().SetPitch(_nightHandle, expected, 0.4f);
        }

        [Test]
        public void DangerChange_BeforeEnteringGame_DoesNotCallSetPitch()
        {
            // Still in Launch — no gameplay handles are valid.
            _player.ClearReceivedCalls();
            _dangerLevel.Value = 0.5f;

            _player.DidNotReceive().SetPitch(Arg.Any<SoundHandle>(), Arg.Any<float>(), Arg.Any<float>());
        }

        // -------------------------------------------------------------------
        // Combined edge case — flight duck + night flip interaction.
        // -------------------------------------------------------------------

        [Test]
        public void IsNightFlipsDuringFlight_BothLoopsStayDucked()
        {
            _timeOfDayNight.IsNight.Returns(false);
            EnterGame();
            _firedHandler.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _player.ClearReceivedCalls();

            // Night falls while in flight — should still be ducked.
            _timeOfDayNight.IsNight.Returns(true);
            _router.Tick();

            _player.Received().SetVolumeFactor(_dayHandle, 0f);
            _player.Received().SetVolumeFactor(_nightHandle, 0f);
        }

        // -------------------------------------------------------------------
        // Self-healing — voice-limiter steal detection and recovery.
        // Conditional branching with side effect: wrong guard or missing
        // handle-clear leaves music permanently mute after a steal.
        // -------------------------------------------------------------------

        [Test]
        public void Tick_DayLoopStolen_ReplaysGameplayLoopDay()
        {
            EnterGame();
            _player.ClearReceivedCalls();

            // Simulate the voice limiter stealing the day loop's slot.
            _player.IsAlive(_dayHandle).Returns(false);

            // After re-play, the new handle must be alive for subsequent ticks.
            var revivedDay = new SoundHandle(3, 2u);
            _player.Play(GameSoundId.GameplayLoopDay, null).Returns(revivedDay);
            _player.IsAlive(revivedDay).Returns(true);

            _router.Tick();

            _player.Received(1).Play(GameSoundId.GameplayLoopDay, null);
            // Night was still alive — should not re-play.
            _player.DidNotReceive().Play(GameSoundId.GameplayLoopNight, null);
        }

        [Test]
        public void Tick_NightLoopStolen_ReplaysGameplayLoopNight()
        {
            EnterGame();
            _player.ClearReceivedCalls();

            // Simulate the voice limiter stealing the night loop's slot.
            _player.IsAlive(_nightHandle).Returns(false);

            var revivedNight = new SoundHandle(4, 2u);
            _player.Play(GameSoundId.GameplayLoopNight, null).Returns(revivedNight);
            _player.IsAlive(revivedNight).Returns(true);

            _router.Tick();

            _player.DidNotReceive().Play(GameSoundId.GameplayLoopDay, null);
            _player.Received(1).Play(GameSoundId.GameplayLoopNight, null);
        }

        [Test]
        public void Tick_BothLoopsStolen_ReplaysBothAndReappliesVolumeAndPitch()
        {
            _timeOfDayNight.IsNight.Returns(false);
            _dangerLevel.Value = 0.5f;
            EnterGame();
            _player.ClearReceivedCalls();

            // Both loops killed (e.g. StopAllVoices from a scope reset racing the router).
            _player.IsAlive(_dayHandle).Returns(false);
            _player.IsAlive(_nightHandle).Returns(false);

            var revivedDay = new SoundHandle(5, 2u);
            var revivedNight = new SoundHandle(6, 2u);
            _player.Play(GameSoundId.GameplayLoopDay, null).Returns(revivedDay);
            _player.Play(GameSoundId.GameplayLoopNight, null).Returns(revivedNight);
            _player.IsAlive(revivedDay).Returns(true);
            _player.IsAlive(revivedNight).Returns(true);

            _router.Tick();

            // Both re-played.
            _player.Received(1).Play(GameSoundId.GameplayLoopDay, null);
            _player.Received(1).Play(GameSoundId.GameplayLoopNight, null);

            // Volume re-applied: day is active (night is false), so day = 1, night = 0.
            _player.Received().SetVolumeFactor(revivedDay, 1f);
            _player.Received().SetVolumeFactor(revivedNight, 0f);

            // Pitch re-applied at the current danger level.
            var expectedPitch = Mathf.Pow(2f, -3f / 12f);
            _player.Received().SetPitch(revivedDay, expectedPitch, 0.4f);
            _player.Received().SetPitch(revivedNight, expectedPitch, 0.4f);
        }

        [Test]
        public void Tick_LoopsAlive_DoesNotReplay()
        {
            EnterGame();
            _player.ClearReceivedCalls();

            // Both loops healthy — Tick should not touch Play.
            _router.Tick();

            _player.DidNotReceive().Play(GameSoundId.GameplayLoopDay, null);
            _player.DidNotReceive().Play(GameSoundId.GameplayLoopNight, null);
        }

        private sealed class FakeFlightScope : IFlightScope
        {
            public ReactiveProperty<bool> Airborne { get; } = new();

            public int FlightIndex => 0;

            IReadOnlyReactiveProperty<bool> IFlightScope.IsLoaded => Airborne;

            IReadOnlyReactiveProperty<bool> IFlightScope.IsAirborne => Airborne;
        }

        private sealed class Relay<T> : IMessageHandler<T>
        {
            private readonly Action _onHandle;

            public Relay(Action onHandle)
            {
                _onHandle = onHandle;
            }

            public void Handle(T message)
            {
                _onHandle();
            }
        }
    }
}

using System;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class WindSoundRouterTests
    {
        private ISoundPlayer _player;
        private WindSoundRouter _router;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;
        private ReactiveProperty<bool> _cruising;
        private IProjectileFlightState _flight;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();

            var loadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            loadedSubscriber
                .Subscribe(Arg.Do<IMessageHandler<ProjectileLoadedMessage>>(h => _loadedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            // ProjectileSpeed 10, x3 cap, +2 per tap => full speed at 10*(3-1)/2 = 10 taps.
            var flightConfig = Substitute.For<IProjectileFlightConfig>();
            flightConfig.ProjectileSpeed.Returns(10f);
            flightConfig.MaxSpeedMultiplier.Returns(3f);
            flightConfig.SpeedGainPerTap.Returns(2f);

            _router = new WindSoundRouter(_player, loadedSubscriber, flightConfig);
            _router.Start();

            _cruising = new ReactiveProperty<bool>(false);
            _flight = Substitute.For<IProjectileFlightState>();
            var model = Substitute.For<IProjectileModel>();
            model.IsCruising.Returns(_cruising);
            model.Flight.Returns(_flight);
            _loadedHandler.Handle(new ProjectileLoadedMessage(model));
        }

        [Test]
        public void FirstTapWhileCruising_PlaysWindLoopAtMinFactor()
        {
            _player.Play(GameSoundId.WindLoop, null).Returns(new SoundHandle(2, 1u));
            _cruising.Value = true;
            _flight.TotalCruiseTaps.Returns(1);

            _router.Tick();

            _player.Received(1).Play(GameSoundId.WindLoop, null);
            _player.Received(1).SetVolumeFactor(Arg.Any<SoundHandle>(), 0f);
        }

        [Test]
        public void MoreTaps_RampTheFactorUpToOneAtFullSpeed()
        {
            _player.Play(GameSoundId.WindLoop, null).Returns(new SoundHandle(2, 1u));
            _cruising.Value = true;

            _flight.TotalCruiseTaps.Returns(1);
            _router.Tick();
            _flight.TotalCruiseTaps.Returns(10);
            _router.Tick();

            _player.Received(1).SetVolumeFactor(Arg.Any<SoundHandle>(), 0f);
            _player.Received(1).SetVolumeFactor(Arg.Any<SoundHandle>(), 1f);
        }

        [Test]
        public void CruiseEnding_StopsTheWind()
        {
            var handle = new SoundHandle(2, 1u);
            _player.Play(GameSoundId.WindLoop, null).Returns(handle);
            _cruising.Value = true;
            _flight.TotalCruiseTaps.Returns(3);
            _router.Tick();

            _cruising.Value = false;   // cruise ends (or the shot dies — both clear IsCruising)
            _router.Tick();

            _player.Received(1).Stop(handle);
        }

        [Test]
        public void BeforeAnyTap_NeverPlays()
        {
            _cruising.Value = true;
            _flight.TotalCruiseTaps.Returns(0);

            _router.Tick();

            _player.DidNotReceive().Play(GameSoundId.WindLoop, null);
        }
    }
}

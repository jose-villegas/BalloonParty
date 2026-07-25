using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Shared.GameState;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class MusicSoundRouterTests
    {
        private ISoundPlayer _player;
        private ReactiveProperty<NavigationState> _state;
        private SoundHandle _handle;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();
            _handle = new SoundHandle(1, 1u);
            _player.Play(GameSoundId.LaunchMusic, null).Returns(_handle);

            _state = new ReactiveProperty<NavigationState>(NavigationState.Launch);
            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(_state);

            var router = new MusicSoundRouter(_player, navigation);
            router.Start();
        }

        [Test]
        public void StartingAtLaunch_PlaysLaunchMusic()
        {
            _player.Received(1).Play(GameSoundId.LaunchMusic, null);
        }

        [Test]
        public void PressingPlayIntoGame_StopsTheMusic()
        {
            _state.Value = NavigationState.Game;

            _player.Received(1).Stop(_handle);
        }

        [Test]
        public void ReturningToLaunch_PlaysAgain()
        {
            _state.Value = NavigationState.Game;
            _state.Value = NavigationState.Launch;

            _player.Received(2).Play(GameSoundId.LaunchMusic, null);
        }

        [Test]
        public void WhileInLaunch_DoesNotRetrigger()
        {
            // Only Launch is entered (once, in SetUp); no state change means no second play.
            _player.Received(1).Play(GameSoundId.LaunchMusic, null);
        }
    }
}

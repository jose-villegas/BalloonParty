using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.Routing;
using BalloonParty.Game.Danger;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class DangerSoundRouterTests
    {
        private ISoundPlayer _player;
        private ReactiveProperty<float> _level;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();
            _level = new ReactiveProperty<float>(0f);

            var bank = Substitute.For<ISoundBankConfiguration>();
            bank.DangerLevelThreshold.Returns(0.6f);

            var dangerLevel = Substitute.For<IDangerLevel>();
            dangerLevel.Level.Returns(_level);

            var router = new DangerSoundRouter(_player, dangerLevel, bank);
            router.Start();
        }

        [Test]
        public void CrossingIntoDanger_PlaysDangerWarnOnce()
        {
            _player.Play(GameSoundId.DangerWarn, null).Returns(new SoundHandle(3, 1u));

            _level.Value = 0.7f;   // enter danger
            _level.Value = 0.9f;   // still in danger — must not start a second voice

            _player.Received(1).Play(GameSoundId.DangerWarn, null);
        }

        [Test]
        public void FallingBackToSafe_StopsTheWarning()
        {
            var handle = new SoundHandle(3, 1u);
            _player.Play(GameSoundId.DangerWarn, null).Returns(handle);

            _level.Value = 0.7f;   // enter → play
            _level.Value = 0.3f;   // safe again → stop

            _player.Received(1).Stop(handle);
        }

        [Test]
        public void StayingBelowThreshold_NeverPlays()
        {
            _level.Value = 0.2f;
            _level.Value = 0.59f;

            _player.DidNotReceive().Play(GameSoundId.DangerWarn, Arg.Any<Vector3?>());
        }
    }
}

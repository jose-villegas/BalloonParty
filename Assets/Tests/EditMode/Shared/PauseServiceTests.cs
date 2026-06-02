using BalloonParty.Shared.Pause;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class PauseServiceTests
    {
        private IPublisher<PausedMessage> _pausedPublisher;
        private IPublisher<ResumedMessage> _resumedPublisher;
        private PauseService _service;

        [SetUp]
        public void SetUp()
        {
            _pausedPublisher = Substitute.For<IPublisher<PausedMessage>>();
            _resumedPublisher = Substitute.For<IPublisher<ResumedMessage>>();
            _service = new PauseService(_pausedPublisher, _resumedPublisher);
        }

        [Test]
        public void InitialState_IsNotPaused()
        {
            Assert.IsFalse(_service.IsAnyPaused.Value);
        }

        [Test]
        public void Pause_SetsIsAnyPausedTrue()
        {
            _service.Pause(PauseSource.Cinematic);

            Assert.IsTrue(_service.IsAnyPaused.Value);
        }

        [Test]
        public void Pause_PublishesPausedMessage()
        {
            _service.Pause(PauseSource.Cinematic);

            _pausedPublisher.Received(1).Publish(Arg.Any<PausedMessage>());
        }

        [Test]
        public void Resume_AfterSinglePause_SetsIsAnyPausedFalse()
        {
            _service.Pause(PauseSource.Cinematic);
            _service.Resume(PauseSource.Cinematic);

            Assert.IsFalse(_service.IsAnyPaused.Value);
        }

        [Test]
        public void Resume_PublishesResumedMessage()
        {
            _service.Pause(PauseSource.Cinematic);
            _service.Resume(PauseSource.Cinematic);

            _resumedPublisher.Received(1).Publish(Arg.Any<ResumedMessage>());
        }

        [Test]
        public void NestedPause_SameSource_StaysPausedUntilAllResumed()
        {
            _service.Pause(PauseSource.Cinematic);
            _service.Pause(PauseSource.Cinematic);
            _service.Resume(PauseSource.Cinematic);

            Assert.IsTrue(_service.IsAnyPaused.Value);

            _service.Resume(PauseSource.Cinematic);

            Assert.IsFalse(_service.IsAnyPaused.Value);
        }

        [Test]
        public void Resume_WithoutPriorPause_DoesNothing()
        {
            _service.Resume(PauseSource.Cinematic);

            Assert.IsFalse(_service.IsAnyPaused.Value);
            _resumedPublisher.DidNotReceive().Publish(Arg.Any<ResumedMessage>());
        }

        [Test]
        public void MultipleSources_OneResumed_StillPaused()
        {
            _service.Pause(PauseSource.Cinematic);
            _service.Pause(PauseSource.LevelUp);
            _service.Resume(PauseSource.Cinematic);

            Assert.IsTrue(_service.IsAnyPaused.Value);
        }
    }
}



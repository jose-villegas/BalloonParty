using System;
using BalloonParty.Game.Coins;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class CoinWalletSaverTests
    {
        private ICoinWalletPersistence _wallet;
        private IMessageHandler<GameOverMessage> _gameOverHandler;
        private IMessageHandler<RunResetMessage> _runResetHandler;
        private Subject<Unit> _suspending;
        private CoinWalletSaver _saver;

        [SetUp]
        public void SetUp()
        {
            _wallet = Substitute.For<ICoinWalletPersistence>();
            _suspending = new Subject<Unit>();
            var suspendSignal = Substitute.For<IAppSuspendSignal>();
            suspendSignal.Suspending.Returns(_suspending);

            _saver = new CoinWalletSaver(
                _wallet,
                CaptureSubscriber<GameOverMessage>(h => _gameOverHandler = h),
                CaptureSubscriber<RunResetMessage>(h => _runResetHandler = h),
                suspendSignal);
            _saver.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _saver.Dispose();
            _suspending.Dispose();
        }

        [Test]
        public void Start_DoesNotSave()
        {
            _wallet.DidNotReceive().Save();
        }

        [Test]
        public void GameOver_SavesTheWallet()
        {
            _gameOverHandler.Handle(new GameOverMessage(5, 1200));

            _wallet.Received(1).Save();
        }

        [Test]
        public void RunReset_SavesTheWallet()
        {
            // Catches coins that landed after the game over's save, and anything spent on the game-over screen.
            _runResetHandler.Handle(new RunResetMessage(true));

            _wallet.Received(1).Save();
        }

        [Test]
        public void AppSuspending_SavesTheWallet()
        {
            _suspending.OnNext(Unit.Default);

            _wallet.Received(1).Save();
        }

        [Test]
        public void Dispose_SavesTheWallet()
        {
            _saver.Dispose();

            _wallet.Received(1).Save();
        }

        [Test]
        public void AfterDispose_SuspendingNoLongerSaves()
        {
            _saver.Dispose();
            _wallet.ClearReceivedCalls();

            _suspending.OnNext(Unit.Default);

            _wallet.DidNotReceive().Save();
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

using System;
using BalloonParty.Game.Coins;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class CoinCreditorTests
    {
        private ICoinWallet _wallet;
        private IMessageHandler<CoinDroppedMessage> _droppedHandler;
        private CoinCreditor _creditor;

        [SetUp]
        public void SetUp()
        {
            _wallet = Substitute.For<ICoinWallet>();
            var subscriber = Substitute.For<ISubscriber<CoinDroppedMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<CoinDroppedMessage>>(h => _droppedHandler = h),
                    Arg.Any<MessageHandlerFilter<CoinDroppedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _creditor = new CoinCreditor(_wallet, subscriber);
            _creditor.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _creditor.Dispose();
        }

        [Test]
        public void CoinDropped_AddsItsAmountToTheWallet()
        {
            _droppedHandler.Handle(new CoinDroppedMessage(2, Vector3.zero));

            _wallet.Received(1).Add(2);
        }
    }
}

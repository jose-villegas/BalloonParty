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
        private IMessageHandler<CoinLandedMessage> _landedHandler;
        private CoinCreditor _creditor;

        [SetUp]
        public void SetUp()
        {
            _wallet = Substitute.For<ICoinWallet>();
            var subscriber = Substitute.For<ISubscriber<CoinLandedMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<CoinLandedMessage>>(h => _landedHandler = h),
                    Arg.Any<MessageHandlerFilter<CoinLandedMessage>[]>())
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
        public void CoinLanded_AddsItsAmountToTheWallet()
        {
            _landedHandler.Handle(new CoinLandedMessage(2, Vector3.zero));

            _wallet.Received(1).Add(2);
        }
    }
}

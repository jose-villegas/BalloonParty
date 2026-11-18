using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Items;
using BalloonParty.Item;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     Which hit releases a carried item. The activation itself waits a frame (<c>UniTask.Yield</c>), which an
    ///     EditMode test cannot drive, so these read the release decision the activator acts on.
    /// </summary>
    [TestFixture]
    public class ItemActivatorTests
    {
        private IBalloonItem _bomb;
        private ItemActivator _activator;

        [SetUp]
        public void SetUp()
        {
            _bomb = Substitute.For<IBalloonItem>();
            _bomb.Type.Returns(ItemType.Bomb);
            _activator = new ItemActivator(
                new[] { _bomb }, Substitute.For<ISubscriber<ActorHitMessage>>(),
                Substitute.For<IPublisher<ItemActivatedMessage>>());
            _activator.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _activator.Dispose();
        }

        [Test]
        public void ACoatedCarrier_KeepsItsItemThroughTheCoatHit_AndReleasesItOnThePop()
        {
            var carrier = new BalloonModel();
            carrier.Item.Value = ItemType.Bomb;
            carrier.TryCoat(1);

            var coatHit = Hit(carrier);
            Assert.AreEqual(HitOutcome.PassThrough, coatHit.Outcome);
            Assert.IsNull(_activator.HandlerReleasedBy(coatHit), "the coat hit releases nothing");

            var pop = Hit(carrier);
            Assert.AreEqual(HitOutcome.Pop, pop.Outcome);
            Assert.AreSame(_bomb, _activator.HandlerReleasedBy(pop));
        }

        [Test]
        public void AnUncoatedCarrier_ReleasesItsItemOnTheFirstHit()
        {
            var carrier = new BalloonModel();
            carrier.Item.Value = ItemType.Bomb;

            Assert.AreSame(_bomb, _activator.HandlerReleasedBy(Hit(carrier)));
        }

        private static ActorHitMessage Hit(BalloonModel balloon)
        {
            return ActorHitMessage.From(balloon, Vector3.zero, Vector3.up,
                new DamageContext(1, DamageFlags.DirectHit, string.Empty, null));
        }
    }
}

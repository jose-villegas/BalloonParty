using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     Exercises the registry's handle/free-list mechanics via TryResolve. Controllers are
    ///     constructed with null collaborators and never driven (HandleHit/HandleBoardClear need
    ///     a live view) — these tests cover the array bookkeeping, which is the risky part.
    /// </summary>
    [TestFixture]
    public class BalloonControllerRegistryTests
    {
        private BalloonControllerRegistry _registry;
        private IPublisher<BoardDepletedMessage> _depletedPublisher;

        [SetUp]
        public void SetUp()
        {
            _depletedPublisher = Substitute.For<IPublisher<BoardDepletedMessage>>();
            _registry = new BalloonControllerRegistry(null, _depletedPublisher);
        }

        [Test]
        public void Register_ResolvesTheOwningController()
        {
            var model = new BalloonModel();
            var controller = CreateController(model);

            _registry.Register(model, controller);

            Assert.IsTrue(_registry.TryResolve(model, out var resolved));
            Assert.AreSame(controller, resolved);
        }

        [Test]
        public void UnregisteredModel_DoesNotResolve()
        {
            var model = new BalloonModel();

            Assert.AreEqual(-1, model.RegistryHandle);
            Assert.IsFalse(_registry.TryResolve(model, out _));
        }

        [Test]
        public void Unregister_ClearsResolutionAndHandle()
        {
            var model = new BalloonModel();
            _registry.Register(model, CreateController(model));

            _registry.Unregister(model);

            Assert.AreEqual(-1, model.RegistryHandle);
            Assert.IsFalse(_registry.TryResolve(model, out _));
        }

        [Test]
        public void ReusedIndex_DoesNotResolveTheStaleModel()
        {
            var first = new BalloonModel();
            _registry.Register(first, CreateController(first));
            var reusedIndex = first.RegistryHandle;
            _registry.Unregister(first);

            // Restore the stale handle a dangling reference would still carry: the reused slot
            // must resolve only its new owner, never the previous one.
            var second = new BalloonModel();
            var secondController = CreateController(second);
            _registry.Register(second, secondController);
            first.RegistryHandle = reusedIndex;

            Assert.AreEqual(reusedIndex, second.RegistryHandle);
            Assert.IsFalse(_registry.TryResolve(first, out _));
            Assert.IsTrue(_registry.TryResolve(second, out var resolved));
            Assert.AreSame(secondController, resolved);
        }

        [Test]
        public void Growth_PastInitialCapacity_KeepsAllResolutions()
        {
            var models = new BalloonModel[300];
            var controllers = new BalloonController[300];

            for (var i = 0; i < models.Length; i++)
            {
                models[i] = new BalloonModel();
                controllers[i] = CreateController(models[i]);
                _registry.Register(models[i], controllers[i]);
            }

            for (var i = 0; i < models.Length; i++)
            {
                Assert.IsTrue(_registry.TryResolve(models[i], out var resolved), $"model {i}");
                Assert.AreSame(controllers[i], resolved, $"model {i}");
            }
        }

        [Test]
        public void FreedIndices_AreReused()
        {
            var first = new BalloonModel();
            _registry.Register(first, CreateController(first));
            var index = first.RegistryHandle;
            _registry.Unregister(first);

            var second = new BalloonModel();
            _registry.Register(second, CreateController(second));

            Assert.AreEqual(index, second.RegistryHandle);
        }

        [Test]
        public void Unregister_LastBalloon_PublishesBoardDepleted()
        {
            var model = new BalloonModel();
            _registry.Register(model, CreateController(model));

            _registry.Unregister(model);

            _depletedPublisher.Received(1).Publish(Arg.Any<BoardDepletedMessage>());
        }

        [Test]
        public void Unregister_NotLastBalloon_DoesNotPublishBoardDepleted()
        {
            var first = new BalloonModel();
            var second = new BalloonModel();
            _registry.Register(first, CreateController(first));
            _registry.Register(second, CreateController(second));

            _registry.Unregister(first);

            _depletedPublisher.DidNotReceive().Publish(Arg.Any<BoardDepletedMessage>());
        }

        [Test]
        public void Unregister_AllBalloonsOneByOne_PublishesOnlyOnLast()
        {
            var first = new BalloonModel();
            var second = new BalloonModel();
            _registry.Register(first, CreateController(first));
            _registry.Register(second, CreateController(second));

            _registry.Unregister(first);
            _depletedPublisher.DidNotReceive().Publish(Arg.Any<BoardDepletedMessage>());

            _registry.Unregister(second);
            _depletedPublisher.Received(1).Publish(Arg.Any<BoardDepletedMessage>());
        }

        private static BalloonController CreateController(IWriteableBalloonModel model)
        {
            var context = new BalloonControllerContext(
                null, null, null, null, null, null, null, null, null, null, null);
            return new BalloonController(model, null, "key", null, null, context);
        }
    }
}

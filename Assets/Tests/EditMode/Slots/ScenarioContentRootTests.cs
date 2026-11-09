using BalloonParty.Slots.Actor;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class ScenarioContentRootTests
    {
        private ScenarioContentRoot _root;
        private Transform _incoming;
        private Transform _outgoing;

        [SetUp]
        public void SetUp()
        {
            _root = new ScenarioContentRoot();
            _incoming = new GameObject("Incoming").transform;
            _incoming.SetParent(_root.Transform, worldPositionStays: false);
            _incoming.localPosition = new Vector3(0.5f, 2f, 0f);
            _outgoing = new GameObject("Outgoing").transform;
            _outgoing.SetParent(_root.Outgoing, worldPositionStays: false);
            _outgoing.localPosition = new Vector3(-1f, -15f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            // Dispose uses Destroy, which edit mode refuses.
            Object.DestroyImmediate(_root.Transform.gameObject);
        }

        [Test]
        public void Travel_MovesBothHalvesTogether()
        {
            _root.SetTravel(15f);

            AssertNear(new Vector2(0.5f, 17f), _incoming.position);
            AssertNear(new Vector2(-1f, 0f), _outgoing.position);
        }

        [Test]
        public void Depth_ScalesEachHalfAboutTheCamera_ByItsOwnFactor()
        {
            _root.SetTravel(6f);
            _root.SetDepth(0.5f, 1.5f);

            AssertNear(0.5f * new Vector2(0.5f, 8f), _incoming.position);
            AssertNear(1.5f * new Vector2(-1f, -9f), _outgoing.position);
            Assert.AreEqual(0.5f, _incoming.lossyScale.x, 1e-4f);
            Assert.AreEqual(1.5f, _outgoing.lossyScale.x, 1e-4f);
        }

        [Test]
        public void Depth_KeepsTheTravelUnscaled_SoTheBeatCanKeepWritingIt()
        {
            _root.SetDepth(0.5f, 1f);
            _root.SetTravel(10f);

            Assert.AreEqual(10f, _root.Travel);
            AssertNear(0.5f * new Vector2(0.5f, 12f), _incoming.position);
        }

        [Test]
        public void DepthBackToOne_RestoresTheRestLayout()
        {
            _root.SetTravel(0f);
            _root.SetDepth(0.4f, 2.5f);

            _root.SetDepth(1f, 1f);

            AssertNear(new Vector2(0.5f, 2f), _incoming.position);
            AssertNear(new Vector2(-1f, -15f), _outgoing.position);
            Assert.AreEqual(1f, _outgoing.lossyScale.x, 1e-4f);
        }

        private static void AssertNear(Vector2 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
        }
    }
}

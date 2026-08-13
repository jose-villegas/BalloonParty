using System.Collections.Generic;
using BalloonParty.Prediction;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Prediction
{
    [TestFixture]
    public class PredictionTraceProviderTests
    {
        private PredictionTraceProvider _provider;
        private List<Vector3> _points;

        [SetUp]
        public void SetUp()
        {
            _provider = new PredictionTraceProvider();
            _points = new List<Vector3> { Vector3.zero, new(1f, 0f, 0f), new(2f, 0f, 0f) };
        }

        [Test]
        public void SetTrace_CalledTwiceWithIdenticalTrace_BumpsVersionOnce()
        {
            _provider.SetTrace(_points);
            var versionAfterFirst = _provider.Version;

            _provider.SetTrace(new List<Vector3>(_points));

            Assert.AreEqual(1, versionAfterFirst);
            Assert.AreEqual(1, _provider.Version, "an unchanged trace must not bump Version again");
        }

        [Test]
        public void SetTrace_ChangedPoint_BumpsVersion()
        {
            _provider.SetTrace(_points);

            var changed = new List<Vector3>(_points) { [1] = new Vector3(1f, 1f, 0f) };
            _provider.SetTrace(changed);

            Assert.AreEqual(2, _provider.Version);
        }

        [Test]
        public void SetTrace_ChangedEndKindOnly_BumpsVersion()
        {
            var wallEnd = new PredictionTraceEnd(-1, PredictionTraceEndKind.Wall, Vector2.up);
            _provider.SetTrace(_points, wallEnd);

            var deflectorEnd = new PredictionTraceEnd(-1, PredictionTraceEndKind.Deflector, Vector2.up);
            _provider.SetTrace(_points, deflectorEnd);

            Assert.AreEqual(2, _provider.Version);
        }

        [Test]
        public void SetTrace_SubEpsilonDrift_DoesNotBumpVersion()
        {
            _provider.SetTrace(_points);
            var versionAfterFirst = _provider.Version;

            var drifted = new List<Vector3>(_points) { [1] = _points[1] + new Vector3(1e-6f, 0f, 0f) };
            _provider.SetTrace(drifted);

            Assert.AreEqual(versionAfterFirst, _provider.Version);
        }

        [Test]
        public void Clear_WhenActive_BumpsVersionOnce()
        {
            _provider.SetTrace(_points);
            var versionAfterSet = _provider.Version;

            _provider.Clear();

            Assert.AreEqual(versionAfterSet + 1, _provider.Version);
            Assert.IsFalse(_provider.IsActive);
        }

        [Test]
        public void Clear_CalledTwice_SecondCallDoesNotBumpVersion()
        {
            _provider.SetTrace(_points);
            _provider.Clear();
            var versionAfterFirstClear = _provider.Version;

            _provider.Clear();

            Assert.AreEqual(versionAfterFirstClear, _provider.Version);
        }
    }
}

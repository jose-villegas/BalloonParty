using System.Collections.Generic;
using BalloonParty.Prediction;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Prediction
{
    [TestFixture]
    public class PredictionTraceCalculatorTests
    {
        private static readonly Vector4 DefaultLimits = new(5f, 3f, -5f, -3f);

        private PredictionTraceCalculator _calculator;
        private IGameConfiguration _config;
        private List<Vector3> _results;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IGameConfiguration>();
            _config.LimitsClockwise.Returns(DefaultLimits);
            _config.PredictionTraceStep.Returns(0.5f);
            _config.PredictionTraceMaxBounces.Returns(3);
            _config.PredictionTraceMaxSteps.Returns(100);

            _calculator = new PredictionTraceCalculator(_config, _config);
            _results = new List<Vector3>();
        }

        [Test]
        public void Calculate_LeftWallBounce_ReflectsAtLeftLimit()
        {
            var origin = new Vector3(-2.5f, 0f, 0f);
            var direction = new Vector3(-1f, 1f, 0f).normalized;

            _calculator.Calculate(origin, direction, _results);

            Assert.GreaterOrEqual(_results.Count, 2);
            Assert.AreEqual(DefaultLimits.w, _results[1].x, 0.01f);
        }

        [Test]
        public void Calculate_MaxBounces_StopsAfterLimit()
        {
            _config.PredictionTraceMaxBounces.Returns(1);
            _config.PredictionTraceMaxSteps.Returns(200);

            var origin = new Vector3(2.5f, 0f, 0f);
            var direction = new Vector3(1f, 1f, 0f).normalized;

            _calculator.Calculate(origin, direction, _results);

            Assert.AreEqual(2, _results.Count);
        }

        [Test]
        public void Calculate_MaxSteps_StopsBeforeReachingWall()
        {
            _config.PredictionTraceMaxSteps.Returns(3);
            _config.PredictionTraceMaxBounces.Returns(10);

            _calculator.Calculate(Vector3.zero, Vector3.up, _results);

            Assert.AreEqual(1, _results.Count);
        }

        [Test]
        public void Calculate_RightWallBounce_ReflectsAtRightLimit()
        {
            var origin = new Vector3(2.5f, 0f, 0f);
            var direction = new Vector3(1f, 1f, 0f).normalized;

            _calculator.Calculate(origin, direction, _results);

            Assert.GreaterOrEqual(_results.Count, 2);
            Assert.AreEqual(DefaultLimits.y, _results[1].x, 0.01f);
        }

        [Test]
        public void Calculate_StraightUp_HitsTopWall()
        {
            _calculator.Calculate(Vector3.zero, Vector3.up, _results);

            var lastPoint = _results[_results.Count - 1];
            Assert.AreEqual(DefaultLimits.x, lastPoint.y, 0.01f);
        }

        [Test]
        public void Calculate_TopWallHit_TerminatesBouncing()
        {
            _config.PredictionTraceMaxBounces.Returns(10);
            _config.PredictionTraceMaxSteps.Returns(200);

            _calculator.Calculate(Vector3.zero, Vector3.up, _results);

            Assert.AreEqual(2, _results.Count);
        }

        [Test]
        public void Calculate_ZigZag_ProducesMultipleBouncePoints()
        {
            _config.PredictionTraceMaxBounces.Returns(5);
            _config.PredictionTraceMaxSteps.Returns(200);
            _config.PredictionTraceStep.Returns(1f);

            var direction = new Vector3(1f, 0.3f, 0f).normalized;

            _calculator.Calculate(Vector3.zero, direction, _results);

            Assert.GreaterOrEqual(_results.Count, 3);
        }
    }
}

using BalloonParty.Shared.Animation;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class CurveUtilityTests
    {
        [Test]
        public void LerpWithVerticalCurve_AtStart_ReturnsFromPosition()
        {
            var from = new Vector3(0f, 0f, 0f);
            var to = new Vector3(10f, 0f, 0f);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            var result = CurveUtility.LerpWithVerticalCurve(from, to, 0f, 5f, curve);

            Assert.AreEqual(from.x, result.x, 0.001f);
            Assert.AreEqual(0f, result.y, 0.001f);
        }

        [Test]
        public void LerpWithVerticalCurve_AtEnd_ReturnsToPositionPlusHeight()
        {
            var from = new Vector3(0f, 0f, 0f);
            var to = new Vector3(10f, 0f, 0f);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            var result = CurveUtility.LerpWithVerticalCurve(from, to, 1f, 5f, curve);

            Assert.AreEqual(10f, result.x, 0.001f);
            Assert.AreEqual(5f, result.y, 0.001f);
        }

        [Test]
        public void LerpWithVerticalCurve_Midpoint_AppliesVerticalOffset()
        {
            var from = Vector3.zero;
            var to = new Vector3(4f, 0f, 0f);
            var curve = AnimationCurve.Constant(0f, 1f, 1f);

            var result = CurveUtility.LerpWithVerticalCurve(from, to, 0.5f, 3f, curve);

            Assert.AreEqual(2f, result.x, 0.001f);
            Assert.AreEqual(3f, result.y, 0.001f);
        }

        [Test]
        public void LerpWithVerticalCurve_ZeroHeight_NoVerticalOffset()
        {
            var from = Vector3.zero;
            var to = new Vector3(6f, 2f, 0f);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            var result = CurveUtility.LerpWithVerticalCurve(from, to, 0.5f, 0f, curve);

            Assert.AreEqual(3f, result.x, 0.001f);
            Assert.AreEqual(1f, result.y, 0.001f);
        }

        [Test]
        public void SampleMultiplied_ReturnsBaseTimeCurve()
        {
            var curve = AnimationCurve.Constant(0f, 1f, 2f);

            var result = CurveUtility.SampleMultiplied(0.5f, 3f, curve);

            Assert.AreEqual(6f, result, 0.001f);
        }

        [Test]
        public void SampleMultiplied_ZeroBase_ReturnsZero()
        {
            var curve = AnimationCurve.Constant(0f, 1f, 5f);

            var result = CurveUtility.SampleMultiplied(0.5f, 0f, curve);

            Assert.AreEqual(0f, result, 0.001f);
        }
    }
}


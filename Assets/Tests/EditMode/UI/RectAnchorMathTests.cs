using BalloonParty.UI;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class RectAnchorMathTests
    {
        private static readonly Rect Bounds = new(-100f, -200f, 200f, 400f);
        private static readonly Vector2 HalfSize = new(30f, 7f);

        [Test]
        public void ClampInside_APointWellInside_StaysPut()
        {
            var point = new Vector2(10f, -50f);

            Assert.AreEqual(point, RectAnchorMath.ClampInside(Bounds, point, HalfSize));
        }

        [Test]
        public void ClampInside_APointOnTheRightEdge_MovesInByHalfTheBox()
        {
            var clamped = RectAnchorMath.ClampInside(Bounds, new Vector2(100f, 0f), HalfSize);

            Assert.AreEqual(new Vector2(70f, 0f), clamped);
        }

        [Test]
        public void ClampInside_APointPastTheTopLeftCorner_LandsInTheCorner()
        {
            var clamped = RectAnchorMath.ClampInside(Bounds, new Vector2(-500f, 900f), HalfSize);

            Assert.AreEqual(new Vector2(-70f, 193f), clamped);
        }

        [Test]
        public void ClampInside_ABoxWiderThanTheBounds_IsCentredAcross()
        {
            var clamped = RectAnchorMath.ClampInside(Bounds, new Vector2(80f, 20f), new Vector2(150f, 7f));

            Assert.AreEqual(new Vector2(0f, 20f), clamped);
        }
    }
}

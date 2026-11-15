using System.Collections.Generic;
using BalloonParty.UI.ClothRibbon;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class RibbonClothTests
    {
        private const float Frame = 1f / 60f;
        private const float Length = 60f;

        private static readonly IReadOnlyList<KeepOut> NoObstacles = new KeepOut[0];

        private RibbonClothSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = new RibbonClothSettings();
        }

        [Test]
        public void AStillAnchor_HangsTheLineBelowIt_AndStreamsTheRibbonFrontOn()
        {
            var cloth = new RibbonCloth(_settings, Length);
            cloth.Place(Vector3.zero);

            Run(cloth, Vector3.zero, Vector3.zero, 3f);

            var pivot = cloth.LinePoint(cloth.LinePoints - 1);
            Assert.That(pivot.y, Is.LessThan(-0.6f * _settings.LineLength), "the line hangs down from the knot");
            Assert.That(cloth.Surface(1f, 0f).x, Is.GreaterThan(pivot.x + 0.5f * Length), "the ribbon streams to +x");
            Assert.That(cloth.Facing(0.5f), Is.GreaterThan(0.5f), "its front faces the screen");
        }

        [Test]
        public void TravellingTheOtherWay_SwingsTheRibbonRoundBehind_ShowingItsBack()
        {
            var cloth = new RibbonCloth(_settings, Length);
            cloth.Place(Vector3.zero);
            Run(cloth, Vector3.zero, Vector3.zero, 2f);

            var end = Run(cloth, Vector3.zero, new Vector3(40f, 0f, 0f), 3f);
            Run(cloth, end, end, 2f);

            var pivot = cloth.LinePoint(cloth.LinePoints - 1);
            Assert.That(cloth.Heading, Is.EqualTo(-1f).Within(1e-4f), "the breeze turned round");
            Assert.That(cloth.Surface(1f, 0f).x, Is.LessThan(pivot.x - 0.5f * Length), "the ribbon trails to -x");
            Assert.That(cloth.Facing(0.5f), Is.LessThan(-0.5f), "its back faces the screen");
        }

        [Test]
        public void TheSwing_GoesRoundThroughDepth_WithTheRibbonKeptUpright()
        {
            var cloth = new RibbonCloth(_settings, Length);
            cloth.Place(Vector3.zero);
            Run(cloth, Vector3.zero, Vector3.zero, 2f);

            var deepest = 0f;
            var anchor = Vector3.zero;
            for (var t = 0f; t < 3f; t += Frame)
            {
                anchor += new Vector3(40f * Frame, 0f, 0f);
                cloth.Step(anchor, Frame, NoObstacles);
                deepest = Mathf.Max(deepest, Mathf.Abs(cloth.Surface(1f, 0f).z - anchor.z));
                for (var column = 0; column < cloth.Columns; column++)
                {
                    Assert.That(cloth.ClothPoint(column, 0).y, Is.GreaterThan(cloth.ClothPoint(column, 2).y),
                        "top stays over bottom: the ribbon never rolls over in the screen's plane");
                }
            }

            Assert.That(deepest, Is.GreaterThan(0.3f * Length), "the tail swung out of the screen on its way round");
        }

        [Test]
        public void NearAnEdge_TheRibbonStreamsTowardTheRoom_EvenAgainstTheTravel()
        {
            var cloth = new RibbonCloth(_settings, Length);
            cloth.SetRoom(-200f, 100f);
            cloth.Place(new Vector3(90f, 0f, 0f));

            // Travelling left would stream it right, off the edge 10 units away.
            var end = Run(cloth, new Vector3(90f, 0f, 0f), new Vector3(80f, 0f, 0f), 3f);
            Run(cloth, end, end, 2f);

            Assert.That(cloth.Heading, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(cloth.Surface(1f, 0f).x, Is.LessThan(end.x));
        }

        [Test]
        public void SlowDrift_DoesNotTurnTheBreeze()
        {
            var cloth = new RibbonCloth(_settings, Length);
            cloth.Place(Vector3.zero);

            var slow = _settings.HeadingSpeed * 0.5f;
            Run(cloth, Vector3.zero, new Vector3(slow * 2f, 0f, 0f), 2f);

            Assert.That(cloth.Heading, Is.EqualTo(1f));
        }

        [Test]
        public void AnObstacle_KeepsTheLineAndRibbonOutOfIt()
        {
            var cloth = new RibbonCloth(_settings, Length);
            cloth.Place(Vector3.zero);
            var obstacle = new KeepOut(new Vector2(20f, -35f), new Vector2(20f, -35f), 15f);

            var anchor = Vector3.zero;
            for (var t = 0f; t < 4f; t += Frame)
            {
                cloth.Step(anchor, Frame, new[] { obstacle });
                for (var i = 0; i < cloth.LinePoints; i++)
                {
                    Assert.That(obstacle.DistanceFrom(cloth.LinePoint(i)), Is.GreaterThan(obstacle.Radius - 0.5f));
                }

                for (var column = 0; column < cloth.Columns; column++)
                {
                    for (var row = 0; row < 3; row++)
                    {
                        Assert.That(obstacle.DistanceFrom(cloth.ClothPoint(column, row)),
                            Is.GreaterThan(obstacle.Radius - 0.5f));
                    }
                }
            }
        }

        [Test]
        public void TheBackWriting_RunsTheOtherWayAlongTheRibbon()
        {
            Assert.That(ClothRibbonWriting.AlongFor(-Length * 0.5f, Length, false), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(ClothRibbonWriting.AlongFor(-Length * 0.5f, Length, true), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ClothRibbonWriting.AlongFor(0f, Length, true), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void EachFace_ShowsItsWriting_OnlyWhileItFacesTheScreen()
        {
            Assert.That(ClothRibbonWriting.ShownFor(1f, false, 0.25f), Is.EqualTo(1f));
            Assert.That(ClothRibbonWriting.ShownFor(1f, true, 0.25f), Is.EqualTo(0f));
            Assert.That(ClothRibbonWriting.ShownFor(-1f, true, 0.25f), Is.EqualTo(1f));
            Assert.That(ClothRibbonWriting.ShownFor(0f, false, 0.25f), Is.EqualTo(0f));
        }

        [Test]
        public void AFold_IsShadedAsSeen_FromWhicheverSideFacesTheScreen()
        {
            var leaning = new Vector3(0.3f, 0f, 0.95f).normalized;
            var turned = new Vector3(0.3f, 0f, -0.95f).normalized;

            Assert.That(ClothRibbonGraphic.Turn(leaning, 2f), Is.GreaterThan(0f));
            Assert.That(ClothRibbonGraphic.Turn(turned, 2f), Is.LessThan(0f));
            Assert.That(ClothRibbonGraphic.Turn(Vector3.right, 5f), Is.EqualTo(1f));
        }

        // Moves the anchor in a straight line from one point to another over a time, a frame at a time; returns
        // where it ended.
        private static Vector3 Run(RibbonCloth cloth, Vector3 from, Vector3 to, float seconds)
        {
            var frames = Mathf.RoundToInt(seconds / Frame);
            for (var f = 1; f <= frames; f++)
            {
                cloth.Step(Vector3.Lerp(from, to, (float)f / frames), Frame, NoObstacles);
            }

            return to;
        }
    }
}

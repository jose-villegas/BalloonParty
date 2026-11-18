using BalloonParty.Balloon.View;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class FormationGlowBeamsTests
    {
        private const float Join = 1f;

        private readonly int[] _groups = new int[FormationGlowField.MaxPoints];
        private readonly FormationGlowBeam[] _beams = new FormationGlowBeam[FormationGlowBeams.MaxBeams];

        [Test]
        public void ALineOfRings_CastsOneBeamFromEndToEnd()
        {
            var points = new[] { Ring(0f, 0f), Ring(0.75f, 0f), Ring(1.5f, 0f), Ring(2.25f, 0f), Ring(3f, 0f) };

            var count = FormationGlowBeams.Group(points, points.Length, Join, _groups, _beams);

            Assert.AreEqual(1, count);
            AssertEnds(_beams[0], new Vector2(0f, 0f), new Vector2(3f, 0f));
            Assert.AreEqual(1f, _beams[0].Level, 1e-5f);
        }

        // Its rings are listed out of order and no two ends stand within reach of each other: only the chain joins them.
        [Test]
        public void AChainOfRings_IsOneGroup_HoweverItIsListed()
        {
            var points = new[] { Ring(3f, 0f), Ring(0f, 0f), Ring(2.25f, 0f), Ring(0.75f, 0f), Ring(1.5f, 0f) };

            var count = FormationGlowBeams.Group(points, points.Length, Join, _groups, _beams);

            Assert.AreEqual(1, count);
            AssertEnds(_beams[0], new Vector2(0f, 0f), new Vector2(3f, 0f));
        }

        [Test]
        public void TwoFormationsApart_CastABeamEach()
        {
            var points = new[] { Ring(0f, 0f), Ring(0.75f, 0f), Ring(0f, 4f), Ring(0.6f, 4.6f) };

            var count = FormationGlowBeams.Group(points, points.Length, Join, _groups, _beams);

            Assert.AreEqual(2, count);
            AssertEnds(_beams[0], new Vector2(0f, 0f), new Vector2(0.75f, 0f));
            AssertEnds(_beams[1], new Vector2(0f, 4f), new Vector2(0.6f, 4.6f));
        }

        [Test]
        public void ARingOnItsOwn_CastsABeamOfNoLength()
        {
            var points = new[] { Ring(2f, 3f, 0.6f) };

            var count = FormationGlowBeams.Group(points, 1, Join, _groups, _beams);

            Assert.AreEqual(1, count);
            AssertEnds(_beams[0], new Vector2(2f, 3f), new Vector2(2f, 3f));
            Assert.AreEqual(0.6f, _beams[0].Level, 1e-5f);
        }

        [Test]
        public void ABeamsLevel_IsItsRingsAverage_OfBrightnessTimesPresence()
        {
            var points = new[] { Ring(0f, 0f, 1f, 1f), Ring(0.75f, 0f, 0.5f, 1f), Ring(1.5f, 0f, 1f, 0.5f) };

            FormationGlowBeams.Group(points, points.Length, Join, _groups, _beams);

            Assert.AreEqual((1f + 0.5f + 0.5f) / 3f, _beams[0].Level, 1e-5f);
        }

        [Test]
        public void ADissolvedGroup_CastsNothing()
        {
            var points = new[] { Ring(0f, 0f, 1f, 0f), Ring(0.75f, 0f, 0f, 1f), Ring(5f, 5f) };

            var count = FormationGlowBeams.Group(points, points.Length, Join, _groups, _beams);

            Assert.AreEqual(1, count);
            AssertEnds(_beams[0], new Vector2(5f, 5f), new Vector2(5f, 5f));
        }

        [Test]
        public void OnlyTheFirstPointsCount()
        {
            var points = new[] { Ring(0f, 0f), Ring(0.75f, 0f), Ring(9f, 9f) };

            var count = FormationGlowBeams.Group(points, 2, Join, _groups, _beams);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void MoreGroupsThanBeams_CastsOnlyAsManyAsThereAreBeams()
        {
            var points = new Vector4[FormationGlowBeams.MaxBeams + 3];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = Ring(i * 5f, 0f);
            }

            var count = FormationGlowBeams.Group(points, points.Length, Join, _groups, _beams);

            Assert.AreEqual(FormationGlowBeams.MaxBeams, count);
        }

        [Test]
        public void NoRings_CastNothing()
        {
            Assert.AreEqual(0, FormationGlowBeams.Group(new Vector4[4], 0, Join, _groups, _beams));
        }

        private static Vector4 Ring(float x, float y, float brightness = 1f, float presence = 1f)
        {
            return new Vector4(x, y, brightness, presence);
        }

        // A beam has no direction: either end may be listed first.
        private static void AssertEnds(FormationGlowBeam beam, Vector2 a, Vector2 b)
        {
            var straight = Vector2.Distance(beam.Start, a) < 1e-4f && Vector2.Distance(beam.End, b) < 1e-4f;
            var swapped = Vector2.Distance(beam.Start, b) < 1e-4f && Vector2.Distance(beam.End, a) < 1e-4f;
            Assert.IsTrue(straight || swapped, $"beam {beam.Start} to {beam.End}, expected {a} to {b}");
        }
    }
}

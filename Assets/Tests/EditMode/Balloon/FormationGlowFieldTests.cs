using System.Collections.Generic;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The formation glow's field (<see cref="FormationGlowField" />): which members light it and how brightly,
    ///     the quad's bounds round them, and where the shader's rings join.
    /// </summary>
    [TestFixture]
    public class FormationGlowFieldTests
    {
        private const float RingRadius = 0.48f;
        private const float Reach = 0.96f;
        private const float RingWidth = 0.05f;
        private const float FullDistance = 0.8f;
        private const float FadeDistance = 2.2f;

        // The board's slot pitch: neighbours in a line stand this far apart.
        private const float Pitch = 0.75f;

        private IFormationGlowSettings _settings;
        private Vector4[] _points;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<IFormationGlowSettings>();
            _settings.RingRadius.Returns(RingRadius);
            _settings.Reach.Returns(Reach);
            _settings.RingWidth.Returns(RingWidth);
            _settings.FullDistance.Returns(FullDistance);
            _settings.FadeDistance.Returns(FadeDistance);
            _points = new Vector4[FormationGlowField.MaxPoints];
        }

        [Test]
        public void Threshold_PutsALoneMembersOutlineAtTheRingRadius()
        {
            var threshold = FormationGlowField.Threshold(RingRadius, Reach);
            var lone = new[] { new Vector4(0f, 0f, 1f, 0f) };

            Assert.AreEqual(threshold, FormationGlowField.FieldAt(lone, 1, new Vector2(RingRadius, 0f), Reach), 1e-5f);
        }

        [Test]
        public void Fill_NeighboursInALine_WeighFully()
        {
            var count = Fill(Source(0f, 0f), Source(Pitch, 0f));

            Assert.AreEqual(2, count);
            Assert.AreEqual(1f, _points[0].z, 1e-5f);
            Assert.AreEqual(1f, _points[1].z, 1e-5f);
        }

        [Test]
        public void Fill_ALoneMember_DoesNotGlow()
        {
            Assert.AreEqual(0, Fill(Source(0f, 0f)));
        }

        [Test]
        public void Fill_MembersPastTheFadeDistance_DoNotGlow()
        {
            Assert.AreEqual(0, Fill(Source(0f, 0f), Source(FadeDistance + 0.1f, 0f)));
        }

        [Test]
        public void Fill_ABalloonOutOfFormation_NeitherGlowsNorCountsAsANeighbour()
        {
            Assert.AreEqual(0, Fill(Source(0f, 0f), Source(Pitch, 0f, 0f)));
        }

        [Test]
        public void Fill_TheWeightFallsAsMembersDrawApart()
        {
            Fill(Source(0f, 0f), Source(1.2f, 0f));
            var near = _points[0].z;
            Fill(Source(0f, 0f), Source(1.8f, 0f));
            var far = _points[0].z;

            Assert.That(near, Is.LessThan(1f));
            Assert.That(far, Is.LessThan(near));
            Assert.That(far, Is.GreaterThan(0f));
        }

        [Test]
        public void Fill_MembershipScalesTheWeight()
        {
            Fill(Source(0f, 0f, 0.5f), Source(Pitch, 0f));

            Assert.AreEqual(0.5f, _points[0].z, 1e-5f);
            Assert.AreEqual(1f, _points[1].z, 1e-5f);
        }

        [Test]
        public void Fill_LeavesOutAMemberWithNoNeighbour_AndCompactsTheRest()
        {
            var count = Fill(Source(0f, 0f), Source(10f, 10f), Source(Pitch, 0f));

            Assert.AreEqual(2, count);
            Assert.AreEqual(new Vector2(0f, 0f), (Vector2)_points[0]);
            Assert.AreEqual(new Vector2(Pitch, 0f), (Vector2)_points[1]);
        }

        [Test]
        public void Fill_TheBoundsCoverTheMembersAndTheirLight()
        {
            Fill(out var bounds, Source(1f, 2f), Source(1f + Pitch, 2f));

            var margin = Reach + RingWidth * 3f;
            Assert.AreEqual(1f - margin, bounds.xMin, 1e-5f);
            Assert.AreEqual(1f + Pitch + margin, bounds.xMax, 1e-5f);
            Assert.AreEqual(2f - margin, bounds.yMin, 1e-5f);
            Assert.AreEqual(2f + margin, bounds.yMax, 1e-5f);
        }

        [Test]
        public void Fill_StopsAtTheShadersArraySize()
        {
            var sources = new List<IFormationGlowSource>();
            for (var i = 0; i < FormationGlowField.MaxPoints + 6; i++)
            {
                sources.Add(Source(i * Pitch, 0f));
            }

            Assert.AreEqual(FormationGlowField.MaxPoints, FormationGlowField.Fill(sources, _settings, _points, out _));
        }

        [Test]
        public void Rings_OfNeighboursInALine_Join()
        {
            Fill(Source(0f, 0f), Source(Pitch, 0f));

            var midpoint = FormationGlowField.FieldAt(_points, 2, new Vector2(Pitch * 0.5f, 0f), Reach);
            Assert.That(midpoint, Is.GreaterThan(FormationGlowField.Threshold(RingRadius, Reach)));
        }

        [Test]
        public void Rings_TwoPitchesApart_StaySeparate()
        {
            Fill(Source(0f, 0f), Source(Pitch * 2f, 0f));

            var midpoint = FormationGlowField.FieldAt(_points, 2, new Vector2(Pitch, 0f), Reach);
            Assert.That(midpoint, Is.LessThan(FormationGlowField.Threshold(RingRadius, Reach)));
        }

        private static IFormationGlowSource Source(float x, float y, float membership = 1f)
        {
            var source = Substitute.For<IFormationGlowSource>();
            source.GlowCentre.Returns(new Vector2(x, y));
            source.GlowMembership.Returns(membership);
            return source;
        }

        private int Fill(params IFormationGlowSource[] sources)
        {
            return Fill(out _, sources);
        }

        private int Fill(out Rect bounds, params IFormationGlowSource[] sources)
        {
            return FormationGlowField.Fill(sources, _settings, _points, out bounds);
        }
    }
}

using System;
using System.Collections.Generic;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The formation glow's field (<see cref="FormationGlowField" />): which rings light and how brightly, how a
    ///     disconnected ring dissolves instead of vanishing, the quad's bounds, and where the shader's rings join.
    /// </summary>
    [TestFixture]
    public class FormationGlowFieldTests
    {
        private const float RingRadius = 0.48f;
        private const float Reach = 0.96f;
        private const float RingWidth = 0.05f;
        private const float FullDistance = 0.8f;
        private const float FadeDistance = 2.2f;
        private const float Dissolve = 0.5f;
        private const float Settled = 10f;

        // The board's slot pitch: neighbours in a line stand this far apart.
        private const float Pitch = 0.75f;

        private IFormationGlowSettings _settings;
        private FormationGlowField _field;
        private List<IFormationGlowSource> _sources;
        private Vector4[] _points;
        private float[] _formations;
        private float _threshold;
        private int _count;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<IFormationGlowSettings>();
            _settings.RingRadius.Returns(RingRadius);
            _settings.Reach.Returns(Reach);
            _settings.RingWidth.Returns(RingWidth);
            _settings.FullDistance.Returns(FullDistance);
            _settings.FadeDistance.Returns(FadeDistance);
            _settings.DissolveSeconds.Returns(Dissolve);
            _field = new FormationGlowField();
            _sources = new List<IFormationGlowSource>();
            _points = new Vector4[FormationGlowField.MaxPoints];
            _formations = new float[FormationGlowField.MaxPoints];
            _threshold = FormationGlowField.Threshold(RingRadius, Reach);
        }

        [Test]
        public void Threshold_PutsAWholeLoneRingAtTheRingRadius()
        {
            var lone = new[] { new Vector4(0f, 0f, 1f, 1f) };

            Assert.AreEqual(_threshold, FormationGlowField.FieldAt(lone, 1, new Vector2(RingRadius, 0f), Reach, _threshold), 1e-5f);
        }

        [TestCase(0.4f, 0.96f)]
        [TestCase(0.3f, 1.2f)]
        [TestCase(0.5f, 0.7f)]
        public void JoinDistance_IsWhereTwoRingsFieldsMeetTheThresholdBetweenThem(float ringRadius, float reach)
        {
            var apart = FormationGlowField.JoinDistance(ringRadius, reach);

            var falloff = 1f - (apart * 0.5f) * (apart * 0.5f) / (reach * reach);
            Assert.AreEqual(FormationGlowField.Threshold(ringRadius, reach), 2f * falloff * falloff * falloff, 1e-4f);
        }

        // Members a slot apart (0.75) share one outline, and two formations with an empty slot between (1.5) don't.
        [Test]
        public void JoinDistance_AtTheAuthoredRing_LiesBetweenASlotAndTwo()
        {
            var apart = FormationGlowField.JoinDistance(0.4f, 0.96f);

            Assert.Greater(apart, 0.75f);
            Assert.Less(apart, 1.5f);
        }

        [Test]
        public void Shape_AtNoPresence_DrawsALoneRingBackIntoItsMember()
        {
            var gone = new[] { new Vector4(0f, 0f, 1f, 0f) };

            Assert.That(FormationGlowField.FieldAt(gone, 1, new Vector2(0.05f, 0f), Reach, _threshold), Is.LessThan(_threshold));
        }

        [Test]
        public void Step_NeighboursJoining_DissolveInOverTheDissolveTime_AtFullBrightness()
        {
            Add(0f, 0f);
            Add(Pitch, 0f);

            Assert.AreEqual(2, Step(Dissolve * 0.5f));
            Assert.AreEqual(1f, _points[0].z, 1e-4f, "it doesn't fade up: its presence alone grows");
            Assert.AreEqual(0.5f, _points[0].w, 1e-4f);

            Step(Dissolve * 0.5f);
            Assert.AreEqual(1f, _points[1].w, 1e-4f);
        }

        [Test]
        public void Step_AMemberJoiningAFormedPair_DissolvesIn_AndEasesIntoTheMerge()
        {
            Add(0f, 0f);
            Add(Pitch, 0f);
            Step(Settled);
            var midpoint = new Vector2(Pitch * 1.5f, 0f);
            var before = FormationGlowField.FieldAt(_points, 2, midpoint, Reach, _threshold);

            Add(Pitch * 2f, 0f);
            var count = Step(Dissolve * 0.5f);
            var half = FormationGlowField.FieldAt(_points, count, midpoint, Reach, _threshold);
            count = Step(Dissolve * 0.5f);
            var whole = FormationGlowField.FieldAt(_points, count, midpoint, Reach, _threshold);

            Assert.AreEqual(3, count);
            Assert.That(half, Is.GreaterThan(before));
            Assert.That(whole, Is.GreaterThan(half));
        }

        [Test]
        public void Step_ALoneMember_NeverLights()
        {
            Add(0f, 0f);

            Assert.AreEqual(0, Step(Settled));
        }

        [Test]
        public void Step_MembersPastTheFadeDistance_NeverLight()
        {
            Add(0f, 0f);
            Add(FadeDistance + 0.1f, 0f);

            Assert.AreEqual(0, Step(Settled));
        }

        [Test]
        public void Step_ABalloonOutOfFormation_DoesNotCountAsANeighbour()
        {
            Add(0f, 0f);
            Add(Pitch, 0f, false);

            Assert.AreEqual(0, Step(Settled));
        }

        [Test]
        public void Step_TheBrightnessFallsAsMembersDrawApart()
        {
            Add(0f, 0f);
            var other = Add(1.2f, 0f);
            Step(Settled);
            var near = _points[0].z;
            other.Centre = new Vector2(1.8f, 0f);
            Step(Settled);
            var far = _points[0].z;

            Assert.That(near, Is.LessThan(1f));
            Assert.That(far, Is.LessThan(near));
            Assert.That(far, Is.GreaterThan(0f));
        }

        [Test]
        public void Step_AMemberLeavingItsFormation_DissolvesWithItsPartner_InsteadOfVanishing()
        {
            Add(0f, 0f);
            var leaver = Add(Pitch, 0f);
            Step(Settled);

            leaver.InFormation = false;
            Assert.AreEqual(2, Step(Dissolve * 0.5f), "both rings still drawn half way");
            Assert.AreEqual(0.5f, _points[0].w, 1e-4f);
            Assert.AreEqual(1f, _points[0].z, 1e-4f, "the brightness holds while it dissolves");

            Assert.AreEqual(0, Step(Dissolve * 0.5f));
        }

        [Test]
        public void Step_ARingFollowsItsLeavingMember_AsItDissolves()
        {
            Add(0f, 0f);
            var leaver = Add(Pitch, 0f);
            Step(Settled);

            leaver.InFormation = false;
            leaver.Centre = new Vector2(Pitch, -1f);
            Step(Dissolve * 0.5f);

            Assert.That(Has(new Vector2(Pitch, -1f)), Is.True);
        }

        [Test]
        public void Step_APoppedMember_LeavesItsRingDissolvingWhereItWas()
        {
            Add(0f, 0f);
            var popped = Add(Pitch, 0f);
            Step(Settled);

            _sources.Remove(popped);
            Assert.AreEqual(2, Step(Dissolve * 0.5f));
            Assert.That(Has(new Vector2(Pitch, 0f)), Is.True);

            Assert.AreEqual(0, Step(Dissolve * 0.5f));
        }

        [Test]
        public void Step_TheBoardCleared_DissolvesEveryRing()
        {
            Add(0f, 0f);
            Add(Pitch, 0f);
            Add(Pitch * 2f, 0f);
            Step(Settled);

            _sources.Clear();
            Assert.AreEqual(3, Step(Dissolve * 0.25f));
            Assert.AreEqual(0, Step(Dissolve));
        }

        [Test]
        public void Step_AMemberReconnectingMidDissolve_GrowsBackFromWhereItWas()
        {
            Add(0f, 0f);
            var member = Add(Pitch, 0f);
            Step(Settled);

            member.InFormation = false;
            Step(Dissolve * 0.5f);
            member.InFormation = true;
            Step(Dissolve * 0.25f);

            Assert.AreEqual(0.75f, _points[0].w, 1e-4f);
        }

        [Test]
        public void Step_WithTimeStopped_HoldsADissolvingRing()
        {
            Add(0f, 0f);
            var member = Add(Pitch, 0f);
            Step(Settled);

            member.InFormation = false;
            Step(Dissolve * 0.5f);
            Step(0f);

            Assert.AreEqual(0.5f, _points[0].w, 1e-4f);
        }

        [Test]
        public void Sources_ALookSpawningAgain_StartsAFreshRing_AndLeavesItsOldOneDissolving()
        {
            var sources = new FormationGlowSources();
            var look = new FakeSource { Centre = new Vector2(Pitch, 0f), InFormation = true };
            sources.Add(new FakeSource { Centre = Vector2.zero, InFormation = true });
            var first = sources.Add(look);
            _field.Step(sources.All, _settings, Settled, _points, _formations, out _);

            // Popped and spawned again from the pool before the view's next frame.
            first.Dispose();
            look.Centre = new Vector2(-Pitch, 0f);
            sources.Add(look);
            _count = _field.Step(sources.All, _settings, Dissolve * 0.5f, _points, _formations, out _);

            Assert.AreEqual(3, _count);
            Assert.That(Has(new Vector2(Pitch, 0f)), Is.True, "the old ring dissolves where it was");
            Assert.That(Has(new Vector2(-Pitch, 0f)), Is.True, "the new one grows in");
        }

        [Test]
        public void Step_TheBoundsCoverTheRingsAndTheirLight()
        {
            Add(1f, 2f);
            Add(1f + Pitch, 2f);
            _field.Step(_sources, _settings, Settled, _points, _formations, out var bounds);

            var margin = Reach + RingWidth * 3f;
            Assert.AreEqual(1f - margin, bounds.xMin, 1e-5f);
            Assert.AreEqual(1f + Pitch + margin, bounds.xMax, 1e-5f);
            Assert.AreEqual(2f - margin, bounds.yMin, 1e-5f);
            Assert.AreEqual(2f + margin, bounds.yMax, 1e-5f);
        }

        [Test]
        public void Step_StopsAtTheShadersArraySize()
        {
            for (var i = 0; i < FormationGlowField.MaxPoints + 6; i++)
            {
                Add(i * Pitch, 0f);
            }

            Assert.AreEqual(FormationGlowField.MaxPoints, Step(Settled));
        }

        [Test]
        public void Rings_OfNeighboursInALine_Join()
        {
            Add(0f, 0f);
            Add(Pitch, 0f);
            Step(Settled);

            var midpoint = FormationGlowField.FieldAt(_points, 2, new Vector2(Pitch * 0.5f, 0f), Reach, _threshold);
            Assert.That(midpoint, Is.GreaterThan(_threshold));
        }

        [Test]
        public void Rings_TwoPitchesApart_StaySeparate()
        {
            Add(0f, 0f);
            Add(Pitch * 2f, 0f);
            Step(Settled);

            var midpoint = FormationGlowField.FieldAt(_points, 2, new Vector2(Pitch, 0f), Reach, _threshold);
            Assert.That(midpoint, Is.LessThan(_threshold));
        }

        [Test]
        public void Rings_ADissolvingNeighbour_EasesOffTheMerge()
        {
            Add(0f, 0f);
            var member = Add(Pitch, 0f);
            Step(Settled);
            var midpoint = new Vector2(Pitch * 0.5f, 0f);
            var whole = FormationGlowField.FieldAt(_points, 2, midpoint, Reach, _threshold);

            member.InFormation = false;
            Step(Dissolve * 0.5f);
            var half = FormationGlowField.FieldAt(_points, 2, midpoint, Reach, _threshold);
            Step(Dissolve * 0.49f);
            var nearlyGone = FormationGlowField.FieldAt(_points, 2, midpoint, Reach, _threshold);

            Assert.That(half, Is.LessThan(whole));
            Assert.That(nearlyGone, Is.LessThan(half));
        }

        // Two formations a slot apart, end to end. Their rings overlap where they meet (a ring is wider than half
        // a slot), so the telling point is beside the gap, in the neck one formation's rings would fill between
        // two members: past either end member's own ring, and inside the outline their two fields make together.
        [Test]
        public void TwoFormations_SideBySide_KeepAnOutlineEach()
        {
            Add(0f, 0f, formation: 1);
            Add(Pitch, 0f, formation: 1);
            Add(Pitch * 2f, 0f, formation: 2);
            Add(Pitch * 3f, 0f, formation: 2);
            Step(Settled);

            var neck = new Vector2(Pitch * 1.5f, 0.4f);
            Assert.That(Vector2.Distance(neck, new Vector2(Pitch, 0f)), Is.GreaterThan(RingRadius), "past a lone ring");
            var asOne = FormationGlowField.FieldAt(_points, _count, neck, Reach, _threshold);
            var first = FormationGlowField.FieldAt(_points, _count, neck, Reach, _threshold, _formations, 1f);
            var second = FormationGlowField.FieldAt(_points, _count, neck, Reach, _threshold, _formations, 2f);

            Assert.That(asOne, Is.GreaterThan(_threshold), "as one formation the neck would be inside the outline");
            Assert.That(first, Is.LessThan(_threshold), "the first formation's outline does not reach into the neck");
            Assert.That(second, Is.LessThan(_threshold), "nor does the second's");
        }

        [Test]
        public void Rings_AreWrittenFormationByFormation_HoweverTheSourcesAreListed()
        {
            Add(0f, 0f, formation: 2);
            Add(5f, 0f, formation: 1);
            Add(Pitch, 0f, formation: 2);
            Add(5f + Pitch, 0f, formation: 1);
            Add(Pitch * 2f, 0f, formation: 2);
            Step(Settled);

            Assert.AreEqual(5, _count);
            CollectionAssert.AreEqual(new[] { 1f, 1f, 2f, 2f, 2f }, new ArraySegment<float>(_formations, 0, _count));
            for (var i = 0; i < _count; i++)
            {
                Assert.AreEqual(_formations[i] == 1f, _points[i].x >= 5f, $"ring {i} keeps its own centre");
            }
        }

        // Beside another formation's member but with none of its own: nothing to draw together with.
        [Test]
        public void AMember_WithOnlyAnotherFormationsMemberNear_LightsNoRing()
        {
            Add(0f, 0f, formation: 1);
            Add(Pitch, 0f, formation: 2);

            Assert.AreEqual(0, Step(Settled));
        }

        // Its formation breaks while a neighbouring one stands: the ring it leaves dissolves as its own
        // formation's, not into the neighbour's outline.
        [Test]
        public void ADissolvingRing_KeepsItsFormation()
        {
            var left = Add(0f, 0f, formation: 1);
            var leftFellow = Add(-Pitch, 0f, formation: 1);
            Add(Pitch, 0f, formation: 2);
            Add(Pitch * 2f, 0f, formation: 2);
            Step(Settled);

            left.InFormation = false;
            leftFellow.InFormation = false;
            Step(Dissolve * 0.5f);

            Assert.AreEqual(4, _count);
            CollectionAssert.AreEqual(new[] { 1f, 1f, 2f, 2f }, new ArraySegment<float>(_formations, 0, _count));
        }

        private FakeSource Add(float x, float y, bool inFormation = true, int formation = 0)
        {
            var source = new FakeSource { Centre = new Vector2(x, y), InFormation = inFormation, Formation = formation };
            _sources.Add(source);
            return source;
        }

        private int Step(float seconds)
        {
            _count = _field.Step(_sources, _settings, seconds, _points, _formations, out _);
            return _count;
        }

        private bool Has(Vector2 centre)
        {
            for (var i = 0; i < _count; i++)
            {
                if (((Vector2)_points[i] - centre).sqrMagnitude < 1e-6f)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class FakeSource : IFormationGlowSource
        {
            public Vector2 Centre;
            public bool InFormation;
            public int Formation;

            public Vector2 GlowCentre => Centre;
            public bool GlowsInFormation => InFormation;
            public int GlowFormation => Formation;
        }
    }
}

using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Slots.Actor.Archetype;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class ContentRampTests
    {
        [Test]
        public void Roll_OnUnlockLevelWithIntroWeight_ReturnsTheIntroWeight()
        {
            var ramp = new ContentWeightRamp(1f, 1f, 3f, 3f, 5, introWeight: 9f);

            Assert.AreEqual(9f, ramp.Roll(4, 4, new System.Random(1)));
        }

        [Test]
        public void Roll_IntroWeightAppliesOnlyOnTheUnlockLevel()
        {
            var ramp = new ContentWeightRamp(1f, 1f, 3f, 3f, 5, introWeight: 9f);

            Assert.AreEqual(1.4f, ramp.Roll(5, 4, new System.Random(1)), 0.0001f, "one level into a 5-level ramp");
        }

        [Test]
        public void Roll_NoIntroWeight_RollsTheEarlyRangeOnTheUnlockLevel()
        {
            var ramp = new ContentWeightRamp(1f, 2f, 5f, 6f, 4);

            for (var seed = 0; seed < 20; seed++)
            {
                Assert.That(ramp.Roll(3, 3, new System.Random(seed)), Is.InRange(1f, 2f));
            }
        }

        [Test]
        public void Roll_AtTheRampMidpoint_RollsTheLerpedRange()
        {
            // Early 1..1, late 3..3 over 6 levels: halfway is exactly 2.
            var ramp = new ContentWeightRamp(1f, 1f, 3f, 3f, 6);

            Assert.AreEqual(2f, ramp.Roll(4, 1, new System.Random(1)), 0.0001f);
        }

        [Test]
        public void Roll_MidpointRange_LiesBetweenTheLerpedBounds()
        {
            // Early 0..2, late 4..6: halfway the range is 2..4.
            var ramp = new ContentWeightRamp(0f, 2f, 4f, 6f, 4);

            for (var seed = 0; seed < 20; seed++)
            {
                Assert.That(ramp.Roll(3, 1, new System.Random(seed)), Is.InRange(2f, 4f));
            }
        }

        [Test]
        public void Roll_AfterTheRamp_RollsTheLateRange()
        {
            var ramp = new ContentWeightRamp(1f, 1f, 3f, 3f, 4);

            Assert.AreEqual(3f, ramp.Roll(5, 1, new System.Random(1)), 0.0001f);
            Assert.AreEqual(3f, ramp.Roll(500, 1, new System.Random(1)), 0.0001f);
        }

        [Test]
        public void Roll_RampOfZero_IsTheLateRangeAtOnce()
        {
            var ramp = new ContentWeightRamp(1f, 1f, 3f, 3f, 0);

            Assert.AreEqual(3f, ramp.Roll(2, 2, new System.Random(1)), 0.0001f);
        }

        [Test]
        public void Roll_BeforeUnlock_ClampsToTheEarlyRange()
        {
            var ramp = new ContentWeightRamp(1f, 1f, 3f, 3f, 4);

            Assert.AreEqual(1f, ramp.Roll(1, 5, new System.Random(1)), 0.0001f);
        }

        [Test]
        public void Roll_AlwaysDrawsExactlyOnce_WhateverThePath()
        {
            var intro = new ContentWeightRamp(1f, 1f, 3f, 3f, 4, introWeight: 2f);
            var ranged = new ContentWeightRamp(1f, 2f, 3f, 4f, 4);
            var flat = ContentWeightRamp.Flat(1f, 1f);

            foreach (var (ramp, level) in new[] { (intro, 2), (ranged, 2), (ranged, 9), (flat, 2) })
            {
                var rolled = new System.Random(5);
                ramp.Roll(level, 2, rolled);
                var reference = new System.Random(5);
                reference.NextDouble();

                Assert.AreEqual(reference.NextDouble(), rolled.NextDouble());
            }
        }

        [Test]
        public void Flat_IsTheSameRangeAtEveryLevel()
        {
            var flat = ContentWeightRamp.Flat(2f, 2f);

            Assert.AreEqual(2f, flat.Roll(1, 1, new System.Random(1)));
            Assert.AreEqual(2f, flat.Roll(40, 1, new System.Random(1)));
        }

        [Test]
        public void RampProgress_RunsFromZeroAtUnlockToOneAfterTheRamp()
        {
            Assert.AreEqual(0f, ContentWeightRamp.RampProgress(3, 3, 4), 0.0001f);
            Assert.AreEqual(0.5f, ContentWeightRamp.RampProgress(5, 3, 4), 0.0001f);
            Assert.AreEqual(1f, ContentWeightRamp.RampProgress(7, 3, 4), 0.0001f);
            Assert.AreEqual(1f, ContentWeightRamp.RampProgress(70, 3, 4), 0.0001f);
            Assert.AreEqual(0f, ContentWeightRamp.RampProgress(1, 3, 4), 0.0001f, "before unlock clamps");
        }

        [Test]
        public void RampProgress_NoRamp_IsOneThroughout()
        {
            Assert.AreEqual(1f, ContentWeightRamp.RampProgress(3, 3, 0), 0.0001f);
        }

        [Test]
        public void PeakWeight_IsTheHighestOfEarlyLateAndIntro()
        {
            Assert.AreEqual(4f, new ContentWeightRamp(1f, 2f, 3f, 4f, 2).PeakWeight);
            Assert.AreEqual(5f, new ContentWeightRamp(1f, 2f, 3f, 4f, 2, 5f).PeakWeight);
            Assert.AreEqual(2f, new ContentWeightRamp(1f, 2f, 0f, 0f, 2).PeakWeight);
        }

        [Test]
        public void IsValid_RejectsNoWeightAndInvertedRanges()
        {
            Assert.IsTrue(ContentWeightRamp.Flat(1f, 1f).IsValid());
            Assert.IsFalse(ContentWeightRamp.Flat(0f, 0f).IsValid(), "never spawns");
            Assert.IsFalse(ContentWeightRamp.Flat(2f, 1f).IsValid());
            Assert.IsFalse(new ContentWeightRamp(1f, 1f, 3f, 2f, 2).IsValid(), "late range inverted");
            Assert.IsTrue(new ContentWeightRamp(0f, 0f, 0f, 0f, 0, 1f).IsValid(), "intro weight alone is a weight");
        }

        [Test]
        public void ActorToGate_AtUnlock_UsesTheEarlyCount()
        {
            var gate = Puff().ToGate(3);

            Assert.AreEqual(7, gate.Count.Min);
            Assert.AreEqual(9, gate.Count.Max);
        }

        [Test]
        public void ActorToGate_MidRamp_LerpsMinAndMax()
        {
            // 4-level ramp from level 3: level 5 is halfway, (7+11)/2 = 9 and (9+13)/2 = 11.
            var gate = Puff().ToGate(5);

            Assert.AreEqual(9, gate.Count.Min);
            Assert.AreEqual(11, gate.Count.Max);
        }

        [Test]
        public void ActorToGate_AfterTheRamp_UsesTheLateCount()
        {
            var gate = Puff().ToGate(40);

            Assert.AreEqual(11, gate.Count.Min);
            Assert.AreEqual(13, gate.Count.Max);
        }

        [Test]
        public void ActorToGate_RampOfZero_UsesTheLateCountAtOnce()
        {
            var entry = new GridActorContentEntry(GridActorType.Puff, 3, 1, new RangedInt(1, 2), new RangedInt(8, 9), 0, 5);

            var gate = entry.ToGate(3);

            Assert.AreEqual(8, gate.Count.Min);
            Assert.AreEqual(9, gate.Count.Max);
        }

        [Test]
        public void ActorToGate_IsRandomMode_AndCarriesTypeAndClusterCap()
        {
            var gate = Puff().ToGate(3);

            Assert.AreEqual(RangeMode.Random, gate.Count.Mode);
            Assert.AreEqual(GridActorType.Puff, gate.Type);
            Assert.AreEqual(5, gate.MaxPerCluster);
        }

        private static GridActorContentEntry Puff()
        {
            return new GridActorContentEntry(GridActorType.Puff, 3, 1, new RangedInt(7, 9), new RangedInt(11, 13), 4, 5);
        }
    }
}

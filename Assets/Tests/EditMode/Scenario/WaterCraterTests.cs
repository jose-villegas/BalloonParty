using BalloonParty.Scenario.View;
using BalloonParty.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Scenario
{
    [TestFixture]
    public class WaterCraterTests
    {
        private const float StepSeconds = 1f / 120f;
        private const float EdgeU = WaterCrater.EdgeInSpreads * WaterCrater.EdgeInSpreads;

        [Test]
        public void From_ABlast_IsAsWideAsItsRadiusAndAsDeepAsItsStrength()
        {
            var bomb = WaterCrater.From(new ImpactEventBus.Blast(1.25f, 1f, 0.6f), 1f, 3f, StepSeconds);
            var nuke = WaterCrater.From(new ImpactEventBus.Blast(3f, 1.5f, 1f), 1f, 3f, StepSeconds);

            Assert.AreEqual(1.25f, bomb.Radius, 1e-5f);
            Assert.AreEqual(3f, bomb.Depth, 1e-5f);
            Assert.AreEqual(0.6f, bomb.Seconds, 1e-5f);
            Assert.AreEqual(3f, nuke.Radius, 1e-5f);
            Assert.AreEqual(4.5f, nuke.Depth, 1e-5f);
            Assert.AreEqual(1f, nuke.Seconds, 1e-5f);
        }

        [Test]
        public void From_TheMaterialsScales_WidenAndDeepenEveryCrater()
        {
            var crater = WaterCrater.From(new ImpactEventBus.Blast(2f, 1.5f, 0.2f), 0.5f, 2f, StepSeconds);

            Assert.AreEqual(1f, crater.Radius, 1e-5f);
            Assert.AreEqual(3f, crater.Depth, 1e-5f);
        }

        [Test]
        public void From_ABlastWithNoSeconds_StillTakesAStepToDig()
        {
            var crater = WaterCrater.From(new ImpactEventBus.Blast(1f, 1f, 0f), 1f, 1.8f, StepSeconds);

            Assert.AreEqual(StepSeconds, crater.Seconds, 1e-6f);
        }

        [Test]
        public void DugBy_RunsFromNothingToAllOfIt_AndNeverBack()
        {
            var crater = new WaterCrater(1f, 1f, 0.4f);

            Assert.AreEqual(0f, crater.DugBy(0f), 1e-6f);
            Assert.AreEqual(1f, crater.DugBy(0.4f), 1e-6f);
            Assert.AreEqual(1f, crater.DugBy(3f), 1e-6f);

            var last = 0f;
            for (var age = 0f; age <= 0.5f; age += 0.01f)
            {
                var dug = crater.DugBy(age);
                Assert.GreaterOrEqual(dug, last);
                last = dug;
            }
        }

        // A blast is hardest at its start: the crater is mostly open in the first part of the dig, and the rest only
        // holds it open against the water flooding back.
        [Test]
        public void DugBy_DigsMostOfTheCraterEarly_AndEasesOff()
        {
            var crater = new WaterCrater(1f, 1f, 0.4f);

            Assert.AreEqual(0.75f, crater.DugBy(0.2f), 1e-5f);
            var firstTenth = crater.DugBy(0.04f);
            var lastTenth = 1f - crater.DugBy(0.36f);
            Assert.Greater(firstTenth, 10f * lastTenth);
        }

        // The buffer digs the share the ease has come on since the last steps ran, so the shares of any run of
        // frames, long or short or with gaps, add up to the whole crater.
        [TestCase(1f / 120f)]
        [TestCase(1f / 30f)]
        [TestCase(0.25f)]
        public void DugBy_TheSharesOfAnyFrameCadence_AddUpToTheWholeCrater(float frameSeconds)
        {
            var crater = new WaterCrater(1f, 1f, 0.4f);

            var total = 0f;
            var dug = 0f;
            for (var age = frameSeconds; dug < 1f; age += frameSeconds)
            {
                var now = crater.DugBy(age);
                total += now - dug;
                dug = now;
            }

            Assert.AreEqual(1f, total, 1e-5f);
        }

        // What the step shader does at the blast's middle with the whole potential: the cell loses the potential's
        // difference to each of its four neighbours, one texel away.
        [TestCase(1.25f, 1.8f)]
        [TestCase(3f, 3.6f)]
        public void PotentialScale_DigsTheMiddleByTheDepth(float radius, float depth)
        {
            const float texelsPerUnit = 16f;
            var crater = new WaterCrater(radius, depth, 0.1f);
            var spreadTexels = crater.Spread * texelsPerUnit;
            var oneTexelAway = 1f / (spreadTexels * spreadTexels);

            var lost = crater.PotentialScale(texelsPerUnit)
                       * 4f * (WaterCrater.Potential(0f) - WaterCrater.Potential(oneTexelAway));

            Assert.AreEqual(depth, lost, depth * 0.02f);
        }

        [Test]
        public void From_ABlastWithALine_IsAFurrowAlongIt()
        {
            var line = new Vector2(0f, 0.2f);
            var furrow = WaterCrater.From(new ImpactEventBus.Blast(0.35f, 0.5f, 0.02f, line), 0.85f, 3f, StepSeconds);
            var crater = WaterCrater.From(new ImpactEventBus.Blast(0.35f, 0.5f, 0.02f), 0.85f, 3f, StepSeconds);

            Assert.IsTrue(furrow.Runs);
            Assert.AreEqual(line, furrow.Line);
            Assert.AreEqual(0.02f, furrow.Seconds, 1e-6f, "a short stretch is run in the blast's own time");
            Assert.IsFalse(crater.Runs);
        }

        // A lance flies faster than the simulation can follow with a furrow as narrow as its heart: run in the
        // step's time, the dig would land once a step, a row of holes. So a long stretch is given the time to be
        // run no faster than the dig's own width allows, and the wide wave's furrow, which needs none, keeps time.
        [Test]
        public void From_AStretchTooLongToRunInItsTime_IsGivenTheTimeToBeDugWithoutGaps()
        {
            var stretch = new Vector2(0f, 1.86f);
            var heart = WaterCrater.From(new ImpactEventBus.Blast(0.35f, 0.5f, 0.02f, stretch), 0.85f, 3f, StepSeconds);
            var wave = WaterCrater.From(new ImpactEventBus.Blast(1.75f, 0.17f, 0.02f, stretch), 0.85f, 3f, StepSeconds);

            var farthestAStep = stretch.magnitude / (heart.Seconds / StepSeconds);
            Assert.Greater(heart.Seconds, 0.02f);
            Assert.AreEqual(heart.Spread * WaterCrater.MaxSpreadsAStep, farthestAStep, 1e-4f);
            Assert.AreEqual(0.02f, wave.Seconds, 1e-6f);
        }

        // A furrow opens in the order its line was flown: the dig starts at the line's first end, is at its middle
        // (the blast's own position) half way through and at its last end when done, at a steady pace.
        [Test]
        public void AFurrowsDig_RunsItsLineFromEndToEnd_AtASteadyPace()
        {
            var furrow = new WaterCrater(0.3f, 2f, 0.2f, new Vector2(2f, 4f));

            Assert.AreEqual(0.25f, furrow.DugBy(0.05f), 1e-5f);
            Assert.AreEqual(0.5f, furrow.DugBy(0.1f), 1e-5f);
            Assert.AreEqual(1f, furrow.DugBy(0.3f), 1e-6f);
            Assert.AreEqual(new Vector2(-1f, -2f), furrow.OffsetAt(0f));
            Assert.AreEqual(Vector2.zero, furrow.OffsetAt(0.5f));
            Assert.AreEqual(new Vector2(1f, 2f), furrow.OffsetAt(1f));
        }

        [Test]
        public void ACratersDig_StaysWhereItIs()
        {
            var crater = new WaterCrater(1f, 2f, 0.6f);

            Assert.AreEqual(Vector2.zero, crater.OffsetAt(0f));
            Assert.AreEqual(Vector2.zero, crater.OffsetAt(1f));
        }

        // What the step shader does under a lance's path over a whole flight: stretch after stretch, each dig runs
        // its own and takes its share at a point of the path by how far from it the dig stands. However the path is
        // cut into stretches (a fast lance's long ones, a slow one's short ones), the point ends the depth down.
        [TestCase(0.3f, 1.6f, 1.86f)]
        [TestCase(0.3f, 1.6f, 0.4f)]
        [TestCase(0.3f, 1.6f, 0.05f)]
        [TestCase(1.5f, 0.5f, 1.86f)]
        [TestCase(1.5f, 0.5f, 0.2f)]
        public void PotentialScale_OfAFurrow_DigsThePathByTheDepth_HoweverItIsCutIntoStretches(
            float radius, float depth, float stretchLength)
        {
            const float texelsPerUnit = 16f;
            const int stepsAStretch = 60;
            var stretch = new WaterCrater(radius, depth, 0.02f, new Vector2(stretchLength, 0f));
            var spreadTexels = stretch.Spread * texelsPerUnit;
            var scale = stretch.PotentialScale(texelsPerUnit);
            // Far enough each way that what lies beyond digs nothing here.
            var stretches = Mathf.CeilToInt(8f * stretch.Spread / stretchLength);

            var lost = 0f;
            for (var index = -stretches; index <= stretches; index++)
            {
                for (var step = 0; step < stepsAStretch; step++)
                {
                    var at = index * stretchLength + stretch.OffsetAt((step + 0.5f) / stepsAStretch).x;
                    var away = at / stretch.Spread;
                    // The potential's Laplacian at the middle of a dig is 4/3 over its spread squared; away from
                    // it, that times the profile.
                    lost += scale / stepsAStretch * 4f / (3f * spreadTexels * spreadTexels) * -WaterCrater.Profile(away * away);
                }
            }

            Assert.AreEqual(depth, lost, depth * 0.02f);
        }

        // A long frame runs several of the shot's fixed steps at once, each reporting its stretch: joined, they are one
        // furrow from the first stretch's start to the last one's end, run in the time of all of them.
        [Test]
        public void TryJoin_StretchesThatCarryStraightOn_AreOneFurrow()
        {
            var first = new WaterCrater(0.3f, 1.5f, 0.02f, new Vector2(0f, 1f));
            var next = new WaterCrater(0.3f, 1.5f, 0.03f, new Vector2(0f, 2f));

            Assert.IsTrue(WaterCrater.TryJoin(
                new Vector2(1f, 0.5f), first, new Vector2(1f, 2f), next, out var origin, out var joined));

            Assert.That(Vector2.Distance(new Vector2(1f, 1.5f), origin), Is.LessThan(1e-5f));
            Assert.That(Vector2.Distance(new Vector2(0f, 3f), joined.Line), Is.LessThan(1e-5f));
            Assert.AreEqual(0.05f, joined.Seconds, 1e-6f);
            Assert.AreEqual(1.5f, joined.Depth, 1e-6f);
            Assert.AreEqual(
                first.PotentialScale(16f) + next.PotentialScale(16f), joined.PotentialScale(16f), 1e-3f,
                "joined, the furrow digs what its stretches would have");
        }

        [Test]
        public void TryJoin_LeavesApart_ATurn_AGap_AnotherWidth_AndCraters()
        {
            var up = new WaterCrater(0.3f, 1.5f, 0.02f, new Vector2(0f, 1f));
            var across = new WaterCrater(0.3f, 1.5f, 0.02f, new Vector2(1f, 0f));
            var wide = new WaterCrater(1.5f, 0.4f, 0.02f, new Vector2(0f, 1f));
            var crater = new WaterCrater(0.3f, 1.5f, 0.02f);
            var start = new Vector2(1f, 0.5f);

            Assert.IsFalse(WaterCrater.TryJoin(start, up, new Vector2(1.5f, 1f), across, out _, out _), "a bounce");
            Assert.IsFalse(WaterCrater.TryJoin(start, up, new Vector2(1f, 2.5f), up, out _, out _), "a gap");
            Assert.IsFalse(WaterCrater.TryJoin(start, up, new Vector2(1f, 1.5f), wide, out _, out _), "the wave");
            Assert.IsFalse(WaterCrater.TryJoin(start, up, new Vector2(1f, 1f), crater, out _, out _));
            Assert.IsFalse(WaterCrater.TryJoin(start, crater, start, crater, out _, out _));
        }

        // A lance that stalls must not dig a hole where it stands: a furrow digs in proportion to the way flown.
        [Test]
        public void PotentialScale_OfAFurrow_IsInProportionToItsLength()
        {
            var stretch = new WaterCrater(0.3f, 1.6f, 0.02f, new Vector2(0.4f, 0f));
            var half = new WaterCrater(0.3f, 1.6f, 0.02f, new Vector2(0.2f, 0f));
            var crawl = new WaterCrater(0.3f, 1.6f, 0.02f, new Vector2(0.001f, 0f));

            Assert.AreEqual(stretch.PotentialScale(16f) * 0.5f, half.PotentialScale(16f), 1e-4f);
            Assert.Less(crawl.PotentialScale(16f), stretch.PotentialScale(16f) * 0.01f);
        }

        [Test]
        public void Profile_IsFlatOverTheInnerHalf_ZeroAtTheEdge_AndARimPastIt()
        {
            Assert.AreEqual(-1f, WaterCrater.Profile(0f), 1e-6f);
            Assert.Less(WaterCrater.Profile(EdgeU * 0.25f), -0.9f, "half way to the edge is still nearly as deep");
            Assert.AreEqual(0f, WaterCrater.Profile(EdgeU), 1e-3f);

            var rim = 0f;
            for (var u = EdgeU; u < 12f; u += 0.05f)
            {
                var height = WaterCrater.Profile(u);
                Assert.Greater(height, -1e-3f, "past the edge nothing is dug");
                rim = Mathf.Max(rim, height);
            }

            Assert.AreEqual(0.35f, rim, 0.03f, "the rim stands about a third as tall as the crater is deep");
        }

        // The profile is what moving water down the potential's slope leaves: its Laplacian, over the plane. For a
        // function of u (the distance squared) that is 4(f' + u f''), and the profile is scaled to -1 at the middle,
        // where it is -4/3.
        [TestCase(0.1f)]
        [TestCase(0.8f)]
        [TestCase(2.27f)]
        [TestCase(4f)]
        [TestCase(7f)]
        public void Profile_IsThePotentialsLaplacian(float u)
        {
            const float h = 0.05f;
            var first = (WaterCrater.Potential(u + h) - WaterCrater.Potential(u - h)) / (2f * h);
            var second = (WaterCrater.Potential(u + h) - 2f * WaterCrater.Potential(u) + WaterCrater.Potential(u - h))
                         / (h * h);

            Assert.AreEqual(3f * (first + u * second), WaterCrater.Profile(u), 5e-3f);
        }

        // The rim holds exactly what the crater lost: summed over the plane (rings of radius s, s being sqrt(u)) the
        // profile comes to nothing.
        [Test]
        public void Profile_TheRimHoldsWhatTheCraterLost()
        {
            const float step = 0.001f;
            var dug = 0f;
            var piled = 0f;
            for (var s = step * 0.5f; s < 8f; s += step)
            {
                var ring = WaterCrater.Profile(s * s) * s * step;
                if (ring < 0f)
                {
                    dug -= ring;
                }
                else
                {
                    piled += ring;
                }
            }

            Assert.Greater(dug, 0.5f);
            Assert.AreEqual(dug, piled, dug * 0.005f);
        }
    }
}

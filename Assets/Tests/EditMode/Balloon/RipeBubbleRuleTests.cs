using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class RipeBubbleRuleTests
    {
        // The shipped BurstSeconds: the whole fold, hit to pop.
        private const float FoldBurst = 0.22f;

        [TestCase(5, 5, true)]
        [TestCase(6, 5, true)]
        [TestCase(4, 5, false)]
        [TestCase(1, 1, true)]
        [TestCase(3, 0, false)]
        public void IsRipe_AtTheCap(int bubbles, int max, bool ripe)
        {
            Assert.AreEqual(ripe, RipeBubbleRule.IsRipe(bubbles, max));
        }

        [TestCase(10f, 4f, 6f)]
        [TestCase(10f, 10f, 0f)]
        [TestCase(10f, 12f, 0f)]
        public void FuseLeft_CountsDownToZeroAndNoFurther(float deadline, float now, float left)
        {
            Assert.AreEqual(left, RipeBubbleRule.FuseLeft(deadline, now));
        }

        [Test]
        public void RippleTakes_APlainBalloon()
        {
            Assert.IsTrue(RipeBubbleRule.RippleTakes(new BalloonModel()));
        }

        [Test]
        public void RippleTakes_NoRewardBalloon()
        {
            var heart = new HeartBalloonModel(new BalloonModelConfig(typeName: BalloonType.Heart));

            Assert.IsFalse(RipeBubbleRule.RippleTakes(heart));
        }

        [Test]
        public void RippleTakes_NoUnbreakable()
        {
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable));

            Assert.IsFalse(RipeBubbleRule.RippleTakes(unbreakable));
        }

        [Test]
        public void RippleTakes_AToughOnlyOnItsLastHit()
        {
            // With more than one hit left a tough bounces an ordinary hit, as the unbreakable does.
            var tough = new ToughBalloonModel(new BalloonModelConfig(typeName: BalloonType.Tough, hitsToPop: 2));
            Assert.IsFalse(RipeBubbleRule.RippleTakes(tough));

            tough.HitsRemaining.Value = 1;
            Assert.IsTrue(RipeBubbleRule.RippleTakes(tough));
        }

        [Test]
        public void RippleTakes_ClustersRipeOrNot()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());
            var cluster = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: 3), palette);
            Assert.IsTrue(RipeBubbleRule.RippleTakes(cluster));

            cluster.SetBubbles(5);
            cluster.Ripen();
            Assert.IsTrue(RipeBubbleRule.RippleTakes(cluster));
        }

        [Test]
        public void RippleTakes_NoEmptySlot()
        {
            Assert.IsFalse(RipeBubbleRule.RippleTakes(null));
        }

        [Test]
        public void RippleTakes_NoActorThatIsNotABalloon()
        {
            // A grid actor: one that cannot be hit, and the absorber, which can but would end the shot's turn.
            Assert.IsFalse(RipeBubbleRule.RippleTakes(Substitute.For<ISlotActor>()));
            Assert.IsFalse(RipeBubbleRule.RippleTakes(new AbsorberActorModel()));
        }

        [Test]
        public void FoamTakes_APlainBalloonBelowTheMax()
        {
            var balloon = new BalloonModel();
            Assert.IsTrue(RipeBubbleRule.FoamTakes(balloon, 1));

            balloon.TryCoat(1);
            Assert.IsFalse(RipeBubbleRule.FoamTakes(balloon, 1), "one already at the max takes none");
            Assert.IsTrue(RipeBubbleRule.FoamTakes(balloon, 2));
        }

        [Test]
        public void FoamTakes_AnItemCarrierAndARainbow()
        {
            var carrier = new BalloonModel();
            carrier.Item.Value = ItemType.Bomb;
            var rainbow = new BalloonModel(new BalloonModelConfig(typeName: BalloonType.Rainbow));
            rainbow.Color.Value = GamePalette.RainbowColorId;

            Assert.IsTrue(RipeBubbleRule.FoamTakes(carrier, 1));
            Assert.IsTrue(RipeBubbleRule.FoamTakes(rainbow, 1));
        }

        [Test]
        public void FoamTakes_NoCluster_RipeOrNot()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());
            var cluster = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: 3), palette);
            Assert.IsFalse(RipeBubbleRule.FoamTakes(cluster, 1));

            cluster.SetBubbles(5);
            cluster.Ripen();
            Assert.IsFalse(RipeBubbleRule.FoamTakes(cluster, 1));
        }

        [Test]
        public void FoamTakes_NoToughUnbreakableOrRewardBalloon()
        {
            var tough = new ToughBalloonModel(new BalloonModelConfig(typeName: BalloonType.Tough, hitsToPop: 1));
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable));
            var heart = new HeartBalloonModel(new BalloonModelConfig(typeName: BalloonType.Heart));
            var golden = new GoldenBalloonModel(new BalloonModelConfig(typeName: BalloonType.Golden));

            Assert.IsFalse(RipeBubbleRule.FoamTakes(tough, 1));
            Assert.IsFalse(RipeBubbleRule.FoamTakes(unbreakable, 1));
            Assert.IsFalse(RipeBubbleRule.FoamTakes(heart, 1));
            Assert.IsFalse(RipeBubbleRule.FoamTakes(golden, 1));
        }

        // A six-second fuse run down in small steps, with the last two seconds fast: once at each whole second above
        // the window, each half second inside it, never at the start or as it runs out.
        [Test]
        public void Throbs_OnceOnEachBeat_TwiceASecondInTheFastWindow()
        {
            var throbs = new List<float>();
            var fast = new List<float>();
            var before = 6f;
            for (var step = 1; step <= 600; step++)
            {
                var after = (600 - step) / 100f;
                if (RipeBubbleRule.Throbs(before, after, 2f))
                {
                    throbs.Add(after);
                    if (RipeBubbleRule.IsFastThrob(after, 2f))
                    {
                        fast.Add(after);
                    }
                }

                before = after;
            }

            CollectionAssert.AreEqual(new[] { 5f, 4f, 3f, 2f, 1.5f, 1f, 0.5f }, throbs);
            CollectionAssert.AreEqual(new[] { 2f, 1.5f, 1f, 0.5f }, fast);
        }

        [TestCase(3.2f, 3.2f, Description = "the level clock holds")]
        [TestCase(2.9f, 3.1f, Description = "the fuse runs back up")]
        [TestCase(0.4f, 0f, Description = "the fuse runs out: the burst takes over")]
        [TestCase(3.9f, 3.1f, Description = "no beat between")]
        public void Throbs_NotWhenNoBeatIsCrossed(float before, float after)
        {
            Assert.IsFalse(RipeBubbleRule.Throbs(before, after, 2f));
        }

        [Test]
        public void Throbs_AStepAcrossSeveralBeats_ThrobsOnce()
        {
            Assert.IsTrue(RipeBubbleRule.Throbs(4.5f, 1.2f, 2f));
        }

        [TestCase(6f, 6f, 0.35f, 0.35f)]
        [TestCase(3f, 6f, 0.35f, 0.675f)]
        [TestCase(0f, 6f, 0.35f, 1f)]
        [TestCase(8f, 6f, 0.35f, 0.35f)]
        [TestCase(1f, 0f, 0.35f, 0f)]
        [TestCase(3f, 6f, 0f, 0.5f)]
        [TestCase(6f, 6f, 2f, 1f)]
        public void WarnAmount_RunsEvenlyFromItsStartToTheEnd(float left, float fuse, float start, float amount)
        {
            Assert.AreEqual(amount, RipeBubbleRule.WarnAmount(left, fuse, start), 1e-5f);
        }

        [Test]
        public void WarnAmount_ShowsFromTheMomentACusterRipens()
        {
            Assert.Greater(RipeBubbleRule.WarnAmount(6f, 6f, 0.35f), 0.3f);
            Assert.Greater(RipeBubbleRule.WarnAmount(5f, 6f, 0.35f), RipeBubbleRule.WarnAmount(6f, 6f, 0.35f));
        }

        [Test]
        public void FoamTakes_NoGridActorAndNoEmptySlot()
        {
            Assert.IsFalse(RipeBubbleRule.FoamTakes(null, 1));
            Assert.IsFalse(RipeBubbleRule.FoamTakes(Substitute.For<ISlotActor>(), 1));
            Assert.IsFalse(RipeBubbleRule.FoamTakes(new AbsorberActorModel(), 1));
        }

        [Test]
        public void ClusterRoom_IsWhatAMergeCouldStillAdd_AndNothingForANonCluster()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());
            var cluster = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: 3), palette);
            cluster.SetBubbles(4);

            Assert.AreEqual(2, RipeBubbleRule.ClusterRoom(cluster, 6));
            cluster.SetBubbles(6);
            Assert.AreEqual(0, RipeBubbleRule.ClusterRoom(cluster, 6));
            Assert.AreEqual(0, RipeBubbleRule.ClusterRoom(new BalloonModel(), 6));
        }

        [Test]
        public void PlanHandout_OneATakerARound_CoatsBeforeClusters_ThenLeftovers()
        {
            var coats = new[] { 0, 1, 0, 1, 0, 0 };
            var clusters = new[] { 2, 0, 0, 0, 0, 0 };

            var handout = RipeBubbleRule.PlanHandout(6, coats, clusters);

            AssertGifts(handout,
                (FoamGift.Coat, 1), (FoamGift.Coat, 3), (FoamGift.Cluster, 0),
                (FoamGift.Cluster, 0), (FoamGift.Leftover, -1), (FoamGift.Leftover, -1));
            Assert.AreEqual(0b1010, RipeBubbleRule.CoatedNeighbours(handout));
        }

        [Test]
        public void PlanHandout_ACoatOfTwoLayers_TakesASecondBubbleInTheNextRound()
        {
            var handout = RipeBubbleRule.PlanHandout(3, new[] { 2, 0, 0, 0, 0, 1 }, new int[6]);

            AssertGifts(handout, (FoamGift.Coat, 0), (FoamGift.Coat, 5), (FoamGift.Coat, 0));
        }

        [Test]
        public void PlanHandout_NoTakers_AllLeftovers_AndNoMoreThanSix()
        {
            var handout = RipeBubbleRule.PlanHandout(9, new int[6], new int[6]);

            Assert.AreEqual(6, RipeBubbleBurstMessage.HandoutCount(handout));
            for (var bubble = 0; bubble < 6; bubble++)
            {
                Assert.AreEqual(FoamGift.Leftover, RipeBubbleBurstMessage.HandoutGift(handout, bubble, out var d));
                Assert.AreEqual(-1, d);
            }
        }

        [Test]
        public void PlanHandout_NoBubbles_NoHandout()
        {
            Assert.AreEqual(0, RipeBubbleBurstMessage.HandoutCount(
                RipeBubbleRule.PlanHandout(0, new[] { 1, 1, 1, 1, 1, 1 }, new int[6])));
        }

        [TestCase(0f, 6)]
        [TestCase(0.03f, 6)]
        [TestCase(0.033f, 5)]
        [TestCase(0.066f, 4)]
        [TestCase(0.165f, 1)]
        [TestCase(0.21f, 1)]
        [TestCase(1f, 1)]
        public void FoldCount_StepsDownEvenly_OverMostOfTheBurst(float elapsed, int shown)
        {
            // 0.22 s, folding over 0.75 of it: a step every 0.033 s, at one by 0.165 s.
            Assert.AreEqual(shown, RipeBubbleRule.FoldCount(elapsed, FoldBurst, 6));
        }

        [Test]
        public void FoldCount_WithNoBurstTime_IsOneBubble()
        {
            Assert.AreEqual(1, RipeBubbleRule.FoldCount(0f, 0f, 6));
        }

        [Test]
        public void FoldStepSeconds_SharesTheFoldEvenly_AndIsNothingWithNothingToStep()
        {
            Assert.AreEqual(0.033f, RipeBubbleRule.FoldStepSeconds(FoldBurst, 6), 1e-5f);
            Assert.AreEqual(0f, RipeBubbleRule.FoldStepSeconds(FoldBurst, 1));
            Assert.AreEqual(0f, RipeBubbleRule.FoldStepSeconds(0f, 6));
        }

        [Test]
        public void FoldScale_StartsAtRest_GrowsEachStep_AndEndsAsOneBubbleTheClustersSize()
        {
            const float growth = 0.4f;
            var radius = SoapBubbleShader.DefaultBubbleRadius;
            Assert.AreEqual(1f, RipeBubbleRule.FoldScale(6, 6, radius, growth), 1e-5f);
            for (var shown = 5; shown >= 1; shown--)
            {
                Assert.Greater(
                    RipeBubbleRule.FoldScale(shown, 6, radius, growth),
                    RipeBubbleRule.FoldScale(shown + 1, 6, radius, growth));
            }

            // One bubble of the folded scale reaches as far as the ring of six did, grown by the fold's growth.
            var reachOfSix = SoapBubbleShader.LayoutReach(6, radius);
            Assert.AreEqual(
                reachOfSix * RipeBubbleRule.FoldedSize(growth), RipeBubbleRule.FoldScale(1, 6, radius, growth), 1e-4f);
            Assert.AreEqual(1.4f, RipeBubbleRule.FoldedSize(growth), 1e-5f);
            Assert.AreEqual(2.1f, reachOfSix, 0.05f);
        }

        // What the cluster's outline has grown by, the layout's own packing taken out: a fifth of the growth a step.
        [TestCase(6, 1f)]
        [TestCase(5, 1.08f)]
        [TestCase(3, 1.24f)]
        [TestCase(1, 1.4f)]
        public void FoldScale_EachStepAddsAnEvenShareOfTheGrowth(int shown, float grown)
        {
            var radius = SoapBubbleShader.DefaultBubbleRadius;

            var outlineOnly = RipeBubbleRule.FoldScale(shown, 6, radius, 0f);
            Assert.AreEqual(grown, RipeBubbleRule.FoldScale(shown, 6, radius, 0.4f) / outlineOnly, 1e-4f);
        }

        // 0.22 s from six: a step every 0.033 s. The kick is whole the moment a count shows, a quarter of it half
        // a step later, and gone by the next step.
        [TestCase(0f, 1.1f)]
        [TestCase(0.0165f, 1.025f)]
        [TestCase(0.0329f, 1f)]
        [TestCase(0.033f, 1.1f)]
        [TestCase(0.0495f, 1.025f)]
        [TestCase(0.132f, 1.1f)]
        [TestCase(0.165f, 1.1f)]
        [TestCase(0.1815f, 1.025f)]
        public void FoldPulse_KicksOutAsEachCountShows_AndSettlesByTheNext(float elapsed, float pulse)
        {
            Assert.AreEqual(pulse, RipeBubbleRule.FoldPulse(elapsed, FoldBurst, 6, 0.1f), 2e-3f);
        }

        [TestCase(0.198f)]
        [TestCase(0.22f)]
        [TestCase(2f)]
        public void FoldPulse_HoldsSettledOnTheOneBigBubble_UntilThePop(float elapsed)
        {
            Assert.AreEqual(1f, RipeBubbleRule.FoldPulse(elapsed, FoldBurst, 6, 0.1f), 1e-4f);
        }

        // Sampled every millisecond: the hit's own pump, then one for each of the five steps down to one bubble.
        [Test]
        public void AFoldFromSix_PumpsSixTimes()
        {
            const float strength = 0.1f;
            var pumps = 0;
            var before = 1f;
            for (var ms = 0; ms <= 220; ms++)
            {
                var pulse = RipeBubbleRule.FoldPulse(ms / 1000f, FoldBurst, 6, strength);
                if (pulse - before > strength * 0.5f)
                {
                    pumps++;
                }

                before = pulse;
            }

            Assert.AreEqual(6, pumps);
        }

        // Each pump peaks above the one before, and the fold ends on the biggest size it settles at.
        [Test]
        public void AFoldsPumps_EachPeakHigherThanTheLast()
        {
            var radius = SoapBubbleShader.DefaultBubbleRadius;
            var step = RipeBubbleRule.FoldStepSeconds(FoldBurst, 6);
            var lastPeak = 0f;
            for (var taken = 0; taken <= 5; taken++)
            {
                var at = taken * step;
                var shown = RipeBubbleRule.FoldCount(at, FoldBurst, 6);
                // The size of the outline, not of the quad: the scale times how far that count's layout reaches.
                var peak = RipeBubbleRule.FoldScale(shown, 6, radius, 0.4f)
                           * RipeBubbleRule.FoldPulse(at, FoldBurst, 6, 0.08f)
                           * SoapBubbleShader.LayoutReach(shown, radius);
                Assert.Greater(peak, lastPeak, $"pump {taken}");
                lastPeak = peak;
            }
        }

        [Test]
        public void FoldPulse_WithNoStrength_NoBurstTime_OrOneBubble_IsStill()
        {
            Assert.AreEqual(1f, RipeBubbleRule.FoldPulse(0f, FoldBurst, 6, 0f));
            Assert.AreEqual(1f, RipeBubbleRule.FoldPulse(0f, 0f, 6, 0.1f));
            Assert.AreEqual(1f, RipeBubbleRule.FoldPulse(0f, FoldBurst, 1, 0.1f));
        }

        // Slots 0.75 apart, as the game's are: the first ring at 0.75, the second at 1.30 and 1.50.
        [TestCase(0.75f, 0f, 1.35f, true)]
        [TestCase(0.375f, 1.25f, 1.35f, true)]
        [TestCase(1.5f, 0f, 1.35f, false)]
        [TestCase(1.125f, 0.65f, 1.35f, true)]
        [TestCase(0f, 1.3f, 1.35f, true)]
        [TestCase(0f, -1.3f, 1.35f, true)]
        [TestCase(0f, 1.4f, 1.35f, false)]
        [TestCase(0f, 0f, 1.35f, true)]
        public void RippleReaches_ASlotCentreWithinTheRadius(
            float dx, float dy, float radius, bool reaches)
        {
            var burst = new Vector2(2f, -3f);

            Assert.AreEqual(reaches, RipeBubbleRule.RippleReaches(burst, burst + new Vector2(dx, dy), radius));
        }

        [Test]
        public void RippleReaches_UpToTheRadius_AndNoFurther()
        {
            Assert.IsTrue(RipeBubbleRule.RippleReaches(Vector2.zero, new Vector2(1.349f, 0f), 1.35f));
            Assert.IsFalse(RipeBubbleRule.RippleReaches(Vector2.zero, new Vector2(0f, -1.351f), 1.35f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void RippleReaches_NothingWithNoRadius_NotEvenItsOwnSlot(float radius)
        {
            Assert.IsFalse(RipeBubbleRule.RippleReaches(Vector2.one, Vector2.one, radius));
        }

        // The game's own lattice (SlotGridConfig: 0.375 x 0.65 apart, so neighbours sit 0.75 from each other): the
        // shipped radius takes the six neighbours and the six slots behind them, and nothing a third ring out.
        [Test]
        public void TheShippedRadius_TakesTwelveSlotsOnAnOpenBoard()
        {
            var separation = new Vector2(0.375f, 0.65f);
            var centre = new Vector2Int(10, 10);
            var burst = (Vector2)HexCoordinates.IndexToWorldPosition(centre, separation, Vector2.zero);
            var reached = 0;
            for (var col = 0; col < 20; col++)
            {
                for (var row = 0; row < 20; row++)
                {
                    var slot = new Vector2Int(col, row);
                    var at = (Vector2)HexCoordinates.IndexToWorldPosition(slot, separation, Vector2.zero);
                    if (slot != centre && RipeBubbleRule.RippleReaches(burst, at, 1.35f))
                    {
                        reached++;
                        Assert.LessOrEqual(HexCoordinates.Distance(centre, slot), 2, $"{slot} is a third ring out");
                    }
                }
            }

            Assert.AreEqual(12, reached);
        }

        [TestCase(1f, false, 2f * Mathf.PI)]
        [TestCase(1f, true, 2f * Mathf.PI * RipeBubbleRule.FastTurnSpeedup)]
        [TestCase(0.5f, false, Mathf.PI)]
        [TestCase(0f, true, 0f)]
        [TestCase(-2f, false, 0f)]
        public void SpiralTurnRate_IsTheArmsTurnsInRadians_ALittleFasterWhenFast(
            float turnsPerSecond, bool fast, float radiansPerSecond)
        {
            Assert.AreEqual(radiansPerSecond, RipeBubbleRule.SpiralTurnRate(turnsPerSecond, fast), 1e-4f);
        }

        // The arm's source turns direction x SpiralTurnRate a second (RipeBubbleBurstView.AdvanceSource). The
        // cluster, drawn by the shader at the spin ClusterSpin gives it, must have turned as far, the same way.
        [TestCase(1f, false)]
        [TestCase(-1f, false)]
        [TestCase(1f, true)]
        [TestCase(-1f, true)]
        public void ClusterSpin_TurnsTheDrawnClusterWithItsSpiralArm_SlowAndFast(float direction, bool fast)
        {
            const float turnsPerSecond = 1f;
            const float seconds = 0.2f;
            const float startAngle = 0.7f;
            var spin = RipeBubbleRule.ClusterSpin(direction, turnsPerSecond, fast);

            var armTurned = direction * RipeBubbleRule.SpiralTurnRate(turnsPerSecond, fast) * seconds;
            var clusterTurned = Vector2.SignedAngle(
                DrawnAt(startAngle), DrawnAt(startAngle + (spin * seconds))) * Mathf.Deg2Rad;

            Assert.AreEqual(armTurned, clusterTurned, 1e-3f);
        }

        [TestCase(0, 0.12f)]
        [TestCase(5, 0.72f)]
        public void FoamDepartSeconds_OneEveryStep_TheFirstAStepIn(int bubble, float seconds)
        {
            Assert.AreEqual(seconds, RipeBubbleRule.FoamDepartSeconds(bubble, 0.12f), 1e-5f);
        }

        [TestCase(0f, 6)]
        [TestCase(0.119f, 6)]
        [TestCase(0.12f, 5)]
        [TestCase(0.6f, 1)]
        [TestCase(0.72f, 0)]
        [TestCase(5f, 0)]
        public void FoamShownCount_StepsSixToZero(float elapsed, int shown)
        {
            Assert.AreEqual(shown, RipeBubbleRule.FoamShownCount(6, elapsed, 0.12f));
        }

        [Test]
        public void FoamLeaveSeconds_IsWhenTheLastBubbleLeaves()
        {
            Assert.AreEqual(RipeBubbleRule.FoamDepartSeconds(5, 0.12f), RipeBubbleRule.FoamLeaveSeconds(6, 0.12f), 1e-5f);
            Assert.AreEqual(0f, RipeBubbleRule.FoamLeaveSeconds(0, 0.12f));
        }

        // Where the soap shader draws a bubble that sits at (1, 0) in the cluster's own frame, at a rotation angle.
        // It samples the cluster at uv turned by the angle (SoapBubbleCluster.shader: uv = (x cos - y sin,
        // x sin + y cos)), so the bubble shows where that turn lands on it: turned back by the angle.
        private static Vector2 DrawnAt(float shaderAngle)
        {
            return new Vector2(Mathf.Cos(shaderAngle), -Mathf.Sin(shaderAngle));
        }

        private static void AssertGifts(int handout, params (FoamGift Gift, int Direction)[] expected)
        {
            Assert.AreEqual(expected.Length, RipeBubbleBurstMessage.HandoutCount(handout));
            for (var bubble = 0; bubble < expected.Length; bubble++)
            {
                var gift = RipeBubbleBurstMessage.HandoutGift(handout, bubble, out var direction);
                Assert.AreEqual(expected[bubble].Gift, gift, $"bubble {bubble}");
                Assert.AreEqual(expected[bubble].Direction, direction, $"bubble {bubble}");
            }
        }
    }
}

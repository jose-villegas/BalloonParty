using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Item.Shield;
using BalloonParty.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    // The planner's promise is that a chain it returns is collectable by ONE shot — so every test
    // here asserts about a flight, not about a set of positions.
    [TestFixture]
    public class ShieldChainPlannerTests
    {
        // x = top, y = right, z = bottom, w = left (WallLimits).
        private static readonly WallLimits Walls = new(new Vector4(5f, 3f, -5f, -3f));

        // The authored defaults — the tuning these tests' expectations were written against.
        private static readonly IShieldChainSettings Settings = new ShieldChainSettings();

        private List<int> _results;

        [SetUp]
        public void SetUp()
        {
            _results = new List<int>();
        }

        // Fired up-right so the leg leaving the right wall heads away from both candidates — firing
        // straight up would bounce off the top and come back down the same column, which is a second
        // leg and would legitimately claim the second one.
        [Test]
        public void PlanChain_TwoCandidatesOnOneLeg_ClaimsOnlyTheNearer()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate>
            {
                new(new Vector2(2f, 2f), 0.4f),
                new(new Vector2(1f, 1f), 0.4f),
            };

            var placed = planner.PlanChain(
                Vector2.zero, new Vector2(1f, 1f).normalized, 1, 2, candidates, _results);

            Assert.AreEqual(1, placed, "two on one straight run is one opportunity, not two");
            Assert.AreEqual(1, _results[0], "the nearer of the two");
        }

        // The mechanic: the shot could not have reached the far candidate on its starting shield, and
        // the one it picks up on the way is what pays for the bounce that gets it there.
        [Test]
        public void PlanChain_ShieldCollectedEnRoute_PaysForTheBounceToTheNext()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            // Up-right hits the right wall at (3,3) and leaves toward (1,5); (2,4) sits on that leg.
            var enRoute = new ShieldHostCandidate(new Vector2(1f, 1f), 0.4f);
            var afterTheBounce = new ShieldHostCandidate(new Vector2(2f, 4f), 0.4f);
            var candidates = new List<ShieldHostCandidate> { enRoute, afterTheBounce };
            var direction = new Vector2(1f, 1f).normalized;

            var placed = planner.PlanChain(Vector2.zero, direction, 0, 2, candidates, _results);

            Assert.AreEqual(2, placed, "the first shield funds the wall bounce that reaches the second");
            CollectionAssert.AreEqual(new[] { 0, 1 }, _results, "collected in flight order");

            // The control: with nothing to pick up on the way, the same shot dies at that same wall
            // and never gets to the second one. That gap IS the mechanic.
            var withoutTheFirst = new List<ShieldHostCandidate> { afterTheBounce };

            Assert.AreEqual(
                0, planner.PlanChain(Vector2.zero, direction, 0, 1, withoutTheFirst, _results),
                "unreachable on the starting shield alone");
        }

        [Test]
        public void PlanChain_NoShields_DiesAtTheFirstWall()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate>
            {
                new(new Vector2(0f, 4f), 0.4f), // reachable only after a bounce
            };

            var placed = planner.PlanChain(
                new Vector2(2.5f, 0f), new Vector2(1f, 0.2f).normalized, 0, 1, candidates, _results);

            Assert.AreEqual(0, placed);
        }

        // A tough redirects for free, so the planner may route a chain through one — but the leg after
        // it has to be the deflected leg, not the straight continuation.
        [Test]
        public void PlanChain_DeflectorBendsTheChain_WithoutSpendingAShield()
        {
            var deflectors = new List<DeflectorCircle> { new(new Vector2(0f, 2f), 0.5f) };
            var planner = new ShieldChainPlanner(Walls, deflectors, Settings);
            var candidates = new List<ShieldHostCandidate>
            {
                new(new Vector2(0f, 3.5f), 0.4f), // straight on, BEHIND the deflector
            };

            var placed = planner.PlanChain(Vector2.zero, Vector2.up, 0, 1, candidates, _results);

            Assert.AreEqual(0, placed, "the shot turns at the tough and never reaches what is behind it");
        }

        [Test]
        public void PlanChain_NothingReachable_ReturnsEmptyRatherThanGuessing()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate> { new(new Vector2(-2.5f, -4f), 0.4f) };

            var placed = planner.PlanChain(Vector2.zero, Vector2.up, 1, 1, candidates, _results);

            Assert.AreEqual(0, placed);
            Assert.IsEmpty(_results);
        }

        [Test]
        public void PlanChain_NeverClaimsTheSameCandidateTwice()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate> { new(new Vector2(0f, 1f), 0.4f) };

            var placed = planner.PlanChain(Vector2.zero, Vector2.up, 5, 3, candidates, _results);

            Assert.AreEqual(1, placed, "one candidate can host one shield however long the flight is");
        }
    
        // The fan version's promise: every shield has several ways in, and the count reported IS the
        // tolerance — no separate angular-margin check needed, because it was measured.
        [Test]
        public void PlanChain_FromAFan_ReportsHowManyAnglesReachEachShield()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate>
            {
                new(new Vector2(0f, 2f), 0.5f),
                new(new Vector2(1.5f, 3f), 0.5f),
            };
            var placements = new List<ShieldPlacement>();

            var placed = planner.PlanChain(Vector2.zero, Fan(30f, 150f, 25), 2, 2, candidates, placements);

            Assert.Greater(placed, 0);
            foreach (var placement in placements)
            {
                Assert.Greater(placement.EntryAngles, 1,
                    "a shield only one angle reaches is the brittle chain this replaces");
            }
        }

        [Test]
        public void PlanChain_FromAFan_NeverPlacesTwiceOnOneCandidate()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate> { new(new Vector2(0f, 2f), 0.5f) };
            var placements = new List<ShieldPlacement>();

            var placed = planner.PlanChain(Vector2.zero, Fan(30f, 150f, 25), 2, 3, candidates, placements);

            Assert.AreEqual(1, placed);
        }

        // Below the thrower, so no opening leg reaches it — and with nothing to spend, no shot
        // survives the wall that would turn it back down. Give this same board two shields and it IS
        // reachable: straight up, off the top, back down through it. Unreachable means unaffordable,
        // not "behind me".
        [Test]
        public void PlanChain_FromAFan_NothingAffordable_PlacesNothing()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate> { new(new Vector2(0f, -4.5f), 0.3f) };
            var placements = new List<ShieldPlacement>();

            var placed = planner.PlanChain(Vector2.zero, Fan(30f, 150f, 25), 0, 1, candidates, placements);

            Assert.AreEqual(0, placed, "better no shield than one no shot can take");

            // The control: the same board, affordable. Reachability is a budget question.
            Assert.AreEqual(
                1, planner.PlanChain(Vector2.zero, Fan(30f, 150f, 25), 2, 1, candidates, placements),
                "two shields buy the top-wall bounce that turns the shot back down onto it");
        }


        // Walls do not move and balloons do, so a route off a wall survives the board rebalancing and
        // survives the tough being popped. Given two otherwise-equal candidates, the wall one wins.
        [Test]
        public void PlanChain_PrefersAShieldReachedOffAWall_OverOneBehindATough()
        {
            var deflectors = new List<DeflectorCircle> { new(new Vector2(-1.5f, 2f), 0.5f) };
            var planner = new ShieldChainPlanner(Walls, deflectors, Settings);

            // Left of centre, only reachable once the tough has bent the shot into it.
            var behindTheTough = new ShieldHostCandidate(new Vector2(-2.4f, 2.6f), 0.45f);

            // Right of centre, straight shots reach it with no balloon involved.
            var offTheWall = new ShieldHostCandidate(new Vector2(1.6f, 2.2f), 0.45f);

            var candidates = new List<ShieldHostCandidate> { behindTheTough, offTheWall };
            var placements = new List<ShieldPlacement>();

            planner.PlanChain(Vector2.zero, Fan(30f, 150f, 25), 2, 1, candidates, placements);

            Assert.AreEqual(1, placements.Count);
            Assert.AreEqual(1, placements[0].CandidateIndex, "the one no tough has to be alive for");
        }


        // The guarantee the fan version originally lost: every shield in a chain must be collectable
        // by ONE opening. Two individually reachable shields on unrelated shots are two pickups, not
        // a chain — which is exactly what "sometimes it chains" looked like from the outside.
        [Test]
        public void PlanChain_EveryShield_IsCollectableByAtLeastOneSharedOpening()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate>
            {
                new(new Vector2(-1.8f, 1.2f), 0.45f),
                new(new Vector2(1.8f, 1.2f), 0.45f),
                new(new Vector2(0f, 3.2f), 0.45f),
                new(new Vector2(2.2f, 3.6f), 0.45f),
            };
            var placements = new List<ShieldPlacement>();
            var fan = Fan(30f, 150f, 25);

            var placed = planner.PlanChain(Vector2.zero, fan, 2, 3, candidates, placements);
            Assert.Greater(placed, 1, "need at least two links to have a chain at all");

            // Re-fly the fan and keep only openings that collect the whole chain.
            var collected = new List<int>();
            var survivors = 0;
            for (var i = 0; i < fan.Count; i++)
            {
                planner.PlanChain(Vector2.zero, fan[i], 2, candidates.Count, candidates, collected);

                var takesAll = true;
                foreach (var placement in placements)
                {
                    takesAll &= collected.Contains(placement.CandidateIndex);
                }

                if (takesAll)
                {
                    survivors++;
                }
            }

            Assert.Greater(survivors, 0, "no single opening collects the chain — these are not links");
        }

        // The reported tolerance is now the chain's, not the shield's, so it can only narrow as links
        // are added — a later shield cannot be easier to reach than the chain that funds it.
        [Test]
        public void PlanChain_ReportedEntryAngles_NarrowAlongTheChain()
        {
            var planner = new ShieldChainPlanner(Walls, null, Settings);
            var candidates = new List<ShieldHostCandidate>
            {
                new(new Vector2(-1.8f, 1.2f), 0.45f),
                new(new Vector2(1.8f, 1.2f), 0.45f),
                new(new Vector2(0f, 3.2f), 0.45f),
                new(new Vector2(2.2f, 3.6f), 0.45f),
            };
            var placements = new List<ShieldPlacement>();

            planner.PlanChain(Vector2.zero, Fan(30f, 150f, 25), 2, 3, candidates, placements);

            for (var i = 1; i < placements.Count; i++)
            {
                Assert.LessOrEqual(placements[i].EntryAngles, placements[i - 1].EntryAngles);
            }
        }

        // Evenly spaced headings across the upper semicircle — the arc the thrower's position, not any
        // code clamp, actually limits aiming to.
        private static List<Vector2> Fan(float fromDegrees, float toDegrees, int count)
        {
            var fan = new List<Vector2>(count);
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5f : (float)i / (count - 1);
                var degrees = Mathf.Lerp(fromDegrees, toDegrees, t);
                fan.Add(new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)));
            }

            return fan;
        }
}
}

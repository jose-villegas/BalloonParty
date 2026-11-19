using System.Collections.Generic;
using System.Text.RegularExpressions;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     The geometry of a mushroom bounce: the mirror reflection (<see cref="TrueReflectRule" />) and the forgiving
    ///     kick (<see cref="ForgivingKickRule" />, <see cref="BumperKick" />), which bends a bounce toward the nearest
    ///     mushroom inside its cone, lands the ball where its next bounce points at a follower, never lands it dead
    ///     centre, and stops once the chain has paid.
    /// </summary>
    [TestFixture]
    public class BumperBounceRuleTests
    {
        private const float Tolerance = 1e-4f;
        private const float Radius = 0.5f;

        // The kick's numbers as they ship (TheNumbersTheseTestsKickWith_AreTheShippedOnes holds them to the
        // settings' defaults): written out because test cases and loops are built from them.
        private const float ConeDegrees = 20f;
        private const float MinOffset = 0.15f;
        private const float MaxOffset = 0.9f;

        // The contact radius of the shipped sizes: a mushroom's 0.3 plus the ball's 0.125.
        private const float ShippedContactRadius = 0.425f;
        private const int MaxBouncesInAPair = 12;
        private const int SimulatedBounces = 200;

        // The chain as settled, and the same with no cap on bounces to speak of, for the tests that show what the
        // kick does by itself.
        private static readonly MushroomChainRules Settled = new(chainTarget: 3);

        private static readonly MushroomChainRules Uncapped = new(chainTarget: 3, maxBouncesPerStretch: 100000);

        // The mushroom every kick test bounces off: the ball touches its top, so the surface normal is +Y.
        private static readonly BumperCircle Hit = new(0, Vector2.zero, Radius);
        private static readonly Vector2 Top = new(0f, Radius);

        private StubSettings _settings;
        private ForgivingKickRule _kick;

        [SetUp]
        public void SetUp()
        {
            _settings = new StubSettings
            {
                Rules = Settled,
                ForgivingKick = true,
                Kick = new BumperKickTuning(ConeDegrees, MinOffset, MaxOffset),
            };
            _kick = new ForgivingKickRule(_settings);
        }

        [Test]
        public void TrueReflect_HeadOn_ComesStraightBack()
        {
            var contact = new BumperContact(new Vector2(0f, -Radius), Vector2.up, Hit);

            var leaving = new TrueReflectRule().Resolve(contact, new[] { Hit }, default);

            AssertDirection(Vector2.down, leaving);
        }

        [Test]
        public void TrueReflect_Glancing_MirrorsAboutTheSurfaceNormal()
        {
            // The normal at the bottom of the circle is -Y: the Y of the travel flips and the X is kept.
            var contact = new BumperContact(new Vector2(0f, -Radius), new Vector2(1f, 1f), Hit);

            var leaving = TrueReflectRule.Reflect(contact);

            AssertDirection(new Vector2(1f, -1f).normalized, leaving);
        }

        [Test]
        public void TrueReflect_AnyIncomingLength_LeavesAtUnitLength()
        {
            var contact = new BumperContact(new Vector2(0f, -Radius), new Vector2(0f, 7f), Hit);

            Assert.AreEqual(1f, TrueReflectRule.Reflect(contact).magnitude, Tolerance);
        }

        [Test]
        public void TrueReflect_AlreadyLeavingTheSurface_KeepsItsDirection()
        {
            var contact = new BumperContact(new Vector2(0f, -Radius), Vector2.down, Hit);

            AssertDirection(Vector2.down, TrueReflectRule.Reflect(contact));
        }

        [Test]
        public void LandingPoint_BetweenTwoMirroredPoints_IsTheTopOfTheCircle()
        {
            var circle = new BumperCircle(0, Vector2.zero, 1f);

            var found = BumperKick.TryFindLandingPoint(
                new Vector2(-2f, 3f), circle, new Vector2(2f, 3f), out var point, out var offset, out var side);

            Assert.IsTrue(found);
            AssertDirection(Vector2.up, point);
            Assert.AreEqual(Mathf.Sin(45f * Mathf.Deg2Rad), offset, Tolerance, "It comes in 45 degrees off the normal");
            Assert.AreEqual(1f, side, "The point is left of the line from the start to the centre");
        }

        // Every landing point the search answers is a true one: the mirror reflection there passes the other
        // point within a tenth of a degree. That holds with either point a hair off the circle too, where the
        // search may give up but must not answer wrong.
        [TestCase(0.4f, 6f)]
        [TestCase(1e-4f, 0.05f)]
        public void LandingPoint_20000RandomPairs_EveryAnswerIsATrueReflection(float nearest, float farthest)
        {
            var circle = new BumperCircle(0, new Vector2(0.3f, -0.2f), ShippedContactRadius);
            var random = new System.Random(7);
            var answered = 0;
            for (var trial = 0; trial < 20000; trial++)
            {
                // One point anywhere, the other in the band under test: off the circle by nearest to farthest.
                var from = RandomPointOff(random, circle, nearest, farthest);
                var toward = RandomPointOff(random, circle, 0.4f, 6f);
                if (trial % 2 == 0)
                {
                    (from, toward) = (toward, from);
                }

                if (!BumperKick.TryFindLandingPoint(from, circle, toward, out var point, out var offset, out _))
                {
                    continue;
                }

                answered++;
                var label = $"Trial {trial}, from {from:F5} toward {toward:F5}";
                var leaving = TrueReflectRule.Reflect(new BumperContact(point, point - from, circle));
                var way = (toward - point).normalized;
                Assert.Greater(Vector2.Dot(leaving, way), 0f, label);
                Assert.Less(Mathf.Abs(Cross(leaving, way)), 1.75e-3f, $"{label}: a tenth of a degree");
                Assert.AreEqual(circle.Radius, Vector2.Distance(point, circle.Center), Tolerance, label);
                Assert.AreEqual(MissDistance(from, point - from, circle.Center) / circle.Radius, offset, 2e-3f,
                    $"{label}: the offset is the impact parameter over the radius");
            }

            Assert.Greater(answered, 1000, "The search answers, it does not only give up");
        }

        // A landing point is found whenever there is one, and there is one whenever some part of the circle is
        // seen by both points. Whether some part is, is settled here by walking the whole circle in steps of a
        // twentieth of a degree, apart from the search. A part narrower than three steps is left out as too close
        // to call.
        [Test]
        public void LandingPoint_5000RandomPairs_FoundWheneverBothPointsSeeSomeOfTheCircle()
        {
            var circle = new BumperCircle(0, new Vector2(0.3f, -0.2f), ShippedContactRadius);
            var random = new System.Random(11);
            var withOne = 0;
            for (var trial = 0; trial < 5000; trial++)
            {
                var from = RandomPointOff(random, circle, 0.02f, 6f);
                var toward = RandomPointOff(random, circle, 0.02f, 6f);
                var seen = StepsSeenByBoth(from, circle, toward);
                if (seen > 0 && seen < 3)
                {
                    continue;
                }

                withOne += seen > 0 ? 1 : 0;

                var found = BumperKick.TryFindLandingPoint(from, circle, toward, out _, out _, out _);

                Assert.AreEqual(seen > 0, found, $"Trial {trial}, from {from:F5} toward {toward:F5}");
            }

            Assert.Greater(withOne, 1000);
        }

        [Test]
        public void LandingPoint_TheOtherPointStraightBehindTheCircle_HasNone()
        {
            var circle = new BumperCircle(0, Vector2.zero, 1f);

            Assert.IsFalse(BumperKick.TryFindLandingPoint(
                new Vector2(-3f, 0f), circle, new Vector2(3f, 0.2f), out _, out _, out _));
        }

        [Test]
        public void LandingPoint_StartInsideTheCircle_HasNone()
        {
            var circle = new BumperCircle(0, Vector2.zero, 1f);

            Assert.IsFalse(BumperKick.TryFindLandingPoint(
                new Vector2(0.5f, 0f), circle, new Vector2(3f, 3f), out _, out _, out _));
        }

        [Test]
        public void TheNumbersTheseTestsKickWith_AreTheShippedOnes()
        {
            var shipped = new MushroomSettings().Kick;

            Assert.AreEqual(shipped.ConeDegrees, ConeDegrees);
            Assert.AreEqual(shipped.MinOffset, MinOffset);
            Assert.AreEqual(shipped.MaxOffset, MaxOffset);
        }

        [Test]
        public void Tuning_TheOffsetsAreTheTurnRange()
        {
            var tuning = new BumperKickTuning(ConeDegrees, MinOffset, MaxOffset);

            Assert.AreEqual(180f, BumperKickTuning.TurnDegrees(0f), Tolerance, "Dead centre comes straight back");
            Assert.AreEqual(0f, BumperKickTuning.TurnDegrees(1f), Tolerance, "A graze flies straight on");
            Assert.AreEqual(162.746f, tuning.MaxTurnDegrees, 0.01f, "180 less twice the arcsine of 0.15");
            Assert.AreEqual(51.684f, tuning.MinTurnDegrees, 0.01f, "180 less twice the arcsine of 0.9");
        }

        [Test]
        public void Tuning_OffsetsAuthoredOutOfRange_NeverTheCentreNeverAGraze()
        {
            var tuning = new BumperKickTuning(ConeDegrees, minOffset: 0f, maxOffset: 1f);
            var crossed = new BumperKickTuning(ConeDegrees, minOffset: 0.6f, maxOffset: 0.3f);

            Assert.Greater(tuning.MinOffset, 0.01f);
            Assert.Less(tuning.MaxOffset, 1f);
            Assert.AreEqual(crossed.MinOffset, crossed.MaxOffset, "A largest under the smallest gives way to it");
        }

        [Test]
        public void Kick_MushroomInsideTheCone_BendsTowardIt()
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(1));

            var reflection = TrueReflectRule.Reflect(contact);
            var bearing = (target.Center - Top).normalized;
            Assert.AreEqual(1f, leaving.magnitude, Tolerance);
            Assert.Less(Vector2.Angle(leaving, bearing), Vector2.Angle(reflection, bearing), "Bent toward it");
            Assert.IsTrue(TryLand(leaving, target, out _), "The kicked ball meets the mushroom");
        }

        [Test]
        public void Kick_WithAFollower_LandsWhereTheReflectionPointsAtIt()
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var follower = Beyond(2, target, bearingDegrees: -20f, distance: 2.5f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target, follower }, Chain(1));

            Assert.IsTrue(TryLand(leaving, target, out var landing));
            Assert.Less(Vector2.Angle(TrueReflectRule.Reflect(landing), follower.Center - landing.Point), 0.05f,
                "The mirror reflection at the landing point points at the follower's centre");
            Assert.That(OffsetOf(landing), Is.InRange(MinOffset, MaxOffset));
        }

        // The composition: the ball a kick lands leaves the target along a reflection that the target's own kick,
        // testing its cone from that reflection, sends on to the follower, where it lands for the follower's
        // follower in turn.
        [Test]
        public void Kick_AtTheTargetItLanded_CarriesOnToTheFollower()
        {
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var follower = Beyond(2, target, bearingDegrees: -20f, distance: 2.5f);
            var last = Beyond(3, follower, bearingDegrees: -120f, distance: 2f);
            var bumpers = new[] { Hit, target, follower, last };
            _settings.Kick = new BumperKickTuning(coneDegrees: 1f, MinOffset, MaxOffset);

            Assert.IsTrue(TryLand(_kick.Resolve(ContactReflecting(79.5f), bumpers, Chain(1)), target, out var first));
            var onward = _kick.Resolve(first, bumpers, Chain(2));

            Assert.IsTrue(TryLandFrom(first.Point, onward, follower, out var second), "A cone of 1 degree is enough");
            Assert.Less(Vector2.Angle(TrueReflectRule.Reflect(second), last.Center - second.Point), 0.05f);
        }

        [Test]
        public void Kick_TwoMushrooms_TheFollowerIsTheOneJustHit_AndTheReturnStaysInsideTheCone()
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(1));

            Assert.IsTrue(TryLand(leaving, target, out var landing));
            Assert.AreEqual(MinOffset, OffsetOf(landing), 1e-3f, "Straight back needs the centre: the clamp binds");
            var offTheWayBack = Vector2.Angle(TrueReflectRule.Reflect(landing), Hit.Center - landing.Point);
            var coneNeeded = 2f * Mathf.Asin(MinOffset) * Mathf.Rad2Deg;
            Assert.LessOrEqual(offTheWayBack, coneNeeded, "Twice the arcsine of the smallest offset: 17.3 degrees");
            Assert.Less(coneNeeded, ConeDegrees, "So the shipped cone of 20 degrees sends it back");
        }

        [Test]
        public void Kick_FollowerNeedsNearlyAGraze_LandsAtTheLargestOffset_OnItsSide()
        {
            var contact = ContactReflecting(79f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var follower = Beyond(2, target, bearingDegrees: 40f, distance: 2.5f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target, follower }, Chain(1));

            Assert.IsTrue(TryLand(leaving, target, out var landing));
            Assert.AreEqual(MaxOffset, OffsetOf(landing), 1e-3f);
            var bearing = target.Center - Top;
            Assert.AreEqual(Mathf.Sign(Cross(bearing, follower.Center - Top)), Mathf.Sign(Cross(bearing, leaving)));
        }

        [Test]
        public void Kick_FollowerStraightBehindTheTarget_LandsAtTheLargestOffset()
        {
            var contact = ContactReflecting(82f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var follower = Beyond(2, target, bearingDegrees: 78f, distance: 2.5f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target, follower }, Chain(1));

            Assert.IsTrue(TryLand(leaving, target, out var landing));
            Assert.AreEqual(MaxOffset, OffsetOf(landing), 1e-3f);
        }

        [Test]
        public void Kick_SeveralFollowersFit_TakesTheNearestToTheTarget_WhateverTheIds()
        {
            // The same four caps numbered two ways, each a wall by itself. The follower is the nearer of the two
            // beyond the target, whichever id it has.
            var nearAhead = FollowerLandedFor(0, 1, 3, 2, aheadDistance: 2.5f, otherDistance: 2.7f, out var ahead, out _);
            var nearOther = FollowerLandedFor(3, 2, 0, 1, aheadDistance: 2.7f, otherDistance: 2.5f, out _, out var other);

            Assert.Less(
                Vector2.Angle(TrueReflectRule.Reflect(nearAhead), ahead.Center - nearAhead.Point), 0.05f, "the nearer, ahead");
            Assert.Less(
                Vector2.Angle(TrueReflectRule.Reflect(nearOther), other.Center - nearOther.Point), 0.05f, "the nearer, the other");
        }

        [Test]
        public void Kick_NeverBendsTowardACapOfTheWallJustHit()
        {
            // One cap dead on the reflection's line. As a cap of another wall it takes the ball; as a cap of the
            // wall just hit the bounce is the mirror reflection, as if nothing were there to bend toward.
            var contact = ContactReflecting(70f);
            var ofAnotherWall = BumperAt(1, bearingDegrees: 76f, distance: 3f);
            var ofTheSameWall = new BumperCircle(1, Hit.Wall, ofAnotherWall.Center, ofAnotherWall.Radius);

            var bent = _kick.Resolve(contact, new[] { Hit, ofAnotherWall }, Chain(1));
            var plain = _kick.Resolve(contact, new[] { Hit, ofTheSameWall }, Chain(1));

            Assert.Greater(Vector2.Angle(TrueReflectRule.Reflect(contact), bent), 0.5f, "bent toward another wall");
            AssertDirection(TrueReflectRule.Reflect(contact), plain);
        }

        [Test]
        public void Kick_TheFollower_IsNeverACapOfTheTargetsOwnWall()
        {
            // Beside the target stands another cap of its wall, nearer than anything else: the ball is landed for
            // the cap it came from, the nearest that is not of the target's wall, and not along the wall.
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var itsNeighbour = Beyond(2, target, bearingDegrees: 170f, distance: 0.9f);
            itsNeighbour = new BumperCircle(2, target.Wall, itsNeighbour.Center, itsNeighbour.Radius);

            var leaving = _kick.Resolve(contact, new[] { Hit, target, itsNeighbour }, Chain(1));

            Assert.IsTrue(TryLand(leaving, target, out var landing));
            var back = Vector2.Angle(TrueReflectRule.Reflect(landing), Hit.Center - landing.Point);
            var along = Vector2.Angle(TrueReflectRule.Reflect(landing), itsNeighbour.Center - landing.Point);
            Assert.LessOrEqual(back, (2f * Mathf.Asin(MinOffset) * Mathf.Rad2Deg) + 0.01f, "sent back toward the wall it came from");
            Assert.Greater(along, 45f, "and not along the target's own wall");
        }

        // Two walls of three caps in neighbouring slots, facing each other across the nearest gap a level deals
        // (1.95), with the shipped cone and offsets: a ball that leaves one wall a little off the straight way
        // across is landed for a cap of the wall it left, and so on. Every bounce up to the one that arms is on
        // the other wall than the one before, which is three hits, wall to wall.
        [Test]
        public void Kick_BetweenTwoFacingWalls_KeepsTheBallCrossing(
            [Values(-12f, -6f, 0f, 6f, 12f)] float offStraightDegrees, [Values(0, 1, 2)] int fromCap)
        {
            var shipped = new MushroomSettings();
            var rule = new ForgivingKickRule(shipped);
            var caps = new List<BumperCircle>();
            for (var i = 0; i < 6; i++)
            {
                caps.Add(new BumperCircle(
                    i, i / 3, new Vector2(0.75f * ((i % 3) - 1), i / 3 == 0 ? 0f : 1.95f), ShippedContactRadius));
            }

            // The ball leaves the top of a cap of the lower wall, its reflection a little off straight up.
            var normal = Direction(90f - (0.5f * offStraightDegrees));
            var leavingAlong = Direction(90f - offStraightDegrees);
            var contact = new BumperContact(
                caps[fromCap].Center + (normal * ShippedContactRadius), Vector2.Reflect(leavingAlong, normal), caps[fromCap]);
            for (var count = 1; count <= shipped.Rules.ChainTarget; count++)
            {
                var leaving = rule.Resolve(contact, caps, Chain(count));

                var next = default(BumperContact);
                var nearest = float.MaxValue;
                foreach (var cap in caps)
                {
                    if (cap.Id != contact.Bumper.Id && TryLandFrom(contact.Point, leaving, cap, out var landing)
                        && Vector2.Distance(contact.Point, landing.Point) < nearest)
                    {
                        nearest = Vector2.Distance(contact.Point, landing.Point);
                        next = landing;
                    }
                }

                Assert.Less(nearest, float.MaxValue, $"bounce {count}: the ball meets a cap");
                Assert.AreNotEqual(contact.Bumper.Wall, next.Bumper.Wall, $"bounce {count}: on the other wall");
                contact = next;
            }
        }

        // Caps of one wall stand in neighbouring slots, so their contact circles overlap: that is how a wall is
        // built, and the kick has nothing to say of it. (In the Editor an assert nobody expected fails the test.)
        [Test]
        public void Kick_OverlappingCapsOfOneWall_AreNoFault()
        {
            var contact = ContactReflecting(70f);
            var besideTheHit = new BumperCircle(1, Hit.Wall, Hit.Center + new Vector2(-0.75f, 0f), Radius);
            var target = BumperAt(2, bearingDegrees: 76f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, besideTheHit, target }, Chain(1));

            Assert.Greater(
                Vector2.Angle(TrueReflectRule.Reflect(contact), leaving), 0.5f, "still kicked toward the other wall");
        }

        // Two walls whose circles overlap leave a ball caught between them no way out but the cap on bounces. No
        // dealt layout stands so; a list that does is called out where the checks are compiled in.
        [Test]
        public void Kick_OverlappingCapsOfTwoWalls_AreAFault()
        {
            LogAssert.Expect(LogType.Assert, new Regex("of two walls stand too close"));
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 76f, distance: 3f);
            var onTopOfIt = new BumperCircle(2, 2, target.Center + new Vector2(0.75f, 0f), Radius);

            _kick.Resolve(contact, new[] { Hit, target, onTopOfIt }, Chain(1));
        }

        // Where a cap stands in the list is not part of the rule: the order is by distance and id alone.
        [Test]
        public void Kick_ShuffledList_SameAnswerAsTheSortedOne()
        {
            var random = new System.Random(5);
            var kicked = 0;
            for (var trial = 0; trial < 3000; trial++)
            {
                var sorted = RandomBoard(random);
                var shuffled = (BumperCircle[])sorted.Clone();
                for (var i = shuffled.Length - 1; i > 0; i--)
                {
                    var swap = random.Next(i + 1);
                    (shuffled[i], shuffled[swap]) = (shuffled[swap], shuffled[i]);
                }

                var contact = RandomContact(random, sorted[random.Next(sorted.Length)]);
                var chain = Chain(1 + random.Next(3));

                var fromSorted = _kick.Resolve(contact, sorted, chain);

                Assert.AreEqual(fromSorted, _kick.Resolve(contact, shuffled, chain), $"Trial {trial}");
                kicked += Vector2.Angle(fromSorted, TrueReflectRule.Reflect(contact)) > 0.5f ? 1 : 0;
            }

            Assert.Greater(kicked, 300, "The boards are ones the kick takes hold on");
        }

        [Test]
        public void Kick_ContactOnAMushroomNotInTheList_LeavesAlongTheTrueReflection()
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var follower = Beyond(2, target, bearingDegrees: -20f, distance: 2.5f);

            var leaving = _kick.Resolve(contact, new[] { target, follower }, Chain(1));

            AssertDirection(TrueReflectRule.Reflect(contact), leaving);
        }

        [Test]
        public void Kick_LineLeavingWithinAHairOfTheTangent_IsNotKicked()
        {
            // The reflection leaves 0.02 degrees above the surface toward a mushroom so far off that the kick
            // turns it by no more: either side, the kicked line is under 0.06 degrees from the tangent, where a
            // rounding error could read it as entering the mushroom it is leaving.
            var contact = ContactReflecting(0.02f);
            var target = BumperAt(1, bearingDegrees: 0.02f, distance: 300f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(1));

            AssertDirection(TrueReflectRule.Reflect(contact), leaving);
        }

        [Test]
        public void Kick_FirstFollowerOutOfTheOffsets_TakesTheNextThatFits()
        {
            var contact = ContactReflecting(79f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var tooStraight = Beyond(2, target, bearingDegrees: 40f, distance: 2.5f);
            var fits = Beyond(3, target, bearingDegrees: 170f, distance: 2.5f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target, tooStraight, fits }, Chain(1));

            Assert.IsTrue(TryLand(leaving, target, out var landing));
            Assert.Less(Vector2.Angle(TrueReflectRule.Reflect(landing), fits.Center - landing.Point), 0.05f);
        }

        [Test]
        public void Kick_FirstFollowersWayBlocked_TakesTheNextThatFits()
        {
            // What stands in the way is a cap of the target's own wall: it is no follower itself, only an obstacle.
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var hidden = Beyond(2, target, bearingDegrees: -20f, distance: 2.5f);
            var fits = Beyond(3, target, bearingDegrees: -60f, distance: 2.5f);
            var inTheWay = Beyond(4, target, bearingDegrees: -20f, distance: 1.25f);
            inTheWay = new BumperCircle(4, target.Wall, inTheWay.Center, inTheWay.Radius);

            var open = _kick.Resolve(contact, new[] { Hit, target, hidden, fits }, Chain(1));
            var blocked = _kick.Resolve(contact, new[] { Hit, target, hidden, fits, inTheWay }, Chain(1));

            Assert.IsTrue(TryLand(open, target, out var toHidden));
            Assert.Less(Vector2.Angle(TrueReflectRule.Reflect(toHidden), hidden.Center - toHidden.Point), 0.05f);
            Assert.IsTrue(TryLand(blocked, target, out var toNext));
            Assert.Less(Vector2.Angle(TrueReflectRule.Reflect(toNext), fits.Center - toNext.Point), 0.05f);
        }

        [Test]
        public void Kick_ThatBringsTheCountToTheTarget_LandsAtTheSmallestOffset_WhateverFollows()
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var follower = Beyond(2, target, bearingDegrees: -20f, distance: 2.5f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target, follower }, Chain(3));

            Assert.IsTrue(TryLand(leaving, target, out var landing));
            Assert.AreEqual(MinOffset, OffsetOf(landing), 1e-3f);
            var bearing = target.Center - Top;
            var reflection = TrueReflectRule.Reflect(contact);
            Assert.AreEqual(Mathf.Sign(Cross(bearing, reflection)), Mathf.Sign(Cross(bearing, leaving)),
                "On the side the reflection already leaned to");
        }

        [Test]
        public void Kick_TwoMushroomsInsideTheCone_TheSmallestAngleWins_WhateverTheListOrder()
        {
            var contact = ContactReflecting(70f);
            var near = BumperAt(1, bearingDegrees: 80f, distance: 3f);
            var far = BumperAt(2, bearingDegrees: 45f, distance: 3f);

            var nearFirst = _kick.Resolve(contact, new[] { Hit, near, far }, Chain(3));
            var farFirst = _kick.Resolve(contact, new[] { far, Hit, near }, Chain(3));

            Assert.IsTrue(TryLand(nearFirst, near, out _));
            AssertDirection(nearFirst, farFirst);
        }

        [Test]
        public void Kick_MushroomOutsideTheCone_LeavesAlongTheTrueReflection()
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 110f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(1));

            AssertDirection(TrueReflectRule.Reflect(contact), leaving);
        }

        [Test]
        public void Kick_NoOtherMushroom_LeavesAlongTheTrueReflection()
        {
            var contact = ContactReflecting(70f);

            var leaving = _kick.Resolve(contact, new[] { Hit }, Chain(1));

            AssertDirection(TrueReflectRule.Reflect(contact), leaving);
        }

        [Test]
        public void Kick_CorridorBlockedByAnotherMushroom_LeavesAlongTheTrueReflection()
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 86f, distance: 4f);

            // 60 degrees from the reflection, so no candidate itself, but wide enough to sit across the line to the
            // target.
            var blocker = BumperAt(2, bearingDegrees: 130f, distance: 0.9f, radius: 0.7f);

            var open = _kick.Resolve(contact, new[] { Hit, target }, Chain(3));
            var blocked = _kick.Resolve(contact, new[] { Hit, target, blocker }, Chain(3));

            var reflection = TrueReflectRule.Reflect(contact);
            Assert.Greater(Vector2.Angle(open, reflection), 1f, "Without the blocker the bounce is kicked");
            AssertDirection(reflection, blocked);
        }

        [Test]
        public void Kick_NearestBlocked_TheNextCandidateWins()
        {
            var contact = ContactReflecting(70f);
            var nearest = BumperAt(1, bearingDegrees: 86f, distance: 4f);
            var blocker = BumperAt(2, bearingDegrees: 130f, distance: 0.9f, radius: 0.7f);
            var next = BumperAt(3, bearingDegrees: 52f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, nearest, blocker, next }, Chain(3));

            Assert.IsTrue(TryLand(leaving, next, out _));
        }

        [Test]
        public void Kick_ThatWouldCrossItsOwnMushroom_LeavesAlongTheTrueReflection()
        {
            // The reflection grazes away at 5 degrees above the surface; the mushroom 25 degrees from it lies
            // below the surface line, behind the mushroom just hit.
            var contact = ContactReflecting(5f);
            var target = BumperAt(1, bearingDegrees: -20f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(1));

            AssertDirection(TrueReflectRule.Reflect(contact), leaving);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Kick_NeverLandsDeadCentreNorAGraze_AcrossTheWholeCone(int count)
        {
            var target = BumperAt(1, bearingDegrees: 90f, distance: 3f);

            // Off to the side, outside the cone of every reflection the loop tries, so the target stays the target.
            var follower = Beyond(2, target, bearingDegrees: -30f, distance: 4f);
            foreach (var bumpers in new[] { new[] { Hit, target }, new[] { Hit, target, follower } })
            {
                for (var degrees = 90f - ConeDegrees + 0.5f; degrees < 90f + ConeDegrees; degrees += 0.5f)
                {
                    var leaving = _kick.Resolve(ContactReflecting(degrees), bumpers, Chain(count));

                    Assert.IsTrue(TryLand(leaving, target, out var landing), $"Reflection at {degrees} degrees");
                    Assert.That(OffsetOf(landing), Is.InRange(MinOffset - 1e-3f, MaxOffset + 1e-3f),
                        $"Reflection at {degrees} degrees, {bumpers.Length} mushrooms");
                }
            }
        }

        [Test]
        public void Kick_ReflectionStraightAtTheCentre_StillLandsBesideIt()
        {
            var target = BumperAt(1, bearingDegrees: 90f, distance: 3f);
            var contact = new BumperContact(Top, Vector2.down, Hit);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(1));

            AssertDirection(Vector2.up, TrueReflectRule.Reflect(contact));
            Assert.IsTrue(TryLand(leaving, target, out var landing));
            Assert.AreEqual(MinOffset, OffsetOf(landing), 1e-3f);
        }

        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        [TestCase(5, false)]
        public void Kick_AppliesUntilTheCountPassesTheChainTarget(int count, bool kicked)
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(count));

            var reflection = TrueReflectRule.Reflect(contact);
            if (kicked)
            {
                Assert.Greater(Vector2.Angle(leaving, reflection), 1f, $"Count {count} is kicked");
            }
            else
            {
                AssertDirection(reflection, leaving);
            }
        }

        [Test]
        public void Kick_ChainTargetComesFromTheSettings()
        {
            _settings.Rules = new MushroomChainRules(chainTarget: 1);
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(2));

            AssertDirection(TrueReflectRule.Reflect(contact), leaving);
        }

        [Test]
        public void Kick_SwitchedOff_LeavesAlongTheTrueReflection()
        {
            _settings.ForgivingKick = false;
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, bearingDegrees: 80f, distance: 3f);

            var leaving = _kick.Resolve(contact, new[] { Hit, target }, Chain(1));

            AssertDirection(TrueReflectRule.Reflect(contact), leaving);
        }

        [Test]
        public void Kick_SameArguments_SameAnswer()
        {
            var contact = ContactReflecting(70f);
            var target = BumperAt(1, 80f, 3f);
            var bumpers = new[] { Hit, target, Beyond(2, target, -20f, 2.5f), BumperAt(3, 20f, 3f) };

            var first = _kick.Resolve(contact, bumpers, Chain(2));
            var second = _kick.Resolve(contact, bumpers, Chain(2));

            Assert.AreEqual(first, second);
        }

        // With no wall between them, two mushrooms must not keep a ball: from any contact on either, the flight
        // leaves the pair within a few bounces, whatever the cone and the smallest offset are tuned to.
        [TestCase(ConeDegrees, MinOffset)]
        [TestCase(30f, 0.2f)]
        [TestCase(10f, 0.05f)]
        [TestCase(60f, 0.05f)]
        [TestCase(90f, 0.1f)]
        [TestCase(90f, 0.5f)]
        [TestCase(45f, 0.9f)]
        public void Kick_TwoMushrooms_NoFlightBouncesBetweenThemForever(float coneDegrees, float minOffset)
        {
            _settings.Kick = new BumperKickTuning(coneDegrees, minOffset, MaxOffset);
            _settings.Rules = Uncapped;
            var random = new System.Random(20261119);
            var worst = 0;

            for (var trial = 0; trial < 4000; trial++)
            {
                var pair = RandomPair(random);
                var bounces = BouncesUntilLeaving(_kick, pair, RandomContact(random, pair[trial % 2]));
                worst = Mathf.Max(worst, bounces);
            }

            Assert.LessOrEqual(worst, MaxBouncesInAPair, "The longest exchange between two mushrooms, with no cap");
        }

        [Test]
        public void Kick_TwoMushrooms_DeadCentreStart_Leaves()
        {
            var pair = PairAlongX();
            _settings.Rules = Uncapped;

            Assert.LessOrEqual(BouncesUntilLeaving(_kick, pair, DeadCentreContact(pair)), MaxBouncesInAPair);
        }

        // Why the kick never lands a ball dead centre: under the mirror reflection alone, a ball on the line
        // between two centres is traded for good.
        [Test]
        public void TrueReflect_TwoMushrooms_DeadCentreStart_NoCap_NeverLeaves()
        {
            var pair = PairAlongX();
            _settings.Rules = Uncapped;

            var bounces = BouncesUntilLeaving(new TrueReflectRule(), pair, DeadCentreContact(pair));

            Assert.Greater(bounces, SimulatedBounces);
        }

        // And why the chain rule caps a stretch's bounces whatever the bounce rule is: the same endless trade is
        // over at the cap, where the mushrooms let the ball through.
        [Test]
        public void TrueReflect_TwoMushrooms_DeadCentreStart_TheCapEndsIt()
        {
            var pair = PairAlongX();

            var bounces = BouncesUntilLeaving(new TrueReflectRule(), pair, DeadCentreContact(pair));

            Assert.AreEqual(Settled.MaxBouncesPerStretch, bounces);
        }

        // Four mushrooms numbered as asked, laid out as in the follower tests: the target 3 from the contact
        // point, one mushroom a 100 degree turn on from it and one a 70 degree turn the other way, each as far
        // from the target as asked. Answers where the kicked ball lands on the target.
        private BumperContact FollowerLandedFor(
            int hitId, int targetId, int aheadId, int otherId, float aheadDistance, float otherDistance,
            out BumperCircle ahead, out BumperCircle other)
        {
            var hit = new BumperCircle(hitId, Hit.Center, Radius);
            var target = BumperAt(targetId, bearingDegrees: 80f, distance: 3f);
            ahead = Beyond(aheadId, target, bearingDegrees: -20f, distance: aheadDistance);
            other = Beyond(otherId, target, bearingDegrees: 170f, distance: otherDistance);
            var reflecting = ContactReflecting(70f);
            var contact = new BumperContact(reflecting.Point, reflecting.Incoming, hit);

            var leaving = _kick.Resolve(contact, new[] { other, hit, ahead, target }, Chain(1));

            Assert.IsTrue(TryLand(leaving, target, out var landing));
            return landing;
        }

        // Three or four mushrooms of the shipped size, each far enough from the others for a ball to pass.
        private static BumperCircle[] RandomBoard(System.Random random)
        {
            var board = new List<BumperCircle>();
            var count = 3 + random.Next(2);
            while (board.Count < count)
            {
                var center = new Vector2(
                    Mathf.Lerp(-2f, 2f, (float)random.NextDouble()), Mathf.Lerp(-2f, 2f, (float)random.NextDouble()));
                if (board.TrueForAll(placed => Vector2.Distance(placed.Center, center) > 1f))
                {
                    board.Add(new BumperCircle(board.Count, center, ShippedContactRadius));
                }
            }

            return board.ToArray();
        }

        // A point off the circle by between nearest and farthest, spread evenly over the orders of magnitude.
        private static Vector2 RandomPointOff(System.Random random, BumperCircle circle, float nearest, float farthest)
        {
            var gap = nearest * Mathf.Pow(farthest / nearest, (float)random.NextDouble());
            return circle.Center + (Direction((float)random.NextDouble() * 360f) * (circle.Radius + gap));
        }

        // How many of the 7200 points walked round the circle are seen by both points: each is on the side of the
        // surface that faces it.
        private static int StepsSeenByBoth(Vector2 from, BumperCircle circle, Vector2 toward)
        {
            var seen = 0;
            for (var step = 0; step < 7200; step++)
            {
                var normal = Direction(step * 0.05f);
                var point = circle.Center + (normal * circle.Radius);
                seen += Vector2.Dot(normal, from - point) > 0f && Vector2.Dot(normal, toward - point) > 0f ? 1 : 0;
            }

            return seen;
        }

        private static BumperCircle[] PairAlongX()
        {
            return new[]
            {
                new BumperCircle(0, Vector2.zero, ShippedContactRadius),
                new BumperCircle(1, new Vector2(2f, 0f), ShippedContactRadius),
            };
        }

        // The ball on the first mushroom, at the point facing the second, having come in along the line between
        // the two centres.
        private static BumperContact DeadCentreContact(BumperCircle[] pair)
        {
            return new BumperContact(new Vector2(ShippedContactRadius, 0f), Vector2.left, pair[0]);
        }

        // How many mushrooms the flight bounces off before it flies clear of the pair, or is let through by them
        // at the cap; more than SimulatedBounces when it is still between them at the end.
        private int BouncesUntilLeaving(IBumperBounceRule rule, BumperCircle[] pair, BumperContact contact)
        {
            var chain = default(MushroomChainState);
            for (var bounce = 1; bounce <= SimulatedBounces; bounce++)
            {
                var answer = MushroomChainRule.OnBounce(ref chain, contact.Bumper.Id, _settings.Rules);
                if (answer == MushroomReward.PassThrough)
                {
                    return bounce - 1;
                }

                var leaving = rule.Resolve(contact, pair, chain);
                var other = pair[contact.Bumper.Id == pair[0].Id ? 1 : 0];
                if (!CircleContact.TryFindEntry(
                        contact.Point, leaving, other.Center, other.Radius, out _, out var distance))
                {
                    return bounce;
                }

                contact = new BumperContact(contact.Point + (leaving * distance), leaving, other);
            }

            return SimulatedBounces + 1;
        }

        // Two mushrooms a leg apart. The near end is under the spacing a planned layout keeps (1.9): the nearer
        // two mushrooms stand, the more readily they trade the ball, so the test is the harder for it.
        private static BumperCircle[] RandomPair(System.Random random)
        {
            var distance = Mathf.Lerp(1.2f, 3.2f, (float)random.NextDouble());
            var bearing = (float)random.NextDouble() * 360f;
            return new[]
            {
                new BumperCircle(0, Vector2.zero, ShippedContactRadius),
                new BumperCircle(1, Direction(bearing) * distance, ShippedContactRadius),
            };
        }

        // A ball touching the bumper anywhere on its circle, coming in from anywhere within 85 degrees of head-on.
        private static BumperContact RandomContact(System.Random random, BumperCircle bumper)
        {
            var outward = (float)random.NextDouble() * 360f;
            var skew = Mathf.Lerp(-85f, 85f, (float)random.NextDouble());
            var point = bumper.Center + (Direction(outward) * bumper.Radius);
            return new BumperContact(point, Direction(outward + 180f + skew), bumper);
        }

        // The ball at the top of the hit mushroom, coming in so that its mirror reflection leaves at the given
        // bearing (degrees from +X, counter-clockwise).
        private static BumperContact ContactReflecting(float reflectionDegrees)
        {
            var reflection = Direction(reflectionDegrees);
            return new BumperContact(Top, new Vector2(reflection.x, -reflection.y), Hit);
        }

        // A mushroom at a bearing and distance from the contact point on the hit mushroom.
        private static BumperCircle BumperAt(int id, float bearingDegrees, float distance, float radius = Radius)
        {
            return new BumperCircle(id, Top + (Direction(bearingDegrees) * distance), radius);
        }

        // A mushroom at a bearing and distance from another one's centre.
        private static BumperCircle Beyond(int id, BumperCircle from, float bearingDegrees, float distance)
        {
            return new BumperCircle(id, from.Center + (Direction(bearingDegrees) * distance), Radius);
        }

        // Where a ball leaving the top of the hit mushroom along a direction touches the target, if it does.
        private static bool TryLand(Vector2 leaving, BumperCircle target, out BumperContact landing)
        {
            return TryLandFrom(Top, leaving, target, out landing);
        }

        private static bool TryLandFrom(Vector2 from, Vector2 leaving, BumperCircle target, out BumperContact landing)
        {
            var lands = CircleContact.TryFindEntry(
                from, leaving, target.Center, target.Radius, out _, out var distance);
            landing = lands ? new BumperContact(from + (leaving.normalized * distance), leaving, target) : default;
            return lands;
        }

        // The landing's impact parameter as a share of the radius: 0 dead centre, 1 a graze.
        private static float OffsetOf(BumperContact landing)
        {
            return MissDistance(landing.Point, landing.Incoming, landing.Bumper.Center) / landing.Bumper.Radius;
        }

        private static MushroomChainState Chain(int count)
        {
            return new MushroomChainState { Count = count };
        }

        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        // How far the line from a point along a direction passes from a centre.
        private static float MissDistance(Vector2 from, Vector2 direction, Vector2 center)
        {
            return Mathf.Abs(Cross(center - from, direction.normalized));
        }

        private static void AssertDirection(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance);
            Assert.AreEqual(expected.y, actual.y, Tolerance);
        }

        private sealed class StubSettings : IMushroomSettings
        {
            public MushroomChainRules Rules { get; set; }
            public bool ForgivingKick { get; set; }
            public BumperKickTuning Kick { get; set; }
            public float Radius { get; set; }
            public MushroomPlacementLimits Placement { get; set; }
            public int PermanentSlotLimit { get; set; }
            public MushroomLeanTuning Lean { get; set; }
            public MushroomIdleTuning Idle { get; set; }
            public MushroomSporeTuning Spores { get; set; }
            public MushroomCutTuning Cut { get; set; }
            public int RegrowBeats { get; set; }
            public MushroomBlastTuning Blast { get; set; }
        }
    }
}

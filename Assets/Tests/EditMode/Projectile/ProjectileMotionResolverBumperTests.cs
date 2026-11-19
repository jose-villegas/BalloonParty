using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Tests.Slots;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     The live bounce off a meadow mushroom, headless (phase 3 of <c>Plans/PLAN-MeadowMushrooms.md</c>):
    ///     <see cref="ProjectileMotionResolver.Step" /> flies each step's stretch against the bumper circles before
    ///     the walls. Unless a test says otherwise the bounce is the mirror reflection, so every expected point
    ///     can be worked out by hand. A ball that is piercing bounces off none: it cuts the caps on its stretch
    ///     and flies on (decision J), which the tests of a lance hold.
    /// </summary>
    [TestFixture]
    public class ProjectileMotionResolverBumperTests
    {
        private const float Tolerance = 1e-4f;
        private const float Radius = 0.5f;

        // ProjectileFlightConfig's speed, the Snipe's speed buff on top, and the fixed step: what a lance flies.
        private const float BaseSpeed = 9.869022f;
        private const float LanceSpeed = BaseSpeed * 1.6f;
        private const float FixedStep = 0.02f;

        // The shipped contact radius: a mushroom's 0.3 plus the ball's 0.125.
        private const float ShippedContactRadius = 0.425f;

        // A 10-wide box centred on the origin, as ProjectileMotionResolverTests uses.
        private static readonly Vector4 Walls = new(5f, 5f, -5f, -5f);

        private static readonly BumperCircle Above = new(0, new Vector2(0f, 2f), Radius);
        private static readonly BumperCircle Below = new(1, new Vector2(0f, -2f), Radius);

        private FakeBumperField _field;
        private MushroomSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _field = new FakeBumperField();
            _settings = new MushroomSettings();
        }

        [Test]
        public void Step_IntoABumperHeadOn_BouncesBackAndCarriesTheRestOfTheStep()
        {
            // From the origin straight up, 2 units in the step. The circle's lowest point is at y = 1.5: 1.5 units
            // flown in, the last 0.5 flown back out.
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            var step = resolver.Step(model, Vector3.zero, 2f);

            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, step.Outcome);
            Assert.AreEqual(1, step.BumperBounces);
            AssertPoint(new Vector2(0f, 1.5f), step.WallContact, "the contact is where the centre entered the circle");
            AssertPoint(new Vector2(0f, 1f), step.Position, "1.5 in, the remaining 0.5 back out");
            AssertPoint(Vector2.down, step.Direction, "a head-on hit comes straight back");
            AssertPoint(Vector2.down, model.Direction, "the model is turned too");
        }

        [Test]
        public void Step_IntoABumperOffCentre_LeavesAsTheBounceRuleSays()
        {
            // Up the line x = 0.3 into a circle of radius 0.5 at (0, 2): entry at y = 2 - 0.4 = 1.6, where the
            // surface normal is (0.6, -0.8). The mirror of (0, 1) about it is (0.96, -0.28).
            var rule = new TrueReflectRule();
            var resolver = Resolver(rule, Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            var step = resolver.Step(model, new Vector3(0.3f, 0f, 0f), 2f);

            var contact = new BumperContact(new Vector2(0.3f, 1.6f), Vector2.up, Above);
            var expected = rule.Resolve(contact, _field.Bumpers, default);
            AssertPoint(new Vector2(0.96f, -0.28f), expected, "the worked example");
            AssertPoint(expected, step.Direction, "the ball leaves as the rule answers for the exact entry point");
            AssertPoint(new Vector2(0.3f, 1.6f), step.WallContact, "entry point");
            AssertPoint(new Vector2(0.3f, 1.6f) + (expected * 0.4f), step.Position, "0.4 of the step left to fly");
        }

        [Test]
        public void Step_IntoABumper_AsksTheRuleWithTheChainAlreadyCounted()
        {
            var rule = new RecordingRule(Vector2.down);
            var resolver = Resolver(rule, Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            resolver.Step(model, Vector3.zero, 2f);

            Assert.AreEqual(1, rule.Calls);
            Assert.AreEqual(1, rule.LastChain.Count, "OnBounce first, then the rule");
            Assert.AreEqual(Above.Id, rule.LastChain.LastWall);
            Assert.AreEqual(2, rule.LastBumperCount, "the rule is handed every bumper on the board");
            AssertPoint(new Vector2(0f, 1.5f), rule.LastContact.Point, "the exact entry point, not the step's end");
            AssertPoint(Vector2.up, rule.LastContact.Incoming, "the direction the ball came in along");
        }

        [Test]
        public void Step_GrazingABumper_BouncesAtTheExactEntryPoint()
        {
            // Along y = 0.42 past a shipped circle at the origin: the chord is only 0.13 long. Entry at
            // x = -sqrt(0.425^2 - 0.42^2) = -0.065.
            var bumper = new BumperCircle(0, Vector2.zero, ShippedContactRadius);
            var resolver = Resolver(new TrueReflectRule(), bumper);
            var model = NewModel(Vector2.right, speed: 1f, shields: 2);

            var step = resolver.Step(model, new Vector3(-1f, 0.42f, 0f), 2f);

            Assert.AreEqual(1, step.BumperBounces);
            AssertPoint(new Vector2(-0.065f, 0.42f), step.WallContact, "entry point");
            Assert.AreEqual(ShippedContactRadius, ((Vector2)step.WallContact).magnitude, Tolerance, "on the circle");
            AssertPoint(new Vector2(0.953218f, 0.302281f), step.Direction, "a shallow turn off a shallow graze");
        }

        [Test]
        public void Step_ABallAsFastAsALanceThatWouldJumpTheBumperBetweenTwoSteps_StillBounces()
        {
            // At a lance's speed a ball flies 0.316 a fixed step. Along y = 0.4 a shipped circle's chord is 0.287
            // long, so a step that starts just short of it ends just past it: both ends outside, the bumper in
            // between.
            var bumper = new BumperCircle(0, Vector2.zero, ShippedContactRadius);
            var resolver = Resolver(new TrueReflectRule(), bumper);
            var model = NewModel(Vector2.right, LanceSpeed, shields: 2);
            var start = new Vector3(-0.15f, 0.4f, 0f);
            var unbentEnd = (Vector2)start + (Vector2.right * (LanceSpeed * FixedStep));
            Assert.Greater(((Vector2)start).magnitude, ShippedContactRadius, "the step starts outside the circle");
            Assert.Greater(unbentEnd.magnitude, ShippedContactRadius, "and would end outside it");

            var step = resolver.Step(model, start, FixedStep);

            var entryX = -Mathf.Sqrt((ShippedContactRadius * ShippedContactRadius) - (0.4f * 0.4f));
            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, step.Outcome, "the whole stretch is swept");
            AssertPoint(new Vector2(entryX, 0.4f), step.WallContact, "the exact entry, however fast the ball is");
            Assert.Greater(step.Direction.y, 0f, "turned up and away off the circle's upper side");
        }

        // The same step flown by a lance: both ends outside the circle, and the cap between them is cut, for the
        // whole stretch is swept for a cut as it is for a bounce.
        [Test]
        public void Step_ALanceThatWouldJumpTheCapBetweenTwoSteps_StillCutsIt()
        {
            var bumper = new BumperCircle(0, Vector2.zero, ShippedContactRadius);
            var resolver = Resolver(new TrueReflectRule(), bumper);
            var model = NewModel(Vector2.right, LanceSpeed, shields: 2);
            model.IsPiercing.Value = true;
            var start = new Vector3(-0.15f, 0.4f, 0f);
            var unbentEnd = start + (Vector3.right * (LanceSpeed * FixedStep));

            var step = resolver.Step(model, start, FixedStep);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            AssertPoint(unbentEnd, step.Position, "flown on as if nothing stood there");
            Assert.AreEqual(1, resolver.StepCapCuts.Count);
            Assert.AreEqual(0, _field.Bumpers.Count);
        }

        [Test]
        public void Step_ABounce_SpendsNoShieldAndCountsNoWallHit()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            resolver.Step(model, Vector3.zero, 2f);

            Assert.AreEqual(2, model.ShieldsRemaining.Value, "walls end flights, mushrooms do not");
            Assert.AreEqual(0, model.Flight.WallHitSequence, "not a wall hit: nothing for a tap to belong to");
        }

        [Test]
        public void Step_ABounceWithNoShieldLeft_DoesNotDestroyTheBall()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 0);

            var step = resolver.Step(model, Vector3.zero, 2f);

            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, step.Outcome);
            Assert.AreEqual(0, model.ShieldsRemaining.Value);
        }

        [Test]
        public void Step_ABounce_RaisesTheCount_AndTheWallThatCountsItsBounceResetsIt()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            resolver.Step(model, Vector3.zero, 2f);

            Assert.AreEqual(1, model.Flight.MushroomChain.Count);
            Assert.AreEqual(1, model.Flight.MushroomChain.Bounces);
            Assert.AreEqual(Above.Id, model.Flight.MushroomChain.LastWall);

            // Now heading down from (0, 1): three steps of 1.5 reach y = -3.5; the fourth crosses the wall at -5.
            var position = new Vector3(0f, 1f, 0f);
            for (var i = 0; i < 3; i++)
            {
                var moved = resolver.Step(model, position, 1.5f);
                Assert.AreEqual(ProjectileStepOutcome.Moved, moved.Outcome);
                Assert.AreEqual(1, model.Flight.MushroomChain.Count, "flying on keeps the count");
                position = moved.Position;
            }

            var bounced = resolver.Step(model, position, 2f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, bounced.Outcome);
            Assert.AreEqual(1, model.Flight.ConsecutiveWallBounces, "the wall bounce is counted on this step");
            Assert.AreEqual(1, model.ShieldsRemaining.Value);
            Assert.AreEqual(0, model.Flight.MushroomChain.Count, "and on this step the chain starts again");
            Assert.AreEqual(0, model.Flight.MushroomChain.Bounces);
            Assert.AreEqual(MushroomChainState.NoBumper, model.Flight.MushroomChain.LastWall);
        }

        [Test]
        public void Step_TheWallOfAPierceDischarge_ResetsTheCountToo()
        {
            // That wall returns before the empty-bounce counter is touched; the chain must not ride through it.
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.down, speed: 1f, shields: 2);
            model.IsPiercing.Value = true;
            model.Flight.MushroomChain.Count = 2;
            model.Flight.MushroomChain.Bounces = 2;
            model.Flight.PendingPierceHits.Add(
                new PendingPierceHit(Substitute.For<BalloonParty.Balloon.Model.IBalloonModel>(), Vector3.zero));

            var step = resolver.Step(model, new Vector3(3f, -4.5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome);
            Assert.AreEqual(0, model.Flight.MushroomChain.Count);
            Assert.AreEqual(0, model.Flight.MushroomChain.Bounces);
        }

        [Test]
        public void Step_TwoBumpersInOneStep_BouncesOffBoth()
        {
            // Up into the first at (0, 1.5) after 0.5, down 3 to the second at (0, -1.5), then 1 back up.
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.Flight.SegmentStartPosition = new Vector3(0f, 1f, 0f);

            var step = resolver.Step(model, new Vector3(0f, 1f, 0f), 4.5f);

            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, step.Outcome);
            Assert.AreEqual(2, step.BumperBounces);
            var bounces = resolver.StepBumperBounces;
            Assert.AreEqual(2, bounces.Count);
            Assert.AreEqual(Above.Id, bounces[0].BumperId);
            Assert.AreEqual(1, bounces[0].Count);
            AssertPoint(new Vector2(0f, 1.5f), bounces[0].Point, "first contact");
            AssertPoint(new Vector2(0f, 1f), bounces[0].LegStart, "the first leg began where the flight's segment did");
            Assert.AreEqual(Below.Id, bounces[1].BumperId);
            Assert.AreEqual(2, bounces[1].Count);
            AssertPoint(new Vector2(0f, -1.5f), bounces[1].Point, "second contact");
            AssertPoint(new Vector2(0f, 1.5f), bounces[1].LegStart, "the second leg began at the first bounce");
            AssertPoint(new Vector2(0f, -1.5f), step.WallContact, "the step reports the last contact");
            AssertPoint(new Vector2(0f, -0.5f), step.Position, "0.5 + 3 + 1 = the step's 4.5");
            AssertPoint(Vector2.up, step.Direction, "turned twice");
            Assert.AreEqual(2, model.Flight.MushroomChain.Count);
        }

        [Test]
        public void Step_BouncesInOneStretch_AreBounded()
        {
            // Two facing bumpers 3 apart and a step of 26: 0.5 to the first bounce, 3 more for each after it, so a
            // ninth would fit at 24.5. The loop stops at eight (21.5 flown, on the lower one) and the 4.5 left is
            // flown straight up untested, through the upper one, for the next step to pick the ball up from.
            // With no chain target no bounce arms, so none ends the step early.
            SetRules(new MushroomChainRules(chainTarget: 0));
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            var step = resolver.Step(model, new Vector3(0f, 1f, 0f), 26f);

            Assert.AreEqual(8, step.BumperBounces, "a hard bound on the bounces of one stretch");
            AssertPoint(new Vector2(0f, 3f), step.Position, "the rest of the stretch is still flown");
            Assert.AreEqual(8, model.Flight.MushroomChain.Bounces);
        }

        // The bounce that arms is the end of its step (the ball is armed by whoever hears the step's bounces,
        // after the step). Two facing mushrooms and a step long enough for five bounces: the third arms and stops
        // it.
        [Test]
        public void Step_TheBounceThatArms_EndsTheStepOnTheMushroom()
        {
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            var step = resolver.Step(model, new Vector3(0f, 1f, 0f), 14f);

            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, step.Outcome);
            Assert.AreEqual(3, step.BumperBounces, "up, down, up: the third reaches the chain target");
            Assert.AreEqual(MushroomReward.Arm, resolver.StepBumperBounces[2].Reward);
            AssertPoint(new Vector2(0f, 1.5f), step.Position, "the ball stops where it bounced");
            AssertPoint(Vector2.down, step.Direction, "turned, ready for the next step");
            AssertPoint(Vector2.down, model.Direction, "the model too");
            AssertPoint(new Vector2(0f, 1.5f), model.Flight.SegmentStartPosition, "and its next leg starts there");
            Assert.AreEqual(3, model.Flight.MushroomChain.Count);
        }

        // From the bounce that armed it the ball is a lance: the next step flies it down through the lower cap,
        // off the floor and back up through the cap that armed it, cutting both and bouncing off neither. The cap
        // that armed it is not cut on the way out (the ball is leaving it), only once the ball comes back to it.
        [Test]
        public void Step_OnceArmed_TheLanceCutsTheFacingCaps_AndBouncesOffNeither()
        {
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            var armed = resolver.Step(model, new Vector3(0f, 1f, 0f), 14f);
            Assert.AreEqual(MushroomReward.Arm, resolver.StepBumperBounces[2].Reward);
            Assert.AreEqual(0, resolver.StepCapCuts.Count, "the arming bounce is a bounce: nothing is cut by it");
            model.IsPiercing.Value = true;

            // 3 down to the lower cap's edge, 6.5 to the floor, and 7.5 back up: past the upper cap's edge at 13.
            var next = resolver.Step(model, armed.Position, 14f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, next.Outcome, "the floor is the step's only turn");
            Assert.AreEqual(0, next.BumperBounces);
            AssertPoint(new Vector2(0f, 2.5f), next.Position, "flown its whole length, unbent");
            AssertPoint(Vector2.up, next.Direction, "as the floor left it");
            Assert.AreEqual(2, resolver.StepCapCuts.Count);
            Assert.AreEqual(Below.Id, resolver.StepCapCuts[0].BumperId, "the lower cap, on the way down");
            Assert.AreEqual(Above.Id, resolver.StepCapCuts[1].BumperId, "and the upper one, on the way back up");
            Assert.AreEqual(0, _field.Bumpers.Count);
            Assert.AreEqual(1, model.ShieldsRemaining.Value, "one shield, for the floor");
        }

        [Test]
        public void Step_TheBounceThatArms_WithAWallJustBeyond_LeavesTheWallToTheNextStep([Values(0, 1)] int shields)
        {
            // 0.5 up to the mushroom, and a step of 10: flown on, the ball would come back down past the bottom
            // wall in this same step, as an ordinary ball, and with no shield would die there before being armed.
            var underTheWall = new BumperCircle(0, new Vector2(0f, 4f), Radius);
            var resolver = Resolver(new TrueReflectRule(), underTheWall);
            var model = NewModel(Vector2.up, speed: 1f, shields);
            model.Flight.MushroomChain.Count = _settings.Rules.ChainTarget - 1;
            model.Flight.MushroomChain.RememberLast(7);

            var step = resolver.Step(model, new Vector3(0f, 3f, 0f), 10f);

            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, step.Outcome, "the wall is not reached in this step");
            Assert.AreEqual(MushroomReward.Arm, resolver.StepBumperBounces[0].Reward);
            AssertPoint(new Vector2(0f, 3.5f), step.Position, "the step ends on the mushroom");
            Assert.AreEqual(shields, model.ShieldsRemaining.Value, "no shield spent");
            Assert.AreEqual(0, model.Flight.WallHitSequence);
            Assert.AreEqual(_settings.Rules.ChainTarget, model.Flight.MushroomChain.Count, "the chain stands, unreset");

            // The next step, flown as the lance the ball has been armed as, is the one that meets the wall.
            model.IsPiercing.Value = true;
            var next = resolver.Step(model, step.Position, 10f);

            Assert.AreEqual(
                shields > 0 ? ProjectileStepOutcome.Bounced : ProjectileStepOutcome.Destroyed, next.Outcome);
            Assert.AreEqual(-5f, next.WallContact.y, Tolerance, "8.5 down from the mushroom");
            Assert.AreEqual(0, resolver.StepCapCuts.Count, "the cap that armed the ball is not cut by it as it leaves");
            Assert.AreEqual(1, _field.Bumpers.Count);
        }

        [Test]
        public void Step_TheBounceThatArms_OnTheWayBackOffAWall_EndsTheStepThere()
        {
            // The wall, then a mushroom on the stretch flown back off it (as in
            // Step_AMushroomOnTheWayBackOffAWall_IsMetInTheSameStep), with a chain target of 1 so that bounce arms:
            // the wall is settled, as the ordinary ball's it was, and the step ends on the mushroom.
            SetRules(new MushroomChainRules(chainTarget: 1));
            var nearTheFloor = new BumperCircle(0, new Vector2(0f, -3.5f), Radius);
            var resolver = Resolver(new TrueReflectRule(), nearTheFloor);
            var model = NewModel(new Vector2(-1f, -1f).normalized, speed: 1f, shields: 2);

            var step = resolver.Step(model, new Vector3(3f, -4f, 0f), 4.5f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome, "the wall's outcome stands");
            Assert.AreEqual(1, model.ShieldsRemaining.Value, "one shield, for the wall before the arming");
            Assert.IsTrue(resolver.StepBumperBounces[0].AfterWall);
            Assert.AreEqual(MushroomReward.Arm, resolver.StepBumperBounces[0].Reward);
            AssertPoint(new Vector2(0.5f, -3.5f), step.Position, "the step ends on the mushroom", 1e-3f);
            AssertPoint(new Vector2(1f, 1f).normalized, step.Direction, "turned there", 1e-3f);
        }

        [Test]
        public void Step_TheBumperBeingLeft_IsNotReadAsEnteredAgain()
        {
            // The worst a bounce rule could answer: on into the very bumper it was asked about. The first step
            // ends inside its circle; the second starts there, still flying the direction the ball left along.
            var leaving = new Vector2(0.6f, 0.8f);
            var resolver = Resolver(new RecordingRule(leaving), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            var first = resolver.Step(model, new Vector3(0f, 1f, 0f), 1f);

            Assert.AreEqual(1, first.BumperBounces, "one bounce, and the rest of the step is not tested against it");
            AssertPoint(new Vector2(0.3f, 1.9f), first.Position, "0.5 of the step left along the leaving direction");
            Assert.Less(((Vector2)first.Position - Above.Center).magnitude, Radius, "which is inside the circle");

            var second = resolver.Step(model, first.Position, 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, second.Outcome, "still leaving: not a new contact");
            Assert.AreEqual(1, model.Flight.MushroomChain.Bounces);
        }

        [Test]
        public void Step_AfterTheBallHasTurned_TheSameBumperBouncesItAgain()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            resolver.Step(model, Vector3.zero, 2f);

            // Something else (a balloon's deflect) sends it back up at the mushroom it just left.
            model.Direction = Vector2.up;
            var again = resolver.Step(model, new Vector3(0f, 1f, 0f), 1f);

            Assert.AreEqual(1, again.BumperBounces, "a turn ends the leaving: the mushroom is in its way again");
            Assert.AreEqual(2, model.Flight.MushroomChain.Bounces);
            Assert.AreEqual(1, model.Flight.MushroomChain.Count, "the same mushroom twice in a row counts once");
        }

        [Test]
        public void Step_ABallFoundInsideABumper_BouncesWhereItsLineCameIn()
        {
            // Set down 0.2 inside the circle (as a deflect's carried remainder can), heading on in: the entry is
            // 0.2 behind it, and that distance is carried along the new direction with the step's own.
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            var step = resolver.Step(model, new Vector3(0f, 1.7f, 0f), 1f);

            Assert.AreEqual(1, step.BumperBounces);
            AssertPoint(new Vector2(0f, 1.5f), step.WallContact, "where the line entered, behind the ball");
            AssertPoint(new Vector2(0f, 0.3f), step.Position, "the 0.2 already flown and the step's 1, back out");
        }

        [Test]
        public void Step_ASpentStretch_FliesThroughTheBumpersUnturned()
        {
            var resolver = Resolver(new RecordingRule(Vector2.down), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.Flight.MushroomChain.Bounces = _settings.Rules.MaxBouncesPerStretch;
            model.Flight.MushroomChain.Count = 1;

            var step = resolver.Step(model, Vector3.zero, 2f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome, "the mushrooms let this ball through");
            Assert.AreEqual(0, step.BumperBounces);
            AssertPoint(new Vector2(0f, 2f), step.Position, "straight on, through the middle of the mushroom");
            AssertPoint(Vector2.up, model.Direction, "not turned");
            Assert.AreEqual(_settings.Rules.MaxBouncesPerStretch, model.Flight.MushroomChain.Bounces, "nothing counted");
            Assert.AreEqual(1, model.Flight.MushroomChain.Count);
        }

        [Test]
        public void Step_TheBounceThatSpendsTheStretch_IsTheLastUntilAWall()
        {
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.Flight.MushroomChain.Bounces = _settings.Rules.MaxBouncesPerStretch - 1;

            // The step is long enough to reach the second bumper after the first; the cap is met at the first.
            var step = resolver.Step(model, new Vector3(0f, 1f, 0f), 4.5f);

            Assert.AreEqual(1, step.BumperBounces, "the last bounce the stretch is allowed");
            AssertPoint(new Vector2(0f, -2.5f), step.Position, "then 4 units straight down, through the second");

            // A wall clears the tally, and the mushrooms bounce the ball again.
            model.Direction = Vector2.down;
            resolver.Step(model, new Vector3(3f, -4.5f, 0f), 1f);
            model.Direction = Vector2.up;
            var after = resolver.Step(model, Vector3.zero, 2f);

            Assert.AreEqual(1, after.BumperBounces, "a fresh stretch");
        }

        [Test]
        public void Step_AMushroomBetweenTheBallAndAWall_IsMetFirst()
        {
            // The step would carry the ball a unit past the top wall. The mushroom under the wall turns it first.
            var underTheWall = new BumperCircle(0, new Vector2(0f, 4f), Radius);
            var resolver = Resolver(new TrueReflectRule(), underTheWall);
            var model = NewModel(Vector2.up, speed: 1f, shields: 1);

            var step = resolver.Step(model, new Vector3(0f, 3f, 0f), 3f);

            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, step.Outcome, "no wall was reached");
            AssertPoint(new Vector2(0f, 3.5f), step.WallContact, "the mushroom's lowest point");
            AssertPoint(new Vector2(0f, 1f), step.Position, "0.5 up, 2.5 back down");
            Assert.AreEqual(1, model.ShieldsRemaining.Value, "the wall was never touched");
        }

        [Test]
        public void Step_ABounceThenAWallInOneStep_ReportsBoth()
        {
            // 0.5 up to the mushroom, then 9.5 down: past the bottom wall at -5 by a unit, mirrored back to -4.
            var underTheWall = new BumperCircle(0, new Vector2(0f, 4f), Radius);
            var resolver = Resolver(new TrueReflectRule(), underTheWall);
            var model = NewModel(Vector2.up, speed: 1f, shields: 1);

            var step = resolver.Step(model, new Vector3(0f, 3f, 0f), 10f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome, "the wall's outcome stands");
            Assert.AreEqual(1, step.BumperBounces, "and the mushroom bounce is reported beside it");
            Assert.IsFalse(resolver.StepBumperBounces[0].AfterWall);
            Assert.AreEqual(1, resolver.StepBumperBounces[0].Count, "counted before the wall cleared the chain");
            AssertPoint(new Vector2(0f, -5f), step.WallContact, "the wall contact is the wall's");
            AssertPoint(new Vector2(0f, -4f), step.Position, "mirrored off the bottom wall");
            Assert.AreEqual(0, model.ShieldsRemaining.Value, "the wall cost its shield");
            Assert.AreEqual(0, model.Flight.MushroomChain.Count, "and ended the chain");
        }

        [Test]
        public void Step_AMushroomOnTheWayBackOffAWall_IsMetInTheSameStep()
        {
            // Down-left from (3, -4): the wall at (2, -5) after 1.414, then up-left for the 3.086 left of the
            // step. That stretch meets a mushroom at (0, -3.5) after 2.121, at its rightmost point (0.5, -3.5),
            // and leaves it up-right with 0.964 to fly.
            var nearTheFloor = new BumperCircle(0, new Vector2(0f, -3.5f), Radius);
            var resolver = Resolver(new TrueReflectRule(), nearTheFloor);
            var diagonal = new Vector2(-1f, -1f).normalized;
            var model = NewModel(diagonal, speed: 1f, shields: 2);
            model.Flight.MushroomChain.Count = 2;

            var step = resolver.Step(model, new Vector3(3f, -4f, 0f), 4.5f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome);
            Assert.AreEqual(1, step.BumperBounces);
            var bounce = resolver.StepBumperBounces[0];
            Assert.IsTrue(bounce.AfterWall, "met on the way back off the wall");
            AssertPoint(new Vector2(0.5f, -3.5f), bounce.Point, "the mushroom's rightmost point", 1e-3f);
            Assert.AreEqual(1, bounce.Count, "the wall cleared the chain, then this bounce began a new one");
            AssertPoint(new Vector2(1f, 1f).normalized, step.Direction, "mirrored about the horizontal normal", 1e-3f);
            AssertPoint(
                new Vector2(-1f, 1f).normalized, step.WallDirection,
                "the wall's own direction is kept beside it: up and to the left, as the ball left the floor", 1e-3f);
            AssertPoint(new Vector2(1.18198f, -2.81802f), step.Position, "0.964 on from the mushroom", 1e-3f);
            Assert.AreEqual(-5f, step.WallContact.y, Tolerance, "the wall contact is still the wall's");
            Assert.AreEqual(1, model.ShieldsRemaining.Value, "one shield, for the wall");
        }

        [Test]
        public void Step_ACornerThenAMushroomOnTheWayBack_IsFlownAlongTheLegsTheBallReallyFlew()
        {
            // Up and to the right at (0.8, 0.6) into the top right corner: the top wall at (4.8, 5) after 0.25,
            // the right wall at (5, 4.85) after another 0.25, then down and to the left along (-0.8, -0.6) with
            // 2.5 of the step left. A mushroom stands on that last leg, its centre 1.5 along it, so the ball meets
            // it head on after 1, at (4.2, 4.25), and comes straight back with 1.5 to fly: 1 of it gets it to the
            // right wall, where it stops for the next step to count.
            //
            // After a corner the ball's direction is not the direction of that last leg (the two walls are summed
            // into one diagonal normal), so the stretch cannot be found by walking back from where the step ended.
            var onTheWayBack = new BumperCircle(0, new Vector2(3.8f, 3.95f), Radius);
            var resolver = Resolver(new TrueReflectRule(), onTheWayBack);
            var model = NewModel(new Vector2(0.8f, 0.6f), speed: 1f, shields: 3);

            var step = resolver.Step(model, new Vector3(4.6f, 4.85f, 0f), 3f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome);
            Assert.AreEqual(2, model.ShieldsRemaining.Value, "a corner is one bounce and one shield");
            Assert.AreEqual(1, step.BumperBounces);
            var bounce = resolver.StepBumperBounces[0];
            Assert.IsTrue(bounce.AfterWall);
            AssertPoint(new Vector2(4.2f, 4.25f), bounce.Point, "where the last leg meets the mushroom", 1e-3f);
            AssertPoint(new Vector2(-0.8f, -0.6f), bounce.Incoming, "flown along the leg off the second wall", 1e-3f);
            AssertPoint(new Vector2(0.8f, 0.6f), step.Direction, "head on, so straight back", 1e-3f);
            AssertPoint(new Vector2(5f, 4.85f), step.Position, "stopped on the wall it was sent back to", 1e-3f);
        }

        [Test]
        public void Step_ACornerWithNoMushroomOnTheWayBack_IsLeftAsTheWallsResolvedIt()
        {
            // The same corner with the mushroom well off the path: the step is the walls' own, to the bit.
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(Walls);
            var plain = new ProjectileMotionResolver(config, new ProjectileTapResolver(config));
            var wired = Resolver(new TrueReflectRule(), new BumperCircle(0, new Vector2(-3f, -3f), Radius));
            var plainModel = NewModel(new Vector2(0.8f, 0.6f), speed: 1f, shields: 3);
            var wiredModel = NewModel(new Vector2(0.8f, 0.6f), speed: 1f, shields: 3);

            var expected = plain.Step(plainModel, new Vector3(4.6f, 4.85f, 0f), 3f);
            var actual = wired.Step(wiredModel, new Vector3(4.6f, 4.85f, 0f), 3f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, actual.Outcome);
            Assert.AreEqual(0, actual.BumperBounces);
            Assert.IsTrue(expected.Position.Equals(actual.Position));
            Assert.IsTrue(expected.Direction.Equals(actual.Direction));
            Assert.IsTrue(actual.WallDirection.Equals(actual.Direction), "no mushroom turned it: one direction");
        }

        [Test]
        public void Step_WallDirection_IsTheDirection_OnEveryStepNoMushroomTurnedAfterAWall()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);

            var moved = resolver.Step(NewModel(Vector2.up, speed: 1f, shields: 2), Vector3.zero, 1f);
            var bumped = resolver.Step(NewModel(Vector2.up, speed: 1f, shields: 2), Vector3.zero, 2f);
            var walled = resolver.Step(NewModel(Vector2.down, speed: 1f, shields: 2), new Vector3(3f, -4.5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, moved.Outcome);
            Assert.AreEqual(moved.Direction, moved.WallDirection);
            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, bumped.Outcome);
            Assert.AreEqual(bumped.Direction, bumped.WallDirection, "a mushroom before any wall: the ball's own direction");
            Assert.AreEqual(ProjectileStepOutcome.Bounced, walled.Outcome);
            Assert.AreEqual(walled.Direction, walled.WallDirection);
        }

        // The rule of decision J: a lance, however it was armed, does not bounce off a mushroom. It flies straight
        // on and cuts the cap, which has left the field when the step returns. Nothing about its run changes and
        // nothing is counted: a cut is no hit of the chain and does not break one.
        [Test]
        public void Step_ALance_CutsTheCapAndFliesOnUnbent_KeepingItsRunAndItsChain()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.IsPiercing.Value = true;
            model.IsCruising.Value = true;
            model.Flight.ConsecutiveWallBounces = 3;
            model.Flight.TotalCruiseTaps = 4;
            model.Flight.MushroomChain.Count = 2;
            model.Flight.MushroomChain.Bounces = 2;
            model.Flight.MushroomChain.RememberLast(5);

            var step = resolver.Step(model, Vector3.zero, 2f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome, "no bounce: the step only moved");
            Assert.AreEqual(0, step.BumperBounces);
            AssertPoint(new Vector2(0f, 2f), step.Position, "the whole step, straight through");
            AssertPoint(Vector2.up, step.Direction, "unbent");
            AssertPoint(Vector2.up, model.Direction, "the model too");
            Assert.IsTrue(model.IsPiercing.Value);
            Assert.IsTrue(model.IsCruising.Value, "an armed shot's run is not ended by a cut");
            Assert.AreEqual(3, model.Flight.ConsecutiveWallBounces);
            Assert.AreEqual(4, model.Flight.TotalCruiseTaps);
            Assert.AreEqual(2, model.ShieldsRemaining.Value, "and no shield is spent");
            Assert.AreEqual(2, model.Flight.MushroomChain.Count, "a cut does not count toward the chain");
            Assert.AreEqual(2, model.Flight.MushroomChain.Bounces);
            Assert.AreEqual(5, model.Flight.MushroomChain.LastWall, "nor break it");

            Assert.AreEqual(1, resolver.StepCapCuts.Count);
            var cut = resolver.StepCapCuts[0];
            Assert.AreEqual(Above.Id, cut.BumperId);
            Assert.AreEqual(new Vector2Int(Above.Id, 0), cut.Slot);
            AssertPoint(Above.Center, cut.Point, "through its middle: the cap is halved");
            AssertPoint(Vector2.up, cut.Direction, "along the lance's way");
            Assert.AreEqual(0, _field.Bumpers.Count, "the cap has left the field");
        }

        // What the cut's message carries: the line the cap splits along. A lance through the cap off its centre
        // gives the point of its path nearest the centre; one whose centre passes outside the cap, clipping it
        // with its edge alone, gives a point drawn in to the cap, so that a sliver comes off.
        [TestCase(0.1f, 0.1f)]
        [TestCase(-0.2f, -0.2f)]
        [TestCase(0.4f, 0.255f)]
        [TestCase(-0.42f, -0.255f)]
        public void Step_ACut_NamesAPointInsideTheCap_OnTheLineTheLanceFlew(float pathX, float pointX)
        {
            var cap = new BumperCircle(0, new Vector2(0f, 2f), ShippedContactRadius);
            var resolver = Resolver(new TrueReflectRule(), cap);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.IsPiercing.Value = true;

            resolver.Step(model, new Vector3(pathX, 0f, 0f), 4f);

            Assert.AreEqual(1, resolver.StepCapCuts.Count);
            var message = resolver.StepCapCuts[0].ToMessage();
            AssertPoint(new Vector2(pointX, 2f), message.Point, "level with the centre, on the lance's side of it");
            Assert.Less((message.Point - cap.Center).magnitude, _settings.Radius, "inside the cap itself");
            AssertPoint(Vector2.up, message.Direction, "the lance's direction");
            Assert.AreEqual(new Vector2Int(0, 0), message.Slot);
            Assert.AreEqual(MushroomCutCause.Lance, message.Cause, "a lance's cut says so");
        }

        [Test]
        public void Step_ALanceThroughTwoCaps_CutsBoth_NearestFirst_AndEachOnce()
        {
            // Listed far cap first, so the order is the flight's and not the list's.
            var near = new BumperCircle(4, new Vector2(0f, 1f), Radius);
            var far = new BumperCircle(2, new Vector2(0.2f, 3f), Radius);
            var resolver = Resolver(new TrueReflectRule(), far, near);
            var model = NewModel(Vector2.up, speed: 1f, shields: 50);
            model.IsPiercing.Value = true;
            var cuts = new List<int>();

            // Up through both, off the top wall and back down over the two stems, and round again.
            var position = new Vector3(0f, -1f, 0f);
            for (var i = 0; i < 40; i++)
            {
                var step = resolver.Step(model, position, 1.5f);
                position = step.Position;
                foreach (var cut in resolver.StepCapCuts)
                {
                    cuts.Add(cut.BumperId);
                }

                Assert.AreEqual(0, step.BumperBounces);
            }

            CollectionAssert.AreEqual(new[] { near.Id, far.Id }, cuts, "one cut a cap, however often it flies over");
            Assert.AreEqual(0, _field.Bumpers.Count);
        }

        // One step through a wall of two caps and on through a cap of another wall: all three are cut, the two
        // of the first wall (their circles overlap, and the lance's path enters both at once) before the third,
        // each once.
        [Test]
        public void Step_ALanceThroughTwoCapsOfOneWallAndACapOfAnother_CutsAllThreeInOneStep()
        {
            var left = new BumperCircle(0, 0, new Vector2(-0.375f, 2f), ShippedContactRadius);
            var right = new BumperCircle(1, 0, new Vector2(0.375f, 2f), ShippedContactRadius);
            var beyond = new BumperCircle(2, 1, new Vector2(0f, 3.9f), ShippedContactRadius);
            var resolver = Resolver(new TrueReflectRule(), left, right, beyond);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.IsPiercing.Value = true;

            var step = resolver.Step(model, Vector3.zero, 4.5f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            AssertPoint(new Vector2(0f, 4.5f), step.Position, "the whole step, unbent");
            Assert.AreEqual(0, step.BumperBounces);
            Assert.AreEqual(3, resolver.StepCapCuts.Count);
            CollectionAssert.AreEquivalent(
                new[] { left.Id, right.Id }, new[] { resolver.StepCapCuts[0].BumperId, resolver.StepCapCuts[1].BumperId },
                "the two caps of the wall, in either order");
            Assert.AreEqual(beyond.Id, resolver.StepCapCuts[2].BumperId, "then the cap of the other wall");
            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
                new[] { resolver.StepCapCuts[0].Slot, resolver.StepCapCuts[1].Slot, resolver.StepCapCuts[2].Slot },
                "each named by the slot it stood in, though the list shrank under the sweep");
            Assert.AreEqual(0, _field.Bumpers.Count);
        }

        // Decision K: every cap a lance cuts banks that flight one plain lance charge, on the flight's own
        // counter, and the cut says which of the flight's charges it was.
        [Test]
        public void Step_EveryCapALanceCuts_BanksTheFlightACharge_AndTheCutSaysWhichItWas()
        {
            var left = new BumperCircle(0, 0, new Vector2(-0.375f, 2f), ShippedContactRadius);
            var right = new BumperCircle(1, 0, new Vector2(0.375f, 2f), ShippedContactRadius);
            var beyond = new BumperCircle(2, 1, new Vector2(0f, 3.9f), ShippedContactRadius);
            var resolver = Resolver(new TrueReflectRule(), left, right, beyond);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.IsPiercing.Value = true;

            resolver.Step(model, Vector3.zero, 4.5f);

            Assert.AreEqual(3, model.Flight.BankedPierceCharges, "one a cap, with no limit");
            Assert.AreEqual(3, model.Flight.CapsCut);
            Assert.AreEqual(0, model.Flight.BankedRainbowPierceCharges, "plain charges");
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(i + 1, resolver.StepCapCuts[i].Charges, "the flight's first, second and third");
                var message = resolver.StepCapCuts[i].ToMessage();
                Assert.AreEqual(i + 1, message.Charges, "and the message carries it");
                Assert.AreEqual(MushroomCutCause.Lance, message.Cause);
            }
        }

        // The count is the flight's, step after step, and it counts cuts: a charge the shot already held (a
        // Snipe picked up mid-pierce) is added to and is not in the count.
        [Test]
        public void Step_TheCountOfCutsIsTheFlights_AndIsNotTheChargesItHolds()
        {
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 5);
            model.IsPiercing.Value = true;
            model.Flight.BankedPierceCharges = 1;

            resolver.Step(model, Vector3.zero, 3f);

            Assert.AreEqual(1, resolver.StepCapCuts.Count);
            Assert.AreEqual(1, resolver.StepCapCuts[0].Charges, "the first cap this flight cut");
            Assert.AreEqual(2, model.Flight.BankedPierceCharges, "on top of the one it held");

            // Off the top wall and down through the lower cap, steps later.
            var position = new Vector3(0f, 3f, 0f);
            var later = 0;
            for (var i = 0; i < 6; i++)
            {
                var step = resolver.Step(model, position, 2f);
                position = step.Position;
                foreach (var cut in resolver.StepCapCuts)
                {
                    later = cut.Charges;
                }
            }

            Assert.AreEqual(2, later, "the second, though no cap was cut in the steps between");
            Assert.AreEqual(2, model.Flight.CapsCut);
            Assert.AreEqual(3, model.Flight.BankedPierceCharges);
        }

        // A ball that is no lance cuts nothing and banks nothing, however often it bounces.
        [Test]
        public void Step_ABallThatBounces_BanksNothing()
        {
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 5);
            var position = Vector3.zero;

            for (var i = 0; i < 4; i++)
            {
                position = resolver.Step(model, position, 2f).Position;
            }

            Assert.AreEqual(0, model.Flight.BankedPierceCharges);
            Assert.AreEqual(0, model.Flight.CapsCut);
        }

        // Why the charge is banked in the step and not by whoever hears of the cut after it: a lance that has
        // plowed a tough cuts a cap and reaches its wall in one and the same step. The cap's charge is there when
        // the wall spends the pierce, so the lance is still a lance off that wall. With no cap on the way the same
        // step ends the pierce.
        [TestCase(true)]
        [TestCase(false)]
        public void Step_ACapCutInTheStepThatReachesTheDischargeWall_HasAlreadyKeptTheLanceArmed(bool capOnTheWay)
        {
            var cap = new BumperCircle(0, new Vector2(0f, 4f), ShippedContactRadius);
            var resolver = capOnTheWay ? Resolver(new TrueReflectRule(), cap) : Resolver(new TrueReflectRule(), Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 3);
            model.IsPiercing.Value = true;
            model.Flight.PendingPierceHits.Add(new PendingPierceHit(new BalloonModel(), new Vector3(0f, 3f, 0f)));

            var step = resolver.Step(model, new Vector3(0f, 3.2f, 0f), 2.5f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome, "the top wall, in this step");
            Assert.AreEqual(capOnTheWay ? 1 : 0, resolver.StepCapCuts.Count);
            Assert.AreEqual(capOnTheWay, model.IsPiercing.Value, "kept a lance by the charge its cut had banked");
            Assert.AreEqual(capOnTheWay ? 1 : 0, model.Flight.BankedPierceCharges,
                "the charge is the Snipe's to spend, when it hears of the discharge");
        }

        // A field that takes no cap out when it is told to cut one (an index it does not know, a slot that
        // holds no mushroom) must not hold the step: the sweep ends, nothing is cut and nothing announced, and
        // the lance flies its step as it would have.
        [Test]
        public void Step_AFieldThatCutsNothing_EndsTheSweep_WithNoCutAnnounced()
        {
            _field.RefusesToCut = true;
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.IsPiercing.Value = true;

            var step = resolver.Step(model, Vector3.zero, 3f);

            Assert.AreEqual(1, _field.CutsAsked, "asked once, not without end");
            Assert.AreEqual(0, resolver.StepCapCuts.Count, "a cap the field kept was not cut");
            Assert.AreEqual(0, model.Flight.BankedPierceCharges, "and a cap that was not cut banks nothing");
            Assert.AreEqual(0, model.Flight.CapsCut);
            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            AssertPoint(new Vector2(0f, 3f), step.Position, "and the lance flew on unbent");
            Assert.AreEqual(2, _field.Bumpers.Count);
        }

        // A lance cuts a cap while another ball is on its way out of it: that ball still names the cap as the
        // one it is leaving, though the cap is gone. Nothing is found in its place, the caps that are whole still
        // bounce the ball, and when it comes back over the stem it flies through.
        [Test]
        public void Step_ACapCutWhileAnotherBallIsLeavingIt_IsSimplyGoneForThatBall()
        {
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var ball = NewModel(Vector2.up, speed: 1f, shields: 2);
            var bounced = resolver.Step(ball, Vector3.zero, 2f);
            Assert.AreEqual(1, bounced.BumperBounces, "off the upper cap, and half a unit back down");
            Assert.AreEqual(Above.Id, ball.Flight.LeavingBumperId(ball.Direction));

            var lance = NewModel(Vector2.right, speed: 1f, shields: 2);
            lance.IsPiercing.Value = true;
            resolver.Step(lance, new Vector3(-1f, 2f, 0f), 2f);
            Assert.AreEqual(1, resolver.StepCapCuts.Count);
            Assert.AreEqual(Above.Id, resolver.StepCapCuts[0].BumperId);
            Assert.AreEqual(Above.Id, ball.Flight.LeavingBumperId(ball.Direction), "the ball still names it");

            // Down to the lower cap, which is whole and counts as the second hit, and back up.
            var down = resolver.Step(ball, bounced.Position, 4f);
            Assert.AreEqual(1, down.BumperBounces);
            Assert.AreEqual(Below.Id, resolver.StepBumperBounces[0].BumperId);
            Assert.AreEqual(2, resolver.StepBumperBounces[0].Count);
            AssertPoint(Vector2.up, down.Direction, "sent back up");

            // Up through where the upper cap stood: no bounce, no cut, on to the wall behind it.
            var up = resolver.Step(ball, down.Position, 6.5f);
            Assert.AreEqual(ProjectileStepOutcome.Bounced, up.Outcome, "the top wall");
            Assert.AreEqual(0, up.BumperBounces);
            Assert.AreEqual(0, resolver.StepCapCuts.Count);
            Assert.AreEqual(5f, up.WallContact.y, Tolerance);
        }

        // A cap that grows back round a ball: the ball, already inside the circle, bounces at the point its line
        // came in, as a ball set down inside a mushroom always did. (The game does not put one there: a cap that
        // is due waits while a ball is inside it.) A lance in the same place cuts the cap again.
        [Test]
        public void Step_ACapRegrownRoundABall_BouncesItWhereItsLineCameIn_AndALanceCutsItAgain(
            [Values(false, true)] bool lance)
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            _field.Cut(0);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.IsPiercing.Value = lance;
            var inside = resolver.Step(model, new Vector3(0f, 1f, 0f), 0.8f);
            Assert.AreEqual(ProjectileStepOutcome.Moved, inside.Outcome, "over the stem");
            AssertPoint(new Vector2(0f, 1.8f), inside.Position, "0.3 inside the circle the cap is to have");

            _field.Set(new[] { Above });
            var step = resolver.Step(model, inside.Position, 0.5f);

            if (lance)
            {
                Assert.AreEqual(1, resolver.StepCapCuts.Count, "cut again");
                Assert.AreEqual(0, step.BumperBounces);
                AssertPoint(new Vector2(0f, 2.3f), step.Position, "and flown on");
                return;
            }

            Assert.AreEqual(1, step.BumperBounces);
            AssertPoint(new Vector2(0f, 1.5f), resolver.StepBumperBounces[0].Point, "where its line entered the circle");
            AssertPoint(Vector2.down, step.Direction, "turned back");
            AssertPoint(new Vector2(0f, 0.7f), step.Position, "carrying what it flew since: 0.3 inside and the step's 0.5");
        }

        // A cut cap is no bumper for the ball that comes next: it flies through where the cap stood, nothing is
        // counted for it, and the rule that bends a bounce is handed the caps that are left.
        [Test]
        public void Step_AfterACut_AnOrdinaryBallFliesThroughWhereTheCapStood()
        {
            var resolver = Resolver(new TrueReflectRule(), Above, Below);
            var lance = NewModel(Vector2.up, speed: 1f, shields: 2);
            lance.IsPiercing.Value = true;
            resolver.Step(lance, Vector3.zero, 2f);
            Assert.AreEqual(1, _field.Bumpers.Count);

            var ball = NewModel(Vector2.up, speed: 1f, shields: 2);
            var step = resolver.Step(ball, Vector3.zero, 3f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            AssertPoint(new Vector2(0f, 3f), step.Position, "through the stem");
            Assert.AreEqual(0, ball.Flight.MushroomChain.Count);
            Assert.AreEqual(0, ball.Flight.MushroomChain.Bounces);
            Assert.AreEqual(0, resolver.StepCapCuts.Count, "the last step's cuts are gone by the next");

            // The lower cap is whole and still bounces it.
            var down = NewModel(Vector2.down, speed: 1f, shields: 2);
            Assert.AreEqual(1, resolver.Step(down, Vector3.zero, 2f).BumperBounces);
        }

        [Test]
        public void Step_AfterACut_TheBounceRuleIsHandedTheCapsThatAreLeft()
        {
            var rule = new RecordingRule(Vector2.up);
            var resolver = Resolver(rule, Above, Below);
            _field.Cut(0);

            resolver.Step(NewModel(Vector2.down, speed: 1f, shields: 2), Vector3.zero, 2f);

            Assert.AreEqual(1, rule.Calls);
            Assert.AreEqual(1, rule.LastBumperCount, "the cut cap is not among them");
            Assert.AreEqual(Below.Id, rule.LastContact.Bumper.Id);
        }

        // The kick never bends a ball toward a cut cap, and never lands it for one: with a cap cut the ball leaves
        // exactly as it does on a board that cap never stood on. Held for a cap that would be the kick's target
        // and for one that would be the follower the ball is landed for.
        [TestCase(1)]
        [TestCase(2)]
        public void Step_TheKick_IsBentAsIfTheCutCapHadNeverStood(int cutId)
        {
            // A ball straight down onto the right shoulder of a cap of wall 0. Its reflection leaves up and to the
            // right, 67 degrees off the x axis. A cap of wall 1 stands 10 degrees off that line, and a cap of wall
            // 2 beside it, the nearest cap to it that is not of its wall.
            var hit = new BumperCircle(0, 0, new Vector2(0f, -2f), Radius);
            var target = new BumperCircle(1, 1, new Vector2(2.278f, 1.845f), Radius);
            var beside = new BumperCircle(2, 2, new Vector2(3.6f, 1.2f), Radius);
            var kick = new ForgivingKickRule(_settings);
            var start = new Vector3(0.1f, 0f, 0f);

            var all = Resolver(kick, hit, target, beside).Step(NewModel(Vector2.down, 1f, 2), start, 2f).Direction;
            var mirror = Resolver(new TrueReflectRule(), hit).Step(NewModel(Vector2.down, 1f, 2), start, 2f).Direction;
            var without = cutId == 1
                ? Resolver(kick, hit, beside).Step(NewModel(Vector2.down, 1f, 2), start, 2f).Direction
                : Resolver(kick, hit, target).Step(NewModel(Vector2.down, 1f, 2), start, 2f).Direction;

            var resolver = Resolver(kick, hit, target, beside);
            _field.Cut(cutId);
            var afterTheCut = resolver.Step(NewModel(Vector2.down, 1f, 2), start, 2f).Direction;

            Assert.Greater(Vector2.Angle(all, mirror), 1f, "with every cap whole the ball is kicked");
            AssertPoint(without, afterTheCut, "the same direction as on the board without that cap", 1e-6f);
        }

        // A lance spent at a wall (it had pierced a tough, which shatters there) is an ordinary ball again from
        // that wall on, and bounces off the mushroom it meets on the way back off it.
        [Test]
        public void Step_ALanceSpentAtAWall_BouncesOffTheMushroomOnTheWayBack()
        {
            var nearTheFloor = new BumperCircle(0, new Vector2(0f, -3.5f), Radius);
            var resolver = Resolver(new TrueReflectRule(), nearTheFloor);
            var model = NewModel(new Vector2(-1f, -1f).normalized, speed: 1f, shields: 2);
            model.IsPiercing.Value = true;
            model.Flight.PendingPierceHits.Add(
                new PendingPierceHit(Substitute.For<BalloonParty.Balloon.Model.IBalloonModel>(), Vector3.zero));

            var step = resolver.Step(model, new Vector3(3f, -4f, 0f), 4.5f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome);
            Assert.IsFalse(model.IsPiercing.Value, "spent at the floor");
            Assert.AreEqual(1, step.BumperBounces, "so the mushroom bounces it");
            Assert.IsTrue(resolver.StepBumperBounces[0].AfterWall);
            Assert.AreEqual(1, resolver.StepBumperBounces[0].Count, "a new chain begins there");
            Assert.AreEqual(0, resolver.StepCapCuts.Count);
            Assert.AreEqual(1, _field.Bumpers.Count);
        }

        // The same step by a lance that keeps its pierce past the wall: the cap on the way back is cut and the
        // step ends where the walls alone would have left it.
        [Test]
        public void Step_ALanceStillArmedPastAWall_CutsTheCapOnTheWayBack()
        {
            var nearTheFloor = new BumperCircle(0, new Vector2(0f, -3.5f), Radius);
            var resolver = Resolver(new TrueReflectRule(), nearTheFloor);
            var diagonal = new Vector2(-1f, -1f).normalized;
            var model = NewModel(diagonal, speed: 1f, shields: 2);
            model.IsPiercing.Value = true;
            var withoutMushrooms = Resolver(new TrueReflectRule()).Step(
                NewModel(diagonal, speed: 1f, shields: 2), new Vector3(3f, -4f, 0f), 4.5f);
            _field.Set(new[] { nearTheFloor });

            var step = resolver.Step(model, new Vector3(3f, -4f, 0f), 4.5f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome);
            Assert.IsTrue(model.IsPiercing.Value);
            Assert.AreEqual(0, step.BumperBounces);
            Assert.AreEqual(1, resolver.StepCapCuts.Count);
            AssertPoint(withoutMushrooms.Position, step.Position, "where the walls alone leave it");
            AssertPoint(withoutMushrooms.Direction, step.Direction, "and heading as they leave it");
            Assert.AreEqual(0, _field.Bumpers.Count);
        }

        [Test]
        public void Step_AnUnarmedCruisingShot_EndsItsEmptyRunAtAMushroom()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.IsCruising.Value = true;
            model.Flight.ConsecutiveWallBounces = 3;
            model.Flight.TotalCruiseTaps = 2;

            resolver.Step(model, Vector3.zero, 2f);

            Assert.IsFalse(model.IsCruising.Value, "the corridor was not empty after all");
            Assert.AreEqual(0, model.Flight.ConsecutiveWallBounces);
            Assert.AreEqual(2, model.Flight.TotalCruiseTaps, "the speed it earned stays, as on a balloon contact");
        }

        [Test]
        public void Step_ABounce_StartsANewSegment()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            model.Flight.SegmentStartPosition = new Vector3(0f, -3f, 0f);
            model.Flight.SegmentElapsed = 5f;

            resolver.Step(model, Vector3.zero, 2f);

            AssertPoint(new Vector2(0f, 1.5f), model.Flight.SegmentStartPosition, "the glide and the sweeps measure from here");
            Assert.AreEqual(0f, model.Flight.SegmentElapsed, Tolerance);
        }

        [Test]
        public void Step_ABumperOutOfReach_ChangesNothing()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            Assert.AreEqual(0, step.BumperBounces);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), step.Position);
        }

        [Test]
        public void Step_TheBouncesOfAStep_AreGoneByTheNext()
        {
            var resolver = Resolver(new TrueReflectRule(), Above);
            var model = NewModel(Vector2.up, speed: 1f, shields: 2);
            resolver.Step(model, Vector3.zero, 2f);
            Assert.AreEqual(1, resolver.StepBumperBounces.Count);

            var next = resolver.Step(model, new Vector3(0f, 1f, 0f), 1f);

            Assert.AreEqual(0, next.BumperBounces);
            Assert.AreEqual(0, resolver.StepBumperBounces.Count);
        }

        [Test]
        public void Step_WiredToAnEmptyBoard_IsBitForBitTheResolverBuiltWithoutABumperField()
        {
            // The same shot through a resolver built with no bumper field and through one wired to an empty
            // board: every step identical, to the bit. Both sides are today's code, so this shows only that the
            // field's presence changes nothing. That today's code flies as the code before the mushrooms did is
            // ProjectileMotionResolverGoldenTests, against numbers captured from that code.
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(Walls);
            var plain = new ProjectileMotionResolver(config, new ProjectileTapResolver(config));
            var wired = Resolver(new ForgivingKickRule(_settings));
            var direction = new Vector2(0.37f, 0.93f).normalized;
            var plainModel = NewModel(direction, BaseSpeed, shields: 40);
            var wiredModel = NewModel(direction, BaseSpeed, shields: 40);
            var plainPosition = new Vector3(0.2f, -4f, 0f);
            var wiredPosition = plainPosition;

            for (var i = 0; i < 600; i++)
            {
                var expected = plain.Step(plainModel, plainPosition, FixedStep);
                var actual = wired.Step(wiredModel, wiredPosition, FixedStep);

                Assert.AreEqual(expected.Outcome, actual.Outcome, $"step {i}");
                Assert.IsTrue(expected.Position.Equals(actual.Position), $"step {i}: position");
                Assert.IsTrue(expected.WallContact.Equals(actual.WallContact), $"step {i}: wall contact");
                Assert.IsTrue(expected.Direction.Equals(actual.Direction), $"step {i}: direction");
                Assert.AreEqual(expected.Speed, actual.Speed, $"step {i}: speed");
                Assert.AreEqual(0, actual.BumperBounces);
                Assert.AreEqual(plainModel.ShieldsRemaining.Value, wiredModel.ShieldsRemaining.Value, $"step {i}");
                plainPosition = expected.Position;
                wiredPosition = actual.Position;
            }

            Assert.Less(plainModel.ShieldsRemaining.Value, 40, "the flight did reach walls");
        }

        [Test]
        public void Step_AShotIntoALayoutsWidestWindow_IsArmedByTheGameInFixedSteps(
            [Values(2, 3, 4)] int walls, [Values(1, 2, 3, 4, 5, 6, 7, 8)] int pick)
        {
            // A layout a level is dealt, the forgiving kick and the game's own stepping, at the ball's real speed:
            // the shot into the middle of the layout's widest window, which the simulator says arms the lance, is
            // armed by the game on the hit that brings its count to three, off more than one wall.
            var settings = new MushroomSettings();
            var layout = MushroomTestBoard.Layout(walls, pick * 11);
            var bumpers = MushroomTestBoard.Bumpers(layout);
            var shot = MushroomTestBoard.Direction(MushroomTestBoard.MiddleOfTheWidestWindow(layout, settings));
            _field.Set(bumpers);
            var resolver = Resolver(new ForgivingKickRule(settings), MushroomTestBoard.WallsClockwise);
            var model = NewModel(shot, BaseSpeed, shields: MushroomTestBoard.StartingShields);

            // The cannon's pivot rests under the bottom wall: the flight is picked up on its line, just inside.
            var position = (Vector3)(MushroomTestBoard.CannonPivot + (shot * (0.3f / shot.y)));
            var wallsHit = new HashSet<int>();
            var armedAt = 0;
            for (var i = 0; i < 4000 && armedAt == 0; i++)
            {
                var step = resolver.Step(model, position, FixedStep);
                for (var b = 0; b < step.BumperBounces; b++)
                {
                    var bounce = resolver.StepBumperBounces[b];
                    wallsHit.Add(bumpers[bounce.BumperId].Wall);
                    if (bounce.Reward == MushroomReward.Arm)
                    {
                        armedAt = bounce.Count;
                    }
                }

                Assert.IsTrue(
                    armedAt > 0 || step.Outcome != ProjectileStepOutcome.Destroyed, "armed before its last wall");
                position = step.Position;
            }

            Assert.AreEqual(settings.Rules.ChainTarget, armedAt, "armed on the hit that reaches the chain target");
            Assert.Greater(wallsHit.Count, 1, "by crossing from one wall of caps to another");
        }

        [Test]
        public void Step_OverManyFlights_NoBallPassesThroughOrComesToRestInsideAMushroom(
            [Values(1f, 1.6f, 4f, 9.4f)] float speedScale, [Values(true, false)] bool kicked)
        {
            // Random shots over layouts of the catalogue, from the ball's own speed up to the speed rail, by the kick and by
            // the mirror reflection: whole flights to their last wall. At 9.4 times its speed a ball flies 1.86 a
            // step, over twice a mushroom's width, and reaches mushrooms on the stretch it flies back off a wall.
            var walls = new WallLimits(MushroomTestBoard.WallsClockwise);
            var settings = new MushroomSettings();
            var rules = settings.Rules;
            var random = new System.Random(20261119);
            var bounces = 0;
            for (var seed = 1; seed <= 12; seed++)
            {
                var bumpers = MushroomTestBoard.Bumpers(MushroomTestBoard.Layout(2 + (seed % 3), seed * 5));
                _field.Set(bumpers);
                IBumperBounceRule rule = kicked ? new ForgivingKickRule(settings) : new TrueReflectRule();
                var resolver = Resolver(rule, MushroomTestBoard.WallsClockwise);
                for (var shot = 0; shot < 10; shot++)
                {
                    var angle = (float)(5.0 + (random.NextDouble() * 170.0)) * Mathf.Deg2Rad;
                    var model = NewModel(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), BaseSpeed * speedScale, 4);
                    Vector3 position = new Vector2((float)((random.NextDouble() * 3.0) - 1.5), -4.3f);
                    var flight = $"board {seed}, shot {shot}";
                    var destroyed = false;
                    for (var i = 0; i < 20000 && !destroyed; i++)
                    {
                        var from = position;
                        var leavingBefore = model.Flight.LeavingBumperId(model.Direction);
                        var spentBefore = MushroomChainRule.IsSpent(model.Flight.MushroomChain, rules);

                        var step = resolver.Step(model, position, FixedStep);

                        position = step.Position;
                        bounces += step.BumperBounces;
                        destroyed = step.Outcome == ProjectileStepOutcome.Destroyed;
                        Assert.LessOrEqual(model.Flight.MushroomChain.Bounces, rules.MaxBouncesPerStretch, flight);
                        if (destroyed || spentBefore || MushroomChainRule.IsSpent(model.Flight.MushroomChain, rules))
                        {
                            continue;
                        }

                        Assert.IsTrue(walls.Contains(position), $"{flight}: a step ended outside the walls");
                        var leaving = model.Flight.LeavingBumperId(model.Direction);
                        foreach (var bumper in bumpers)
                        {
                            Assert.IsFalse(
                                bumper.Id != leaving
                                && ((Vector2)position - bumper.Center).magnitude < bumper.Radius - 1e-3f,
                                $"{flight}: the ball came to rest inside mushroom {bumper.Id}");
                            Assert.IsFalse(
                                step.Outcome == ProjectileStepOutcome.Moved && bumper.Id != leavingBefore
                                && MushroomTestBoard.DistanceToSegment(bumper.Center, from, position)
                                < bumper.Radius - 1e-3f,
                                $"{flight}: a step flew through mushroom {bumper.Id} and reported nothing");
                        }
                    }

                    Assert.IsTrue(destroyed, $"{flight}: no bounce spends a shield, yet every flight ends at a wall");
                }
            }

            Assert.Greater(bounces, 0, "the flights did meet mushrooms");
        }

        private ProjectileMotionResolver Resolver(IBumperBounceRule rule, params BumperCircle[] bumpers)
        {
            _field.Set(bumpers);
            return Resolver(rule, Walls);
        }

        private ProjectileMotionResolver Resolver(IBumperBounceRule rule, Vector4 walls)
        {
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(walls);
            return new ProjectileMotionResolver(config, new ProjectileTapResolver(config), _field, rule, _settings);
        }

        private void SetRules(MushroomChainRules rules)
        {
            typeof(MushroomSettings)
                .GetField("_rules", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(_settings, rules);
        }

        private static ProjectileModel NewModel(Vector2 direction, float speed, int shields)
        {
            var model = new ProjectileModel
            {
                Direction = direction,
                Speed = speed,
                IsFree = true
            };
            model.ShieldsRemaining.Value = shields;
            return model;
        }

        private static void AssertPoint(Vector2 expected, Vector2 actual, string message, float tolerance = Tolerance)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, message + " (x)");
            Assert.AreEqual(expected.y, actual.y, tolerance, message + " (y)");
        }

        // A board's bumpers handed over as they are, each standing in a slot numbered after its id.
        private sealed class FakeBumperField : IBumperField
        {
            private readonly List<BumperCircle> _bumpers = new();

            public IReadOnlyList<BumperCircle> Bumpers => _bumpers;

            public Vector2Int SlotAt(int index)
            {
                return new Vector2Int(_bumpers[index].Id, 0);
            }

            /// <summary>When set, a cut takes nothing out: a field that cannot cut the cap it was handed.</summary>
            internal bool RefusesToCut { get; set; }

            internal int CutsAsked { get; private set; }

            public void Cut(int index)
            {
                CutsAsked++;
                if (!RefusesToCut)
                {
                    _bumpers.RemoveAt(index);
                }
            }

            internal void Set(IReadOnlyList<BumperCircle> bumpers)
            {
                _bumpers.Clear();
                _bumpers.AddRange(bumpers);
            }
        }

        // Answers one fixed direction and remembers what it was asked.
        private sealed class RecordingRule : IBumperBounceRule
        {
            private readonly Vector2 _leaving;

            internal int Calls { get; private set; }
            internal BumperContact LastContact { get; private set; }
            internal MushroomChainState LastChain { get; private set; }
            internal int LastBumperCount { get; private set; }

            internal RecordingRule(Vector2 leaving)
            {
                _leaving = leaving.normalized;
            }

            public Vector2 Resolve(
                in BumperContact contact, IReadOnlyList<BumperCircle> bumpers, in MushroomChainState chain)
            {
                Calls++;
                LastContact = contact;
                LastChain = chain;
                LastBumperCount = bumpers.Count;
                return _leaving;
            }
        }
    }
}

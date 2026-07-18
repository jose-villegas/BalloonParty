using System;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class ProjectileMotionResolverTests
    {
        // A 10-wide box centred on the origin: top +5, right +5, bottom −5, left −5
        // (clockwise convention x=top, y=right, z=bottom, w=left).
        private static readonly Vector4 Walls = new(5f, 5f, -5f, -5f);

        private ProjectileMotionResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<IGameConfiguration>();
            config.LimitsClockwise.Returns(Walls);
            _resolver = new ProjectileMotionResolver(config);
        }

        [Test]
        public void Step_WellInsideBounds_MovesWithoutBouncing()
        {
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);

            var step = _resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), step.Position);
            Assert.AreEqual(2, model.ShieldsRemaining.Value, "no wall hit — shields untouched");
        }

        [Test]
        public void Step_CrossingWallWithShield_MirrorsReflectsAndDecrements()
        {
            // Heading straight up from y=4.5 at speed 1 lands at 5.5 → mirrored back to 4.5: the
            // overshoot continues along the reflected heading (exact billiard, no time or lateral
            // offset lost), while the wall contact reports where the bounce visually happened.
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 1);

            var step = _resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome);
            Assert.AreEqual(4.5f, step.Position.y, 1e-4f, "overshoot mirrored back below the wall");
            Assert.AreEqual(5f, step.WallContact.y, 1e-4f, "bounce VFX anchor sits on the wall itself");
            Assert.AreEqual(0, model.ShieldsRemaining.Value, "one shield consumed");
            Assert.Less(model.Direction.y, 0f, "reflected downward off the top wall");
        }

        [Test]
        public void Step_CrossingWallWithNoShieldLeft_Destroys()
        {
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);

            var step = _resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Destroyed, step.Outcome);
            Assert.AreEqual(5f, step.Position.y, 1e-4f, "a dead shot stops AT the wall, not mirrored");
            Assert.AreEqual(-1, model.ShieldsRemaining.Value, "decrement crossed below zero");
        }

        [Test]
        public void Deflect_ReflectsDirectionOffBalloonSurfaceNormal()
        {
            // Projectile directly above the balloon, travelling down → reflects to travelling up.
            var model = NewModel(direction: Vector2.down, speed: 1f, shields: 3);

            _resolver.Deflect(model, new Vector3(0f, 1f, 0f), Vector3.zero, 0.4f);

            Assert.Greater(model.Direction.y, 0f, "bounced back upward off the balloon");
        }

        [Test]
        public void Deflect_ReturnsContactPointPlusReflectedRemainder()
        {
            // Trigger fired 0.1 deep inside the circle: snap to the surface entry (0, 0.4), then the
            // already-travelled 0.1 of penetration continues along the reflected (upward) heading —
            // the exact billiard continuation, so no distance or time is lost at the contact.
            var model = NewModel(direction: Vector2.down, speed: 1f, shields: 3);

            var contact = _resolver.Deflect(model, new Vector3(0f, 0.3f, 0f), Vector3.zero, 0.4f);

            Assert.AreEqual(0f, contact.x, 1e-4f);
            Assert.AreEqual(0.5f, contact.y, 1e-4f, "surface entry 0.4 plus the 0.1 remainder, reflected up");
        }

        [Test]
        public void Deflect_NearWallBalloon_ClampsResultInsideWalls()
        {
            // A balloon near the right wall (centre 4.7, radius 0.4) has its far surface at x=5.1 —
            // past the wall at 5. A shot penetrating from x=5 leftward deflects with a contact there;
            // un-clamped the returned position would sit outside, and the next Step would read it as a
            // spurious wall bounce (a shield loss that could kill a 0-shield shot at the deflect).
            var model = NewModel(direction: Vector2.left, speed: 1f, shields: 3);

            var contact = _resolver.Deflect(model, new Vector3(5f, 0f, 0f), new Vector3(4.7f, 0f, 0f), 0.4f);

            Assert.LessOrEqual(contact.x, 5f + 1e-4f, "deflect result stays inside the right wall");
            Assert.GreaterOrEqual(contact.x, -5f - 1e-4f);
        }

        [Test]
        public void Deflect_DegenerateInput_KeepsThePenetratedPosition()
        {
            var model = NewModel(direction: Vector2.zero, speed: 1f, shields: 3);
            var position = new Vector3(0.1f, 0.2f, 0f);

            var contact = _resolver.Deflect(model, position, Vector3.zero, 0.4f);

            Assert.AreEqual(position, contact, "no ray to backtrack — stay where the trigger fired");
        }

        [Test]
        public void TryComputeContactNormal_HeadOn_NormalOpposesTravel()
        {
            // Travelling down onto a circle at the origin, trigger fired 0.1 deep inside.
            var found = ProjectileMotionResolver.TryComputeContactNormal(
                new Vector2(0f, 0.3f), Vector2.down, Vector2.zero, 0.4f, out var normal);

            Assert.IsTrue(found);
            Assert.AreEqual(0f, normal.x, 0.0001f);
            Assert.AreEqual(1f, normal.y, 0.0001f);
        }

        [Test]
        public void TryComputeContactNormal_PenetratedOblique_MatchesAnalyticEntry()
        {
            // Travelling +X along y = 0.2 into a radius-0.4 circle at the origin: analytic entry at
            // x = -sqrt(0.4^2 - 0.2^2). The trigger position sits well past it, inside the circle.
            var found = ProjectileMotionResolver.TryComputeContactNormal(
                new Vector2(0.1f, 0.2f), Vector2.right, Vector2.zero, 0.4f, out var normal);

            var entryX = -Mathf.Sqrt(0.4f * 0.4f - 0.2f * 0.2f);
            Assert.IsTrue(found);
            Assert.AreEqual(entryX / 0.4f, normal.x, 0.0001f);
            Assert.AreEqual(0.2f / 0.4f, normal.y, 0.0001f);
            Assert.AreEqual(1f, normal.magnitude, 0.0001f);
        }

        [Test]
        public void TryComputeContactNormal_GrazingChord_NormalPerpendicularToTravel()
        {
            // Chord at the circle's edge: y equals the radius → entry tangency, normal straight up.
            var found = ProjectileMotionResolver.TryComputeContactNormal(
                new Vector2(0f, 0.4f), Vector2.right, Vector2.zero, 0.4f, out var normal);

            Assert.IsTrue(found);
            Assert.AreEqual(0f, normal.x, 0.001f);
            Assert.AreEqual(1f, normal.y, 0.001f);
        }

        [Test]
        public void TryComputeContactNormal_LineMissesCircle_ReturnsFalse()
        {
            var found = ProjectileMotionResolver.TryComputeContactNormal(
                new Vector2(0f, 1f), Vector2.right, Vector2.zero, 0.4f, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryComputeContactNormal_DegenerateInput_ReturnsFalse()
        {
            Assert.IsFalse(ProjectileMotionResolver.TryComputeContactNormal(
                Vector2.zero, Vector2.zero, Vector2.zero, 0.4f, out _));
            Assert.IsFalse(ProjectileMotionResolver.TryComputeContactNormal(
                Vector2.zero, Vector2.right, Vector2.zero, 0f, out _));
        }

        [Test]
        public void Step_WithSpeedBuff_MovesTwiceAsFar()
        {
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.AddBuff(new ProjectileBuff(
                ProjectileBuffId.Speed, 2f, BuffModifierOp.Multiplicative,
                new WallBounceEndCondition(NeverFiringWallBounces())));

            var step = _resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            Assert.AreEqual(new Vector3(0f, 2f, 0f), step.Position, "speed 1 x2 buff over dt 1 = 2 units");
        }

        [Test]
        public void Step_ConsecutiveBounces_CountsWithoutEnteringCruise()
        {
            // Entry is the view's call (it confirms with a physics lookahead) — the resolver only
            // maintains the counter the view checks against the threshold.
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 10);

            for (var i = 0; i < 5; i++)
            {
                model.Direction = Vector2.up;
                _resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);
            }

            Assert.AreEqual(5, model.Flight.ConsecutiveWallBounces);
            Assert.IsFalse(model.IsCruising.Value, "the plain resolver never flips cruise on by itself");
        }

        [Test]
        public void Step_CruiseEntry_StartsAtBaseSpeed()
        {
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 4);
            model.Flight.CruiseStartShields = 4;
            model.IsCruising.Value = true;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            Assert.AreEqual(1f, step.Position.y, 1e-4f, "no shields spent yet — still at base speed");
        }

        [Test]
        public void Step_CruiseRamp_SpeedsUpAsShieldsSpend()
        {
            // Entry bank 4 at 0.5/shield -> max x3; half the bounces spent, linear ramp -> x2.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.Flight.CruiseStartShields = 4;
            model.IsCruising.Value = true;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(2f, step.Position.y, 1e-4f, "half the shields spent, linear ramp → halfway to x3");
        }

        [Test]
        public void Step_CruiseRamp_PeaksOnLastShieldSpent()
        {
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);
            model.Flight.CruiseStartShields = 4;
            model.IsCruising.Value = true;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(3f, step.Position.y, 1e-4f, "all entry shields spent — 1 + 0.5 x 4 = x3");
        }

        [Test]
        public void Step_CruiseTopSpeed_ScalesWithEntryShields()
        {
            // The whole point of the shield-scaled cap: a bigger bank tops out faster. Entry 8 at
            // 0.5/shield fully spent -> x5, where entry 4 gave x3.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);
            model.Flight.CruiseStartShields = 8;
            model.IsCruising.Value = true;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(5f, step.Position.y, 1e-4f);
        }

        [Test]
        public void Step_TapEnvelope_FreezesThenPicksUpToTarget()
        {
            // Tap animation: 1s linear 0->1 curve. Right after a tap (elapsed 0) the shot is FROZEN
            // (curve(0) = 0); halfway through the window it flies at half the x2 target; once the
            // window completes it holds the full target.
            var resolver = CruiseResolver(perShield: 0.5f, tapEaseDuration: 1f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.Flight.CruiseStartShields = 4;
            model.IsCruising.Value = true;
            model.Flight.CruiseTapElapsed = 0f;

            var frozen = resolver.Step(model, Vector3.zero, 0.5f);
            Assert.AreEqual(0f, frozen.Position.y, 1e-4f, "curve(0) = 0 — the freeze beat");

            var pickingUp = resolver.Step(model, frozen.Position, 0.5f);
            Assert.AreEqual(0.5f, pickingUp.Position.y, 1e-4f, "curve(0.5) = 0.5 of the x2 target over 0.5s");

            var atTarget = resolver.Step(model, pickingUp.Position, 0.5f);
            Assert.AreEqual(1.5f, atTarget.Position.y, 1e-4f, "window complete — full x2 target");
        }

        [Test]
        public void Step_CruiseBounce_RestartsTheTapEnvelope()
        {
            var resolver = CruiseResolver(perShield: 0.5f, tapEaseDuration: 1f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.Flight.CruiseStartShields = 4;
            model.IsCruising.Value = true;
            model.Flight.CruiseTapElapsed = 99f;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(0f, model.Flight.CruiseTapElapsed, "a cruise bounce replays the animation from t=0");
        }

        [Test]
        public void Step_LethalBounce_DoesNotCountACruiseBounce()
        {
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);

            var step = resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Destroyed, step.Outcome);
            Assert.AreEqual(0, model.Flight.ConsecutiveWallBounces, "a lethal bounce ends the shot, not the count");
        }

        [Test]
        public void Step_CruiseTaps_ArmPiercingAtThreshold()
        {
            var resolver = CruiseResolver(perShield: 0f, piercingTapThreshold: 3);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 4);
            model.Flight.CruiseStartShields = 5;
            model.IsCruising.Value = true;

            // This bounce spends shield 4 -> 3: taps = 5 - 3 = 2, still below the threshold.
            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);
            Assert.IsFalse(model.IsPiercing.Value, "two taps — not armed yet");

            // Next bounce: taps = 3 — the shot arms for the rest of its life.
            model.Direction = Vector2.up;
            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);
            Assert.IsTrue(model.IsPiercing.Value, "third tap arms piercing");
        }

        [Test]
        public void Step_PiercingThresholdZero_NeverArms()
        {
            var resolver = CruiseResolver(perShield: 0f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 10);
            model.Flight.CruiseStartShields = 10;
            model.IsCruising.Value = true;

            for (var i = 0; i < 6; i++)
            {
                model.Direction = Vector2.up;
                resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);
            }

            Assert.IsFalse(model.IsPiercing.Value, "0 disables the piercing grant");
        }

        [Test]
        public void Step_WallAfterToughPlow_EndsCruiseAndResetsScale()
        {
            // scale < 1 means the shot has already plowed a tough — the next wall is the recovery.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.Flight.CruiseStartShields = 3;
            model.IsCruising.Value = true;
            model.Flight.CruisePierceSpeedScale = 0.5f;
            model.IsPiercing.Value = true;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.IsFalse(model.IsCruising.Value, "the recovery wall ends the speed cruise");
            Assert.AreEqual(1f, model.Flight.CruisePierceSpeedScale, 1e-4f, "pierce speed scale resets to base");
            Assert.IsFalse(model.IsPiercing.Value,
                "piercing is consumed at the recovery wall — a normal shot again");
        }

        [Test]
        public void Step_PiercingWallBounce_BeforeAnyToughKeepsCruising()
        {
            // Freshly armed (scale == 1, hasn't hit a tough): a corridor wall must NOT slow it —
            // the cruise rides on, no premature reset.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.Flight.CruiseStartShields = 5;
            model.IsCruising.Value = true;
            model.Flight.CruisePierceSpeedScale = 1f;
            model.IsPiercing.Value = true;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.IsTrue(model.IsCruising.Value, "an armed shot keeps cruising off empty corridor walls");
            Assert.IsTrue(model.IsPiercing.Value,
                "nothing slowed it, so piercing isn't spent");
        }

        [Test]
        public void Step_PierceSpeedScale_FloorsAtBaseSpeed()
        {
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);
            model.Flight.CruiseStartShields = 4;   // ramp target x3 at full spend
            model.IsCruising.Value = true;
            model.Flight.CruisePierceSpeedScale = 0.01f;   // decayed hard by many tough plows

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(1f, step.Position.y, 1e-4f, "never below normal speed however much it decayed");
        }

        [Test]
        public void Step_LastShieldApproach_TraversesSegmentNormalizedToTime()
        {
            // Segment from y=0 to the top wall (y=5), length 5; a linear time->position curve over a
            // 4s duration. At elapsed 2s (halfway in TIME) the shot sits at half the segment (y=2.5),
            // independent of speed — the moment is timed, not distance-driven.
            var resolver = LastShieldResolver(AnimationCurve.Linear(0f, 0f, 1f, 1f), durationSeconds: 4f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);
            model.IsLastShieldApproach.Value = true;
            model.Flight.SegmentStartPosition = Vector3.zero;
            model.Flight.SegmentElapsed = 2f;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            Assert.AreEqual(2.5f, step.Position.y, 1e-4f, "halfway in time = halfway along the segment");
        }

        [Test]
        public void Step_LastShieldApproach_DiesOnceTheTimerCompletes()
        {
            // Elapsed past the duration overshoots the wall so the doomed shot crosses and dies,
            // rather than resting exactly on it forever.
            var resolver = LastShieldResolver(AnimationCurve.Linear(0f, 0f, 1f, 1f), durationSeconds: 3f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);
            model.IsLastShieldApproach.Value = true;
            model.Flight.SegmentStartPosition = Vector3.zero;
            model.Flight.SegmentElapsed = 3f;

            var step = resolver.Step(model, new Vector3(0f, 5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Destroyed, step.Outcome, "the completed timer sends it into the wall");
        }

        private static ProjectileMotionResolver LastShieldResolver(AnimationCurve approachCurve, float durationSeconds)
        {
            var config = Substitute.For<IGameConfiguration>();
            config.LimitsClockwise.Returns(Walls);
            config.CruiseTapCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            config.LastShieldApproachCurve.Returns(approachCurve);
            config.LastShieldApproachDuration.Returns(durationSeconds);
            return new ProjectileMotionResolver(config);
        }

        private static ProjectileMotionResolver CruiseResolver(
            float perShield, float tapEaseDuration = 0f, int piercingTapThreshold = 0)
        {
            var config = Substitute.For<IGameConfiguration>();
            config.LimitsClockwise.Returns(Walls);
            config.CruiseSpeedPerShield.Returns(perShield);
            config.CruiseTapEaseDuration.Returns(tapEaseDuration);
            config.CruisePiercingTapThreshold.Returns(piercingTapThreshold);
            config.CruiseTapCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return new ProjectileMotionResolver(config);
        }

        private static ISubscriber<ShieldLostMessage> NeverFiringWallBounces()
        {
            var wallBounces = Substitute.For<ISubscriber<ShieldLostMessage>>();
            wallBounces
                .Subscribe(
                    Arg.Any<IMessageHandler<ShieldLostMessage>>(),
                    Arg.Any<MessageHandlerFilter<ShieldLostMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            return wallBounces;
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
    }
}

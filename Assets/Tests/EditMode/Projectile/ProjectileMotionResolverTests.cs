using System;
using BalloonParty.Balloon.Model;
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
            var config = Substitute.For<IProjectileFlightConfig>();
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
            model.IsCruising.Value = true;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            Assert.AreEqual(1f, step.Position.y, 1e-4f, "0 taps means cruise starts at base speed");
        }

        [Test]
        public void Step_CruiseRamp_SpeedsUpAsShieldsSpend()
        {
            // Two taps at 0.5/tap -> +1.0 speed bonus -> x2 target.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.Flight.TotalCruiseTaps = 2;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(2f, step.Position.y, 1e-4f, "2 taps at 0.5/tap produce an x2 cruise speed");
        }

        [Test]
        public void Step_CruiseRamp_PeaksOnLastShieldSpent()
        {
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);
            model.Flight.TotalCruiseTaps = 4;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(3f, step.Position.y, 1e-4f, "4 taps at 0.5/tap produce 1 + 2.0 = x3");
        }

        [Test]
        public void Step_CruiseTopSpeed_ScalesWithEntryShields()
        {
            // More taps should scale the multiplier directly: 8 taps at 0.5/tap -> x5.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);
            model.Flight.TotalCruiseTaps = 8;

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
            model.Flight.TotalCruiseTaps = 2;
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
            model.IsCruising.Value = true;
            model.Flight.CruiseTapElapsed = 99f;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(0f, model.Flight.CruiseTapElapsed, "a cruise bounce replays the animation from t=0");
        }

        [Test]
        public void Step_SweepTap_UsesSameEasePathAsCruiseTap()
        {
            var resolver = CruiseResolver(perShield: 0.5f, tapEaseDuration: 1f);
            var cruiseModel = NewModel(direction: Vector2.up, speed: 1f, shields: 1);
            cruiseModel.Flight.TotalCruiseTaps = 1;
            cruiseModel.Flight.CruiseTapElapsed = 0f;

            var sweepModel = NewModel(direction: Vector2.up, speed: 1f, shields: 1);
            sweepModel.Flight.TotalCruiseTaps = 1;
            sweepModel.Flight.CruiseTapElapsed = 0f;

            var cruiseFrozen = resolver.Step(cruiseModel, Vector3.zero, 0.5f);
            var sweepFrozen = resolver.Step(sweepModel, Vector3.zero, 0.5f);
            Assert.AreEqual(cruiseFrozen.Position.y, sweepFrozen.Position.y, 1e-4f,
                "both tap types should hit the same freeze beat right after the tap");

            var cruisePickup = resolver.Step(cruiseModel, cruiseFrozen.Position, 0.5f);
            var sweepPickup = resolver.Step(sweepModel, sweepFrozen.Position, 0.5f);
            Assert.AreEqual(cruisePickup.Position.y, sweepPickup.Position.y, 1e-4f,
                "mid-window pickup should follow the same lerp path for sweep and cruise taps");

            var cruiseTarget = resolver.Step(cruiseModel, cruisePickup.Position, 0.5f);
            var sweepTarget = resolver.Step(sweepModel, sweepPickup.Position, 0.5f);
            Assert.AreEqual(cruiseTarget.Position.y, sweepTarget.Position.y, 1e-4f,
                "once the ease completes, both tap types should hold the same target speed");
        }

        [Test]
        public void Step_CruiseBounce_IncrementsTotalCruiseTaps()
        {
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.IsCruising.Value = true;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(1, model.Flight.TotalCruiseTaps);
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
            // The model enters the test mid-cruise with one tap already banked.
            model.Flight.TotalCruiseTaps = 1;
            model.IsCruising.Value = true;

            // This bounce brings TotalCruiseTaps to 2 — still below the threshold of 3.
            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);
            Assert.IsFalse(model.IsPiercing.Value, "two taps — not armed yet");

            // Next bounce: TotalCruiseTaps = 3 — the shot arms for the rest of its life.
            model.Direction = Vector2.up;
            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);
            Assert.IsTrue(model.IsPiercing.Value, "third tap arms piercing");
        }

        [Test]
        public void Step_PiercingThresholdZero_NeverArms()
        {
            var resolver = CruiseResolver(perShield: 0f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 10);
            model.IsCruising.Value = true;

            for (var i = 0; i < 6; i++)
            {
                model.Direction = Vector2.up;
                resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);
            }

            Assert.IsFalse(model.IsPiercing.Value, "0 disables the piercing grant");
        }

        [Test]
        public void Step_PiercingWallBounce_BeforeAnyToughKeepsCruising()
        {
            // A cruising, armed shot rides a corridor wall without losing cruise or its pierce — a wall
            // with no pending tough hits never ends the pierce.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsCruising.Value = true;
            model.IsPiercing.Value = true;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.IsTrue(model.IsCruising.Value, "an armed shot keeps cruising off empty corridor walls");
            Assert.IsTrue(model.IsPiercing.Value,
                "a wall with no pending toughs never ends the pierce");
        }

        [Test]
        public void Step_PiercingNotCruising_BeforeAnyTough_KeepsPiercing()
        {
            // A non-cruising Snipe lance: a wall costs a shield but never spends the pierce — only the
            // wall-discharge (when pending toughs exist) ends it.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsPiercing.Value = true;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.IsTrue(model.IsPiercing.Value, "a wall with no pending toughs never ends the pierce");
            Assert.AreEqual(2, model.ShieldsRemaining.Value, "the wall still costs a shield");
        }

        [Test]
        public void Step_WallBounceDischarge_EndsPierceWhenPendingHitsExist()
        {
            // A piercing shot that plowed a tough and then hits a wall: the pierce ends at the wall,
            // and the view resolves the pending toughs (tested in the view/hit resolver tests).
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsPiercing.Value = true;
            model.IsCruising.Value = true;
            model.Flight.PendingPierceHits.Add(
                new PendingPierceHit(Substitute.For<IBalloonModel>(), Vector3.zero));

            var step = resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome);
            Assert.IsFalse(model.IsPiercing.Value, "pierce ends at the wall when toughs were plowed");
            Assert.IsFalse(model.IsCruising.Value, "cruise resets with the pierce");
        }

        [Test]
        public void Step_WallBounceDischarge_ResetsCruiseState()
        {
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsPiercing.Value = true;
            model.IsCruising.Value = true;
            model.Flight.TotalCruiseTaps = 5;
            model.Flight.ConsecutiveWallBounces = 10;
            model.Flight.PendingPierceHits.Add(
                new PendingPierceHit(Substitute.For<IBalloonModel>(), Vector3.zero));

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(0, model.Flight.TotalCruiseTaps,
                "the next cruise must bank fresh taps instead of re-arming off the old pierce");
            Assert.AreEqual(0, model.Flight.ConsecutiveWallBounces,
                "wall bounce counter resets so cruise re-entry requires fresh empty bounces");
        }

        [Test]
        public void Step_ArmedShot_EarnsNoFurtherCruiseTapsAndKeepsItsEnvelope()
        {
            // Cruise stops paying out once the shot is armed: it already sits at the top speed its taps
            // earned, and a tap it can't use would only ramp it further and re-trigger the
            // freeze-then-pickup envelope (which, with no speed change left to sell, reads as a hitch).
            var resolver = CruiseResolver(perShield: 0.5f, piercingTapThreshold: 3);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsCruising.Value = true;
            model.IsPiercing.Value = true;
            model.Flight.TotalCruiseTaps = 5;
            model.Flight.CruiseTapElapsed = 0.2f;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(5, model.Flight.TotalCruiseTaps, "an armed shot banks no further taps");
            Assert.GreaterOrEqual(
                model.Flight.CruiseTapElapsed, 0.2f,
                "the envelope is never restarted at the bounce — a reset would zero this");
        }

        [Test]
        public void Step_PiercingArms_RampsIntoTheFrozenTopSpeedInsteadOfSnappingToIt()
        {
            // Taps stop at the arming bounce, so nothing is left for the per-tap beat to sell and the shot
            // would otherwise hold its frozen top speed from that bounce on — reading as a jump to full
            // speed. One ramp carries it there from the speed it armed at (José's playtest, 2026-07-27).
            // perShield 1 + 1 tap ⇒ top speed = base x 2; a linear ramp curve (the resolver's fallback for
            // an unauthored one) over 1s makes the expected speeds exact.
            var resolver = CruiseResolver(perShield: 1f, piercingTapThreshold: 1, armRampDuration: 1f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsCruising.Value = true;

            var armStep = resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.IsTrue(model.IsPiercing.Value, "one tap at threshold 1 arms the shot");
            Assert.AreEqual(1f, armStep.Speed, 1e-4f, "it arms while still travelling at base speed");
            Assert.AreEqual(
                1f, model.Flight.PierceArmFromSpeed, 1e-4f,
                "the ramp is anchored at that actual speed, so the transition is continuous");

            // The ramp walks from 1 to 2 over 1s. The step right after arming is the one that would betray
            // a snap: it must still be at the arming speed, not already at the top.
            model.Direction = Vector2.up;
            Assert.AreEqual(1f, resolver.Step(model, Vector3.zero, 0.25f).Speed, 1e-3f, "no jump at t=0");
            Assert.AreEqual(1.25f, resolver.Step(model, Vector3.zero, 0.25f).Speed, 1e-3f, "quarter way");
            Assert.AreEqual(1.5f, resolver.Step(model, Vector3.zero, 0.25f).Speed, 1e-3f, "half way");
            Assert.AreEqual(1.75f, resolver.Step(model, Vector3.zero, 0.25f).Speed, 1e-3f, "three quarters");
            Assert.AreEqual(2f, resolver.Step(model, Vector3.zero, 0.25f).Speed, 1e-3f, "top speed reached");
            Assert.AreEqual(2f, resolver.Step(model, Vector3.zero, 0.25f).Speed, 1e-3f, "and held there");
        }

        [Test]
        public void Step_PiercingArmsWithNoRampConfigured_HoldsTopSpeedImmediately()
        {
            // 0 duration disables the ramp — the pre-ramp behaviour, kept reachable for tuning.
            var resolver = CruiseResolver(perShield: 1f, piercingTapThreshold: 1);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsCruising.Value = true;

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);
            model.Direction = Vector2.up;

            Assert.AreEqual(2f, resolver.Step(model, Vector3.zero, 0.25f).Speed, 1e-3f);
        }

        [Test]
        public void Step_WallBounceDischarge_WithABankedSnipe_KeepsPiercingButStillEndsTheCruise()
        {
            // A Snipe taken mid-pierce is banked and re-arms the lance at this discharge, so IsPiercing
            // never dips (LevelController releases its level-up hold on that edge — a dip would fire the
            // ceremony mid-flight). The cruise still ends: the re-armed lance starts from base speed, so
            // the old ramp can never compound with the grant the charge is about to re-apply. Spending the
            // charge itself belongs to SnipeItemHandler, which owns the grant.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsPiercing.Value = true;
            model.IsCruising.Value = true;
            model.Flight.TotalCruiseTaps = 5;
            model.Flight.BankedPierceCharges = 1;
            model.Flight.PendingPierceHits.Add(
                new PendingPierceHit(Substitute.For<IBalloonModel>(), Vector3.zero));

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.IsTrue(model.IsPiercing.Value, "the banked charge re-arms the lance in place");
            Assert.IsFalse(model.IsCruising.Value, "the cruise that fed the spent pierce still ends");
            Assert.AreEqual(0, model.Flight.TotalCruiseTaps, "back to base speed for the fresh lance");
            Assert.AreEqual(1, model.Flight.BankedPierceCharges, "the resolver peeks; the handler spends");
        }

        [Test]
        public void Step_WallBounceDischarge_WithABankedRainbowSnipe_KeepsPiercing()
        {
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 3);
            model.IsPiercing.Value = true;
            model.Flight.BankedRainbowPierceCharges = 1;
            model.Flight.PendingPierceHits.Add(
                new PendingPierceHit(Substitute.For<IBalloonModel>(), Vector3.zero));

            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.IsTrue(model.IsPiercing.Value, "a rainbow charge re-arms exactly like a plain one");
        }

        [Test]
        public void Step_PiercingNoPendingHits_NeverDischarges()
        {
            // A piercing shot that never plowed a tough has no pending hits, so the discharge never
            // fires at walls — the pierce persists indefinitely.
            var resolver = CruiseResolver(perShield: 0.5f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 5);
            model.IsPiercing.Value = true;
            model.IsCruising.Value = true;

            // Bounce off a wall with no pending hits:
            resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.IsTrue(model.IsPiercing.Value, "no toughs plowed — pierce persists");
            Assert.IsTrue(model.IsCruising.Value, "cruising continues through empty walls");
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

        [Test]
        public void Step_TotalCruiseTaps_AppliesOutsideCruise()
        {
            var resolver = CruiseResolver(perShield: 0.5f, tapEaseDuration: 0f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.Flight.TotalCruiseTaps = 1;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(1.5f, step.Position.y, 1e-4f);
        }

        [Test]
        public void Step_TotalCruiseTaps_CombineIntoUnifiedSpeed()
        {
            // The refactor collapsed cruise and sweep into one tap counter. Three taps at 0.5/tap
            // give +1.5 bonus -> x2.5 target.
            var resolver = CruiseResolver(perShield: 0.5f, tapEaseDuration: 0f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.Flight.TotalCruiseTaps = 3;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(2.5f, step.Position.y, 1e-4f,
                "the unified tap counter should drive the full combined speed");
        }

        [Test]
        public void Step_MaxCruiseSpeedCap_ClampsUnifiedTapSpeed()
        {
            // Without a cap: 3 taps at 0.5/tap -> +1.5 -> x2.5. With a x2.0 cap, clamp to 2.0.
            var resolver = CruiseResolver(perShield: 0.5f, tapEaseDuration: 0f, maxSpeedMultiplier: 2.0f);
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.Flight.TotalCruiseTaps = 3;

            var step = resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(2.0f, step.Position.y, 1e-4f,
                "max-speed cap applies to the unified tap total");
        }

        // --- Graze-deflect teleport bug (investigation dated 2026-07-25; José's report) ---
        // A grazing OnTriggerEnter2D reports a position OUTSIDE the combined circle (the projectile's
        // CapsuleCollider2D extends past the point the resolver is handed) up to ~46.8% of live
        // deflects, so `TryComputeContactNormal`'s `backtrack < 0f` check bails and `Deflect` falls
        // back to a "degenerate" branch that (a) never re-anchors the flight segment and (b) reflects
        // off the penetrated radial normal instead of the true tangent-entry normal.

        [Test]
        public void Step_AfterFallbackDeflectOnLastShieldApproach_DoesNotTeleportPastOneStepDistance()
        {
            // The bug's actual symptom (H1): the fallback branch in Deflect leaves
            // model.Flight.SegmentStartPosition/SegmentElapsed pointing at the PREVIOUS wall bounce.
            // The very next Step, on a doomed 0-shield last-shield-approach segment, recomputes
            // position ABSOLUTELY from that stale anchor plus the just-reflected heading — relocating
            // the shot several units in one 0.02s fixed step instead of the honest speed*dt travel.
            var resolver = GrazeDeflectResolver();
            var model = NewModel(direction: new Vector2(0.29996f, 0.95395f), speed: 8f, shields: 0);
            model.IsLastShieldApproach.Value = true;
            model.Flight.SegmentStartPosition = new Vector3(0.5f, -4.5f, 0f); // previous bottom-wall bounce
            model.Flight.SegmentElapsed = 0.32f;

            var projectilePosition = new Vector3(1.2f, -2.274f, 0f);
            var balloonPosition = new Vector3(1.5992f, -2.0938f, 0f);
            const float contactRadius = 0.4375f; // 0.3125 (balloon) + 0.125 (reported projectile radius)

            // Confirms the repro sits on the epsilon-scale branch flip that forces the fallback:
            // |p-c| = 0.43799 is a hair OUTSIDE R = 0.4375, so the analytic backtrack goes negative.
            Assert.IsFalse(
                ProjectileMotionResolver.TryComputeContactNormal(
                    projectilePosition, model.Direction, balloonPosition, contactRadius, out _),
                "repro requires the outside-the-circle graze that forces Deflect's fallback branch");

            var deflectedPosition = resolver.Deflect(model, projectilePosition, balloonPosition, contactRadius);
            var step = resolver.Step(model, deflectedPosition, 0.02f);

            var moved = Vector3.Distance(deflectedPosition, step.Position);
            Assert.LessOrEqual(moved, 8f * 0.02f * 1.5f,
                "a single fixed step must never relocate the shot farther than ~1.5x its per-step travel " +
                "distance — today this jumps ~2.5 units (~15x a step) because the fallback deflect never " +
                "starts a new flight segment");
        }

        [Test]
        public void Deflect_ContactReportedOutsideCircle_HeadingDivergesFromAnalyticTangentReflection()
        {
            // Same fallback branch, isolated to its OTHER live consequence (H3): reflecting off the
            // penetrated radial normal instead of the true forward-entry tangent normal the solver
            // (ShotSimulator.DeflectOffBalloon) computes for the identical contact. The investigation
            // measured this fallback path diverging from the solver's outgoing angle by a median 6.4°,
            // p90 15.7°, max ~21.8° across live contacts. This is a concrete instance (~20.6°),
            // constructed with clean numbers so both the analytic and buggy outcomes are exact:
            // p=(-0.6, 0.3) outside a radius-0.5 circle at the origin, travelling +X.
            // Analytic forward entry: t = -along - sqrt(disc) = 0.2 -> contact (-0.4, 0.3) ->
            // normal (-0.8, 0.6) -> reflected direction (-0.28, 0.96).
            // Today's fallback instead uses the penetrated radial normal (-0.8944, 0.4472) -> reflected
            // direction (-0.6, 0.8) — a ~20.6° divergence from the correct billiard outcome.
            var model = NewModel(direction: Vector2.right, speed: 1f, shields: 3);

            var result = _resolver.Deflect(model, new Vector3(-0.6f, 0.3f, 0f), Vector3.zero, 0.5f);

            Assert.AreEqual(-0.28f, model.Direction.x, 1e-3f,
                "outgoing heading should match the analytic forward-entry tangent reflection");
            Assert.AreEqual(0.96f, model.Direction.y, 1e-3f);

            // A forward (still-short-of-the-surface) contact has no already-travelled surplus to carry
            // past the contact point — the shot must land ON the entry point, not entryDistance beyond
            // it along the reflected heading (that would manufacture free travel every grazing deflect).
            Assert.AreEqual(-0.4f, result.x, 1e-3f,
                "forward-entry deflect must snap to the contact point with zero carried remainder");
            Assert.AreEqual(0.3f, result.y, 1e-3f);
        }

        [Test]
        public void Deflect_ContactExactlyOnCircle_ProducesExactTangentNormalAndZeroRelocation()
        {
            // Regression lock for any H1/H3 fix: a contact exactly ON the combined circle — the
            // construction ShotSimulator.DeflectOffBalloon relies on (it advances the flight to the
            // exact circle entry via TryFindStaticBalloonEntry before deflecting, ShotSimulator.cs) —
            // must keep taking the analytic path with the exact radial tangent normal and ZERO
            // relocation. TryComputeContactNormal is shared verbatim between the live resolver and the
            // solver; a fix that regresses this boundary would re-baseline the solver's bias seam too.
            var model = NewModel(direction: Vector2.right, speed: 1f, shields: 3);
            var projectilePosition = new Vector3(0f, 0.4f, 0f);

            var contact = _resolver.Deflect(model, projectilePosition, Vector3.zero, 0.4f);

            Assert.AreEqual(projectilePosition, contact, "an exact-tangent contact carries zero remainder");
            Assert.AreEqual(1f, model.Direction.x, 1e-4f, "tangent normal is straight up; horizontal travel unchanged");
            Assert.AreEqual(0f, model.Direction.y, 1e-4f);
            Assert.AreEqual(projectilePosition, model.Flight.SegmentStartPosition,
                "the new flight segment anchors at the contact point");
            Assert.AreEqual(0f, model.Flight.SegmentElapsed, "a fresh segment starts its clock at zero");
        }

        private static ProjectileMotionResolver GrazeDeflectResolver()
        {
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(new Vector4(4f, 2.25f, -4.5f, -2.25f));
            config.CruiseTapCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            config.LastShieldApproachCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            config.LastShieldApproachDuration.Returns(0.8f);
            return new ProjectileMotionResolver(config);
        }

        private static ProjectileMotionResolver LastShieldResolver(AnimationCurve approachCurve, float durationSeconds)
        {
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(Walls);
            config.CruiseTapCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            config.LastShieldApproachCurve.Returns(approachCurve);
            config.LastShieldApproachDuration.Returns(durationSeconds);
            return new ProjectileMotionResolver(config);
        }

        private static ProjectileMotionResolver CruiseResolver(
            float perShield, float tapEaseDuration = 0f, int piercingTapThreshold = 0,
            float maxSpeedMultiplier = 0f, float armRampDuration = 0f)
        {
            var config = Substitute.For<IProjectileFlightConfig>();
            config.PierceArmRampDuration.Returns(armRampDuration);
            config.LimitsClockwise.Returns(Walls);
            config.CruiseSpeedPerShield.Returns(perShield);
            config.CruiseTapEaseDuration.Returns(tapEaseDuration);
            config.CruisePiercingTapThreshold.Returns(piercingTapThreshold);
            config.MaxCruiseSpeedMultiplier.Returns(maxSpeedMultiplier);
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

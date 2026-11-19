using BalloonParty.Configuration.GridActors;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     The motion of a mushroom's cap after a hit (<see cref="MushroomCapSpring" />, @ref plan_meadow_mushrooms):
    ///     it leans along the ball's way in, wobbles back along that line, comes to rest and says so.
    /// </summary>
    [TestFixture]
    public class MushroomCapSpringTests
    {
        private const float Frame = 1f / 60f;
        private const float BallSpeed = 10f;

        private static readonly MushroomLeanTuning Shipped = new MushroomSettings().Lean;

        [Test]
        public void AFreshSpring_IsAtRest_AndSteppingItSaysSo()
        {
            var spring = new MushroomCapSpring();

            Assert.IsTrue(spring.IsAtRest);
            Assert.IsFalse(spring.Step(Frame, Shipped));
            Assert.AreEqual(Vector2.zero, spring.Lean);
            Assert.AreEqual(0f, spring.Pulse);
        }

        [TestCase(1f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        [TestCase(0.6f, 0.8f)]
        [TestCase(-0.8f, -0.6f)]
        public void TheCapLeansTheWayTheBallWasGoing(float x, float y)
        {
            var incoming = new Vector2(x, y);
            var spring = new MushroomCapSpring();
            spring.Hit(incoming, BallSpeed, Shipped);

            var furthest = Vector2.zero;
            for (var i = 0; i < 12; i++)
            {
                spring.Step(Frame, Shipped);
                if (spring.Lean.sqrMagnitude > furthest.sqrMagnitude)
                {
                    furthest = spring.Lean;
                }
            }

            Assert.Greater(furthest.magnitude, 0.01f, "the cap moved");
            Assert.AreEqual(1f, Vector2.Dot(furthest.normalized, incoming.normalized), 1e-4f,
                "away from the hit, along the ball's own line");
        }

        [Test]
        public void TheWobbleStaysOnTheLineOfTheHit_AndCrossesBackOverRest()
        {
            var incoming = new Vector2(0.6f, 0.8f);
            var spring = new MushroomCapSpring();
            spring.Hit(incoming, BallSpeed, Shipped);

            var wentBackPastRest = false;
            for (var i = 0; i < 90 && spring.Step(Frame, Shipped); i++)
            {
                var along = Vector2.Dot(spring.Lean, incoming);
                var across = Vector2.Dot(spring.Lean, new Vector2(-incoming.y, incoming.x));
                Assert.AreEqual(0f, across, 1e-5f, "no sideways drift off the line of the hit");
                wentBackPastRest |= along < -0.005f;
            }

            Assert.IsTrue(wentBackPastRest, "an elastic stalk swings back past its rest, it does not creep home");
        }

        [Test]
        public void TheFurthestLean_IsTheTunedReachForTheBallsSpeed()
        {
            var spring = new MushroomCapSpring();
            spring.Hit(Vector2.right, BallSpeed, Shipped);

            // Fine steps: the authored reach is the spring's true peak, which a coarse frame can step over.
            var furthest = 0f;
            for (var i = 0; i < 600; i++)
            {
                spring.Step(1f / 1200f, Shipped);
                furthest = Mathf.Max(furthest, spring.Lean.x);
            }

            Assert.AreEqual(BallSpeed * Shipped.LeanPerSpeed, furthest, 0.004f);
        }

        [Test]
        public void AFasterBallLeansTheCapFurther_UpToTheMost()
        {
            Assert.Less(FurthestLean(5f, 1), FurthestLean(10f, 1));
            Assert.LessOrEqual(FurthestLean(500f, 1), Shipped.MaxLean + 1e-4f, "a tapped-up ball");
            Assert.LessOrEqual(FurthestLean(500f, 6), Shipped.MaxLean + 1e-4f, "six of them at once");
        }

        [Test]
        public void TheCapComesToRest_AndIsThenExactlyAtRest()
        {
            var spring = new MushroomCapSpring();
            spring.Hit(new Vector2(-0.3f, 1f), BallSpeed, Shipped);
            spring.Pay(Shipped);

            var frames = 0;
            while (spring.Step(Frame, Shipped))
            {
                frames++;
                Assert.Less(frames, 600, "still wobbling after ten seconds");
            }

            Assert.Greater(frames, 20, "it wobbled for a while first");
            Assert.IsTrue(spring.IsAtRest);
            Assert.AreEqual(Vector2.zero, spring.Lean);
            Assert.AreEqual(0f, spring.Pulse);
            Assert.IsFalse(spring.Step(Frame, Shipped), "and stays there");
        }

        [Test]
        public void ItSettlesTheSame_WhateverTheFrameRate()
        {
            // The spring is stepped no coarser than its own fixed step, so a slow device's cap wobbles as long.
            var fast = SecondsToRest(1f / 120f);
            var slow = SecondsToRest(1f / 20f);

            Assert.AreEqual(fast, slow, 0.15f);
        }

        [Test]
        public void AHitch_DoesNotThrowTheCapOff()
        {
            var spring = new MushroomCapSpring();
            spring.Hit(Vector2.up, BallSpeed, Shipped);
            spring.Step(2f, Shipped);

            Assert.LessOrEqual(spring.Lean.magnitude, Shipped.MaxLean + 1e-4f);
            Assert.IsFalse(float.IsNaN(spring.Lean.x) || float.IsNaN(spring.Lean.y));
        }

        [Test]
        public void ABounceThatPays_SwellsTheCap_AboutAsFarAsTuned()
        {
            var spring = new MushroomCapSpring();
            spring.Pay(Shipped);

            var fullest = 0f;
            for (var i = 0; i < 600; i++)
            {
                spring.Step(1f / 1200f, Shipped);
                fullest = Mathf.Max(fullest, spring.Pulse);
            }

            Assert.AreEqual(Shipped.RewardPulse, fullest, 0.03f);
            Assert.AreEqual(Vector2.zero, spring.Lean, "a swell is not a lean");
        }

        [Test]
        public void AHitWithNoDirection_DoesNothing()
        {
            var spring = new MushroomCapSpring();
            spring.Hit(Vector2.zero, BallSpeed, Shipped);

            Assert.IsTrue(spring.IsAtRest);
        }

        [Test]
        public void Reset_StopsTheCapWhereItRests()
        {
            var spring = new MushroomCapSpring();
            spring.Hit(Vector2.right, BallSpeed, Shipped);
            spring.Pay(Shipped);
            spring.Step(Frame, Shipped);

            spring.Reset();

            Assert.IsTrue(spring.IsAtRest);
            Assert.IsFalse(spring.Step(Frame, Shipped));
        }

        [Test]
        public void TheShippedTuning_IsElastic_FitsTheQuadItIsDrawnOn_AndKnocksTheCapOffItsStem()
        {
            var settings = new MushroomSettings();

            Assert.Less(settings.Lean.Damping, 0.5f, "underdamped: the cap wobbles");
            Assert.LessOrEqual(settings.Lean.MaxLean, settings.Radius * 0.5f, "the cap stays on its quad");

            // Seen from above, the stem shows only once the cap has been knocked well off its foot: under about
            // four tenths of the radius an ordinary shot's knock would uncover none of it.
            Assert.Greater(BallSpeed * settings.Lean.LeanPerSpeed, settings.Radius * 0.4f,
                "an ordinary shot knocks the cap far enough to uncover the stem");
            Assert.Less(BallSpeed * settings.Lean.LeanPerSpeed, settings.Lean.MaxLean,
                "and a faster one still knocks it further");
        }

        private static float FurthestLean(float speed, int hits)
        {
            var spring = new MushroomCapSpring();
            for (var i = 0; i < hits; i++)
            {
                spring.Hit(Vector2.right, speed, Shipped);
            }

            var furthest = 0f;
            for (var i = 0; i < 240; i++)
            {
                spring.Step(1f / 240f, Shipped);
                furthest = Mathf.Max(furthest, spring.Lean.magnitude);
            }

            return furthest;
        }

        private static float SecondsToRest(float frame)
        {
            var spring = new MushroomCapSpring();
            spring.Hit(Vector2.right, BallSpeed, Shipped);
            var seconds = 0f;
            while (spring.Step(frame, Shipped) && seconds < 20f)
            {
                seconds += frame;
            }

            return seconds;
        }
    }
}

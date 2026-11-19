using BalloonParty.Game;
using BalloonParty.UI.ClothRibbon;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class BalloonWanderTests
    {
        private const float Frame = 1f / 60f;

        private static readonly Rect Bounds = Rect.MinMaxRect(-90f, -130f, 90f, 175f);
        private static readonly KeepOut AroundPlay = new(new Vector2(0f, 5f), new Vector2(0f, 75f), 77f);

        [Test]
        public void Wandering_NeverEntersTheKeepOut_NorLeavesTheBounds()
        {
            var wander = new BalloonWander(new BalloonWanderSettings(), Bounds, AroundPlay, new Vector2(60f, -125f), 7);

            for (var f = 0; f < 60 * 300; f++)
            {
                wander.Step(Frame);
                Assert.That(AroundPlay.Contains(wander.Position), Is.False, $"inside the keep-out at frame {f}");
                Assert.That(Bounds.Contains(wander.Position), Is.True, $"out of bounds at frame {f}");
            }
        }

        [Test]
        public void Wandering_GetsAroundTheScreen_WhenThereIsRoomBesideTheKeepOut()
        {
            // Spots are only headed for in plain sight, so the way above runs up a side: give it some width.
            var roomy = new KeepOut(AroundPlay.Bottom, AroundPlay.Top, 60f);
            var wander = new BalloonWander(new BalloonWanderSettings(), Bounds, roomy, new Vector2(60f, -125f), 7);
            var left = false;
            var right = false;
            var above = false;

            for (var f = 0; f < 60 * 600; f++)
            {
                wander.Step(Frame);
                left |= wander.Position.x < -40f;
                right |= wander.Position.x > 40f;
                above |= wander.Position.y > 60f;
            }

            Assert.That(left && right && above, Is.True, $"left {left}, right {right}, above {above}");
        }

        [Test]
        public void AStartInsideTheKeepOut_IsMovedSomewhereAllowed()
        {
            var wander = new BalloonWander(new BalloonWanderSettings(), Bounds, AroundPlay, Vector2.zero, 1);

            Assert.That(wander.Allowed(wander.Position), Is.True);
        }

        [Test]
        public void TheSameSeed_WandersTheSameWay()
        {
            var a = new BalloonWander(new BalloonWanderSettings(), Bounds, AroundPlay, new Vector2(60f, -125f), 11);
            var b = new BalloonWander(new BalloonWanderSettings(), Bounds, AroundPlay, new Vector2(60f, -125f), 11);

            for (var f = 0; f < 600; f++)
            {
                a.Step(Frame);
                b.Step(Frame);
            }

            Assert.That(a.Position, Is.EqualTo(b.Position));
        }

        [Test]
        public void PushOut_PutsAPointInsideOnTheCapsulesEdge()
        {
            var pushed = AroundPlay.PushOut(new Vector2(10f, 40f), out var outward);

            Assert.That(AroundPlay.Contains(pushed), Is.False, "out of it");
            Assert.That(AroundPlay.DistanceFrom(pushed), Is.EqualTo(AroundPlay.Radius).Within(0.01f), "on its edge");
            Assert.That(outward.x, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Blocks_TellsAPathThroughTheCapsule_FromOnePastIt()
        {
            Assert.That(AroundPlay.Blocks(new Vector2(-100f, 40f), new Vector2(100f, 40f)), Is.True, "straight across");
            Assert.That(AroundPlay.Blocks(new Vector2(-100f, -100f), new Vector2(100f, -100f)), Is.False, "below it");
            Assert.That(AroundPlay.Blocks(new Vector2(-60f, -60f), new Vector2(60f, -60f)), Is.True,
                "below the spine but within its reach of the bottom end");
            Assert.That(AroundPlay.Blocks(new Vector2(85f, -100f), new Vector2(85f, 170f)), Is.False, "up the side");
        }

        // The launch screen's own shape: a keep-out wide enough to leave only a sliver at either side, so the band
        // above it can't be reached from below. Heading for a spot up there slid the balloon to the keep-out's
        // underside, where it sat; and a slide that left it a rounding error inside the keep-out stopped it dead.
        [Test]
        public void Wandering_NeverStops_WhenTheKeepOutCutsTheAreaInTwo()
        {
            var settings = new BalloonWanderSettings();
            var bounds = Rect.MinMaxRect(-89.4f, -137.5f, 89.4f, 176.9f);
            var keepOut = new KeepOut(new Vector2(0f, -3f), new Vector2(0f, 63.5f), 88.1f);

            for (var seed = 0; seed < 10; seed++)
            {
                var wander = new BalloonWander(settings, bounds, keepOut, new Vector2(50f, -137f), seed);
                var stopped = 0;
                for (var f = 0; f < 60 * 120; f++)
                {
                    wander.Step(Frame);
                    var underway = Vector2.Distance(wander.Position, wander.Target) > settings.ArrivedWithin;
                    stopped = underway && wander.Velocity.magnitude < 0.05f ? stopped + 1 : 0;
                    Assert.That(stopped, Is.LessThan(30), $"seed {seed}: stopped short of its spot at {f * Frame:0.0} s");
                }
            }
        }
    }
}

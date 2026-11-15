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
        public void Wandering_GetsAroundTheScreen()
        {
            var wander = new BalloonWander(new BalloonWanderSettings(), Bounds, AroundPlay, new Vector2(60f, -125f), 7);
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

            Assert.That(AroundPlay.DistanceFrom(pushed), Is.EqualTo(AroundPlay.Radius).Within(1e-3f));
            Assert.That(outward.x, Is.EqualTo(1f).Within(1e-4f));
        }
    }
}

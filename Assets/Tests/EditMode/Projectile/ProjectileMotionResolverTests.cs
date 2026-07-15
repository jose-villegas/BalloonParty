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
        public void Step_CrossingWallWithShield_ClampsReflectsAndDecrements()
        {
            // Heading straight up from y=4.5 at speed 1 lands at 5.5 → clamped to the top wall.
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 1);

            var step = _resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Bounced, step.Outcome);
            Assert.AreEqual(5f, step.Position.y, 1e-4f, "clamped to the top wall");
            Assert.AreEqual(0, model.ShieldsRemaining.Value, "one shield consumed");
            Assert.Less(model.Direction.y, 0f, "reflected downward off the top wall");
        }

        [Test]
        public void Step_CrossingWallWithNoShieldLeft_Destroys()
        {
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 0);

            var step = _resolver.Step(model, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(ProjectileStepOutcome.Destroyed, step.Outcome);
            Assert.AreEqual(-1, model.ShieldsRemaining.Value, "decrement crossed below zero");
        }

        [Test]
        public void Deflect_ReflectsDirectionOffBalloonSurfaceNormal()
        {
            // Projectile directly above the balloon, travelling down → reflects to travelling up.
            var model = NewModel(direction: Vector2.down, speed: 1f, shields: 3);

            _resolver.Deflect(model, new Vector3(0f, 1f, 0f), Vector3.zero);

            Assert.Greater(model.Direction.y, 0f, "bounced back upward off the balloon");
        }

        [Test]
        public void Step_WithSpeedBuff_MovesTwiceAsFar()
        {
            var model = NewModel(direction: Vector2.up, speed: 1f, shields: 2);
            model.AddBuff(new ProjectileBuff(
                ProjectileBuffId.Speed, 2f, new WallBounceEndCondition(NeverFiringWallBounces())));

            var step = _resolver.Step(model, Vector3.zero, 1f);

            Assert.AreEqual(ProjectileStepOutcome.Moved, step.Outcome);
            Assert.AreEqual(new Vector3(0f, 2f, 0f), step.Position, "speed 1 x2 buff over dt 1 = 2 units");
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

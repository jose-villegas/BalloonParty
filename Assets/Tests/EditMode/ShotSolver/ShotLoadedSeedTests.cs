using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Solver;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     The ball in the thrower seeds the solve (<see cref="ShotBoardGather.SeedFor" />): one armed there, by a
    ///     Snipe pickup or the Sharpshooter card as it loaded, is planned as the lance it is, so the bots (Fire Best
    ///     Shot, the autoplay) aim it as one.
    /// </summary>
    [TestFixture]
    public class ShotLoadedSeedTests
    {
        private const float SnipeSpeed = 1.6f;
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

        [Test]
        public void SeedFor_NoBall_IsThePlainDefault()
        {
            var seed = ShotBoardGather.SeedFor(null);

            Assert.IsFalse(seed.IsPiercing);
            Assert.IsFalse(seed.HasSpeedBuff);
        }

        [Test]
        public void SeedFor_PlainBall_IsThePlainDefault()
        {
            var seed = ShotBoardGather.SeedFor(new ProjectileModel());

            Assert.IsFalse(seed.IsPiercing);
            Assert.IsFalse(seed.HasSpeedBuff);
            Assert.IsFalse(seed.RainbowBuffUntilWall);
            Assert.IsFalse(seed.RainbowBuffUntilPierceEnd);
        }

        [Test]
        public void SeedFor_ArmedBall_PiercesWithTheSnipesSpeed()
        {
            var seed = ShotBoardGather.SeedFor(ArmedBall(rainbow: false));

            Assert.IsTrue(seed.IsPiercing);
            Assert.IsTrue(seed.HasSpeedBuff);
            Assert.AreEqual(SnipeSpeed, seed.SpeedBuffMultiplier, 1e-5f);
            Assert.IsFalse(seed.RainbowBuffUntilPierceEnd);
        }

        [Test]
        public void SeedFor_RainbowArmedBall_CarriesTheRainbowOnThePierce()
        {
            var seed = ShotBoardGather.SeedFor(ArmedBall(rainbow: true));

            Assert.IsTrue(seed.IsPiercing);
            Assert.IsTrue(seed.RainbowBuffUntilPierceEnd);
            Assert.IsFalse(seed.RainbowBuffUntilWall);
        }

        [Test]
        public void Simulate_ArmedBallSeed_PopsWhatAPlainBallOnlyDeflectsOff()
        {
            // A golden straight ahead deflects a plain shot forever and pops only to a pierce: the seed alone
            // decides which the solver plans.
            var board = new[] { ShotBalloonSnapshot.ForGoldenTarget(new Vector2(0f, 2f), 0.3f, null) };

            var plain = Simulate(board, ShotBoardGather.SeedFor(new ProjectileModel()));
            var armed = Simulate(board, ShotBoardGather.SeedFor(ArmedBall(rainbow: false)));

            Assert.AreEqual(0, plain.GoldenPops);
            Assert.AreEqual(1, armed.GoldenPops);
        }

        private static ShotSimulationResult Simulate(ShotBalloonSnapshot[] board, in ShotFlightSeed seed)
        {
            return ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], rippleDamage: 1, seed: seed);
        }

        // As SnipeItemHandler.ArmLance leaves it: piercing, with the speed (and a rainbow host's) buff riding it.
        private static ProjectileModel ArmedBall(bool rainbow)
        {
            var ball = new ProjectileModel();
            ball.IsPiercing.Value = true;
            ball.AddBuff(new ProjectileBuff(
                ProjectileBuffId.Speed, SnipeSpeed, BuffModifierOp.Multiplicative,
                new PierceEndedEndCondition(ball.IsPiercing)));
            if (rainbow)
            {
                ball.AddBuff(new ProjectileBuff(
                    ProjectileBuffId.RainbowShield, 0f, BuffModifierOp.Flat,
                    new PierceEndedEndCondition(ball.IsPiercing)));
            }

            return ball;
        }
    }
}

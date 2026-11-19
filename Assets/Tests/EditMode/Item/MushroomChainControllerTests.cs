using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item;
using BalloonParty.Item.Snipe;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Solver;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     Paying the meadow's mushroom chain (phase 4 of <c>Plans/PLAN-MeadowMushrooms.md</c>):
    ///     <see cref="MushroomChainController" /> hears each bounce and, when the chain rule answered
    ///     <c>Arm</c>, arms the ball through <see cref="ISnipeArmer" /> and announces it. The first half drives it
    ///     with a recording armer; the second with the real Snipe handler; the last flies the game's own steps
    ///     into it, to hold that the bounce that arms is the end of its step and that the lance it makes cuts the
    ///     caps in its way and is not armed again.
    /// </summary>
    [TestFixture]
    public class MushroomChainControllerTests
    {
        private static readonly Vector3 BouncePoint = new(0.5f, 1.25f, 0f);
        private static readonly Vector2Int Slot = new(2, 4);

        private ProjectileRegistry _registry;
        private IMessageHandler<MushroomBouncedMessage> _bounced;
        private List<MushroomLanceMessage> _announced;
        private IPublisher<MushroomLanceMessage> _lancePublisher;
        private ISubscriber<MushroomBouncedMessage> _bouncedSubscriber;
        private ISnipeArmer _armer;
        private MushroomChainController _controller;

        [SetUp]
        public void SetUp()
        {
            _registry = new ProjectileRegistry();
            _announced = new List<MushroomLanceMessage>();
            _lancePublisher = Substitute.For<IPublisher<MushroomLanceMessage>>();
            _lancePublisher.Publish(Arg.Do<MushroomLanceMessage>(_announced.Add));
            _bouncedSubscriber = Substitute.For<ISubscriber<MushroomBouncedMessage>>();
            _bouncedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<MushroomBouncedMessage>>(h => _bounced = h),
                    Arg.Any<MessageHandlerFilter<MushroomBouncedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            _armer = Substitute.For<ISnipeArmer>();
            Build(_armer);
        }

        [Test]
        public void TheBounceThatCompletesAChain_ArmsTheBall_AndAnnouncesTheLance()
        {
            var ball = Fly();

            Bounce(ball, count: 3, MushroomReward.Arm);

            _armer.Received(1).ArmAt(ball, BouncePoint);
            Assert.AreEqual(1, _announced.Count);
            Assert.AreSame(ball, _announced[0].Projectile);
            Assert.AreEqual(BouncePoint, _announced[0].Position);
            Assert.AreEqual(3, _announced[0].Count);
        }

        // Every answer the rule gives but Arm: a bounce that pays nothing, and no bounce at all.
        [Test]
        public void ABounceThatPaysNothing_GrantsNothing_AndAnnouncesNothing(
            [Values((int)MushroomReward.None, (int)MushroomReward.PassThrough)] int reward)
        {
            var ball = Fly();

            Bounce(ball, count: 1, (MushroomReward)reward);
            Bounce(ball, count: 2, (MushroomReward)reward);

            AssertNothingGranted();
            Assert.AreEqual(0, _announced.Count);
        }

        [Test]
        public void ABallThatDiedBeforeItsRewardCouldLand_IsNotPaid()
        {
            var landed = Fly();
            _registry.Land(landed);
            var retired = Fly();
            _registry.Retire(retired);

            Bounce(landed, count: 3, MushroomReward.Arm);
            Bounce(retired, count: 3, MushroomReward.Arm);

            AssertNothingGranted();
            Assert.AreEqual(0, _announced.Count);
        }

        [Test]
        public void ABallStillInTheThrower_IsNotPaid()
        {
            var held = new ProjectileModel();
            _registry.Load(held, null);

            Bounce(held, count: 3, MushroomReward.Arm);

            AssertNothingGranted();
        }

        [Test]
        public void ABallThatCannotBeWrittenTo_IsNotPaid()
        {
            var readOnly = Substitute.For<IProjectileModel>();
            _registry.Load(readOnly, null);
            _registry.Launch(readOnly);

            Bounce(readOnly, count: 3, MushroomReward.Arm);

            AssertNothingGranted();
            Assert.AreEqual(0, _announced.Count);
        }

        // A run reset, a level change that sweeps the board and the game over all take the balls off through the
        // registry. The controller keeps nothing of its own, so there is nothing of the old run for it to pay.
        [Test]
        public void AfterTheBoardIsCleared_AnOldBallsRewardIsDropped_AndTheNextRunsIsPaid()
        {
            var old = Fly();
            _registry.Clear();

            Bounce(old, count: 3, MushroomReward.Arm);

            AssertNothingGranted();

            var next = Fly();
            Bounce(next, count: 3, MushroomReward.Arm);

            _armer.Received(1).ArmAt(next, BouncePoint);
            Assert.AreEqual(1, _announced.Count);
            Assert.AreSame(next, _announced[0].Projectile);
        }

        [Test]
        public void TwoBallsInFlight_AreEachPaidForTheirOwnChain()
        {
            var first = Fly();
            var second = Fly();

            Bounce(second, count: 3, MushroomReward.Arm);

            _armer.Received(1).ArmAt(second, BouncePoint);
            _armer.DidNotReceive().ArmAt(first, Arg.Any<Vector3>());
        }

        [Test]
        public void WithTheSnipesOwnHandler_TheThirdBounceArms_AndNoChargeIsBanked()
        {
            var lanceArming = Substitute.For<ISnipeLanceArming>();
            Build(RealHandler(lanceArming));
            var ball = Fly();

            Bounce(ball, count: 1, MushroomReward.None);
            Bounce(ball, count: 2, MushroomReward.None);
            Assert.IsFalse(ball.IsPiercing.Value, "two mushrooms pay nothing");

            Bounce(ball, count: 3, MushroomReward.Arm);

            Assert.IsTrue(ball.IsPiercing.Value, "the ball is a lance from the third bounce's message on");
            Assert.AreEqual(0, ball.Flight.BankedPierceCharges);
            lanceArming.Received(1).OnLanceArmedAt(ball, BouncePoint);
            Assert.AreEqual(1, _announced.Count);
        }

        // Every answer the rule gives but Arm pays nothing on both sides: no charge is saved, in the game or in
        // the simulator.
        [Test]
        public void AnAnswerThatIsNotArm_PaysNothing_InTheGameAndInTheSimulator(
            [Values((int)MushroomReward.None, (int)MushroomReward.PassThrough)] int reward)
        {
            Build(RealHandler(Substitute.For<ISnipeLanceArming>()));
            var ball = Fly();
            var simulated = new ShotFlightState(Vector2.zero, Vector2.up, 3);

            Bounce(ball, count: 4, (MushroomReward)reward);
            ShotSimulator.PayMushroomReward((MushroomReward)reward, 1.6f, ref simulated);

            Assert.IsFalse(ball.IsPiercing.Value);
            Assert.AreEqual(0, ball.Flight.BankedPierceCharges);
            Assert.AreEqual(0, _announced.Count);
            Assert.IsFalse(simulated.IsPiercing);
            Assert.AreEqual(0, simulated.BankedPierceCharges);
            Assert.IsFalse(simulated.HasSpeedBuff);
            Assert.AreEqual(0, simulated.Mushrooms.LancesArmed);
        }

        [Test]
        public void AnArm_ArmsWithTheSnipesSpeedBuff_AndBanksNothing_InTheGameAndInTheSimulator()
        {
            Build(RealHandler(Substitute.For<ISnipeLanceArming>()));
            var ball = Fly();
            var simulated = new ShotFlightState(Vector2.zero, Vector2.up, 3);

            Bounce(ball, count: 3, MushroomReward.Arm);
            ShotSimulator.PayMushroomReward(MushroomReward.Arm, 1.6f, ref simulated);

            Assert.IsTrue(ball.IsPiercing.Value);
            Assert.AreEqual(0, ball.Flight.BankedPierceCharges);
            Assert.AreEqual(ball.IsPiercing.Value, simulated.IsPiercing);
            Assert.AreEqual(ball.Flight.BankedPierceCharges, simulated.BankedPierceCharges);
            Assert.IsTrue(simulated.HasSpeedBuff);
            Assert.AreEqual(1.6f, simulated.SpeedBuffMultiplier);
            Assert.AreEqual(1, simulated.Mushrooms.LancesArmed);
        }

        // The whole of it, the rule included: the game's own steps trade a ball between two mushrooms, every
        // bounce is handed to the controller as the view hands it, and the real Snipe handler pays. The third
        // bounce arms the ball, and from there it is a lance: it bounces off neither mushroom again, cuts the cap
        // of each as its path enters it, and is never armed a second time. Each cap it cuts banks it a charge
        // (decision K): two caps, two charges.
        [Test]
        public void FlownByTheGame_TheThirdBounceArms_AndTheLanceCutsBothCapsAndBouncesOffNeither()
        {
            Build(RealHandler(Substitute.For<ISnipeLanceArming>()));
            var settings = new MushroomSettings();
            var field = new TwoFacingBumpers();
            var resolver = Resolver(field, settings);
            var ball = Fly();
            ball.Direction = Vector2.up;
            ball.Speed = 1f;
            ball.ShieldsRemaining.Value = 50;
            var position = Vector3.zero;
            var bounces = 0;
            var bouncesAsALance = 0;
            var cuts = new List<int>();

            for (var i = 0; i < 400; i++)
            {
                var wasPiercing = ball.IsPiercing.Value;
                var step = resolver.Step(ball, position, 0.5f);
                position = step.Position;
                bounces += step.BumperBounces;
                bouncesAsALance += wasPiercing ? step.BumperBounces : 0;
                foreach (var cut in resolver.StepCapCuts)
                {
                    cuts.Add(cut.BumperId);
                }

                Announce(resolver, ball, step);
            }

            Assert.AreEqual(settings.Rules.ChainTarget, bounces, "up, down, up: the third bounce armed the ball");
            Assert.AreEqual(0, bouncesAsALance);
            Assert.IsTrue(ball.IsPiercing.Value);
            Assert.AreEqual(1, _announced.Count, "armed once");
            Assert.AreEqual(2, ball.Flight.BankedPierceCharges, "and a charge banked for each cap it cut");
            Assert.AreEqual(2, ball.Flight.CapsCut);
            CollectionAssert.AreEqual(
                new[] { TwoFacingBumpers.LowerId, TwoFacingBumpers.UpperId }, cuts,
                "the lower cap on the way down, and the cap that armed the ball once it came back to it");
            Assert.AreEqual(0, field.Bumpers.Count, "a cut cap has left the field");
        }

        // The bounce that arms is the end of its step: a wall just beyond the mushroom is not reached in it. So
        // the ball is alive and unspent when it is armed, and the wall is the lance's, a step later.
        [Test]
        public void FlownByTheGame_TheStepThatArms_EndsOnTheMushroom_ShortOfTheWallBeyondIt(
            [Values(0, 1)] int shields)
        {
            var lanceArming = Substitute.For<ISnipeLanceArming>();
            Build(RealHandler(lanceArming));
            var settings = new MushroomSettings();
            var field = new TwoFacingBumpers();
            var resolver = Resolver(field, settings);
            var ball = Fly();
            ball.Direction = Vector2.up;
            ball.Speed = 1f;
            ball.ShieldsRemaining.Value = shields;
            ball.Flight.MushroomChain.Count = settings.Rules.ChainTarget - 1;
            ball.Flight.MushroomChain.RememberLast(TwoFacingBumpers.LowerId);

            // With the lower mushroom gone the ball has 1.5 to fly up to the upper one, and a step of 20: flown
            // on, it would come straight back down to the bottom wall, 6.5 past the bounce.
            field.RemoveLower();
            var step = resolver.Step(ball, Vector3.zero, 20f);
            var wasInFlightWhenPaid = _registry.IsInFlight(ball);
            Announce(resolver, ball, step);

            Assert.AreEqual(ProjectileStepOutcome.BumperBounced, step.Outcome, "no wall in the step that arms");
            Assert.Less(Vector3.Distance(new Vector3(0f, 1.5f, 0f), step.Position), 1e-5f, "it ends on the mushroom");
            Assert.AreEqual(shields, ball.ShieldsRemaining.Value, "no shield spent");
            Assert.IsTrue(wasInFlightWhenPaid);
            Assert.IsTrue(ball.IsPiercing.Value, "armed there");
            Assert.AreEqual(1, _announced.Count);
            lanceArming.Received(1).OnLanceArmedAt(ball, Arg.Any<Vector3>());

            // The next step is the lance's: it reaches the wall as one.
            var next = resolver.Step(ball, step.Position, 20f);
            Announce(resolver, ball, next);

            Assert.AreEqual(
                shields > 0 ? ProjectileStepOutcome.Bounced : ProjectileStepOutcome.Destroyed, next.Outcome);
            Assert.AreEqual(1, _announced.Count, "and nothing more is announced for it, dead or alive");
        }

        private void AssertNothingGranted()
        {
            _armer.DidNotReceiveWithAnyArgs().ArmAt(default, default);
        }

        private static ProjectileMotionResolver Resolver(IBumperField field, IMushroomSettings settings)
        {
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(new Vector4(5f, 5f, -5f, -5f));
            return new ProjectileMotionResolver(
                config, new ProjectileTapResolver(config), field, new TrueReflectRule(), settings);
        }

        // What the view does with a step's bounces: each is published as it stands, in order.
        private void Announce(ProjectileMotionResolver resolver, IProjectileModel ball, in ProjectileStep step)
        {
            var bounces = new List<BumperBounce>(resolver.StepBumperBounces);
            foreach (var bounce in bounces)
            {
                _bounced.Handle(bounce.ToMessage(ball, step.Speed));
            }
        }

        private void Build(ISnipeArmer armer)
        {
            _controller = new MushroomChainController(_bouncedSubscriber, _lancePublisher, armer, _registry);
            _controller.Start();
        }

        private SnipeItemHandler RealHandler(ISnipeLanceArming lanceArming)
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", ItemType.Snipe);
            SetField(settings.Snipe, "_snipeSpeedBuffMultiplier", 1.6f);
            var itemConfig = Substitute.For<IItemConfiguration>();
            itemConfig[ItemType.Snipe].Returns(settings);
            var palette = Substitute.For<IGamePalette>();
            return new SnipeItemHandler(
                itemConfig, palette, new ItemEffectPlayer(new PoolManager(), palette),
                Substitute.For<IProjectileBuffs>(), _registry,
                Substitute.For<ISubscriber<PierceDischargedMessage>>(), lanceArming);
        }

        private ProjectileModel Fly()
        {
            var ball = new ProjectileModel();
            ball.ShieldsRemaining.Value = 3;
            _registry.Load(ball, null);
            _registry.Launch(ball);
            return ball;
        }

        private void Bounce(IProjectileModel ball, int count, MushroomReward reward)
        {
            _bounced.Handle(new MushroomBouncedMessage(
                ball, Slot, count - 1, BouncePoint, Vector2.up, 9.87f, count, reward));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }

        // Two mushrooms facing each other across the origin, 3 apart: a ball sent straight up between them is
        // traded from one to the other.
        private sealed class TwoFacingBumpers : IBumperField
        {
            internal const int UpperId = 0;
            internal const int LowerId = 1;

            private readonly List<BumperCircle> _bumpers = new()
            {
                new BumperCircle(UpperId, new Vector2(0f, 2f), 0.5f),
                new BumperCircle(LowerId, new Vector2(0f, -2f), 0.5f),
            };

            public IReadOnlyList<BumperCircle> Bumpers => _bumpers;

            public Vector2Int SlotAt(int index)
            {
                return new Vector2Int(_bumpers[index].Id, 0);
            }

            public void Cut(int index)
            {
                _bumpers.RemoveAt(index);
            }

            internal void RemoveLower()
            {
                _bumpers.RemoveAt(1);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Game.Level;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Snipe;
using BalloonParty.Nudge;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The nuke's blast (PLAN-NukeBalloon phase 7): <see cref="NukeBlast.Detonate" /> pops the nuke, kills the shots
    ///     in flight and grows a disc that pops every non-reward balloon its edge reaches, spares and nudges the other
    ///     rewards, sweeps up late arrivals at its end and then grants the spawn clock's breather. A level change, game
    ///     over or run reset stops it with no breather. A meadow mushroom stays where it is and the edge blows its cap
    ///     off as it reaches it.
    /// </summary>
    [TestFixture]
    public class NukeBlastTests
    {
        private static readonly Vector2Int NukeSlot = new(0, 0);
        private static readonly Vector2Int NearSlot = new(1, 0);
        private static readonly Vector2Int FarSlot = new(5, 9);

        // A tenth of the blast's second: the disc has reached about two slots out, short of the far corner.
        private const float EarlyStep = 0.1f;
        private const float WholeBlast = 10f;

        private SlotGrid _grid;
        private IPublisher<NudgeMessage> _nudgePublisher;
        private IPublisher<ClearShotsInFlightMessage> _shotsPublisher;
        private IPublisher<NukeDetonatedMessage> _detonatedPublisher;
        private IPublisher<NukeBreatherMessage> _breatherPublisher;
        private IBalloonPopper _popper;
        private IBreatherGrant _breather;
        private INukeBlastView _view;
        private INukeCloudView _cloud;
        private IMushroomBlast _mushrooms;
        private ReactiveProperty<int> _level;
        private IMessageHandler<GameOverMessage> _gameOverHandler;
        private NukeBlast _blast;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gridConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.NukeBlast.Returns(new NukeBlastSettings());

            _nudgePublisher = Substitute.For<IPublisher<NudgeMessage>>();
            _shotsPublisher = Substitute.For<IPublisher<ClearShotsInFlightMessage>>();
            _detonatedPublisher = Substitute.For<IPublisher<NukeDetonatedMessage>>();
            _breatherPublisher = Substitute.For<IPublisher<NukeBreatherMessage>>();
            // A real pop frees the slot: the sweep must not find the same balloon again.
            _popper = Substitute.For<IBalloonPopper>();
            _popper.TryPopSingle(Arg.Any<IBalloonModel>(), Arg.Any<bool>()).Returns(call =>
            {
                _grid.Remove(call.Arg<IBalloonModel>().SlotIndex.Value);
                return true;
            });
            _breather = Substitute.For<IBreatherGrant>();
            _view = Substitute.For<INukeBlastView>();
            _cloud = Substitute.For<INukeCloudView>();
            _mushrooms = Substitute.For<IMushroomBlast>();
            _level = new ReactiveProperty<int>(1);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);

            var gameOver = Substitute.For<ISubscriber<GameOverMessage>>();
            gameOver
                .Subscribe(Arg.Any<IMessageHandler<GameOverMessage>>(), Arg.Any<MessageHandlerFilter<GameOverMessage>[]>())
                .Returns(call =>
                {
                    _gameOverHandler = call.Arg<IMessageHandler<GameOverMessage>>();
                    return Substitute.For<IDisposable>();
                });

            _blast = new NukeBlast(
                gameOver, levelProgress, _grid, balloons, _nudgePublisher, _shotsPublisher, _detonatedPublisher,
                _breatherPublisher, _popper, _breather, _view, _cloud, _mushrooms);
            _blast.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _blast.Dispose();
        }

        [Test]
        public void Detonate_StartsTheBlast_AtTheNukesPosition_WithTheCloud()
        {
            var nuke = PlaceNuke();
            var center = _grid.IndexToWorldPosition(NukeSlot);

            _blast.Detonate(nuke, center);

            Assert.IsTrue(_blast.IsRunning);
            _view.Received(1).Show(center);
            _cloud.Received(1).Play(center);
            _detonatedPublisher.Received(1).Publish(Arg.Is<NukeDetonatedMessage>(m => m.Position == center));
        }

        [Test]
        public void Detonate_PopsTheNukeItself_AndClearsTheShotsInFlight()
        {
            var nuke = PlaceNuke();

            Detonate(nuke);

            _popper.Received(1).TryPopSingle(nuke, Arg.Any<bool>());
            _shotsPublisher.Received(1).Publish(Arg.Any<ClearShotsInFlightMessage>());
            Assert.IsTrue(_grid.IsEmpty(NukeSlot.x, NukeSlot.y), "the nuke's slot is freed by its pop");
        }

        [Test]
        public void TheBlast_PublishesNothingAboutTheBreather_UntilItEnds()
        {
            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);

            _breatherPublisher.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void TheEdge_PopsNearBalloonsBeforeFarOnes()
        {
            var near = PlaceBalloon(NearSlot);
            var far = PlaceBalloon(FarSlot);

            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);

            _popper.Received(1).TryPopSingle(near, Arg.Any<bool>());
            _popper.DidNotReceive().TryPopSingle(far, Arg.Any<bool>());
            Assert.IsTrue(_blast.IsRunning);

            _blast.Advance(WholeBlast);

            _popper.Received(1).TryPopSingle(far, Arg.Any<bool>());
        }

        [Test]
        public void ByTheEnd_EveryNonRewardBalloon_IsPoppedOnce_AndTheNukeOnlyByItsDetonation()
        {
            var nuke = PlaceNuke();
            var a = PlaceBalloon(NearSlot);
            var b = PlaceBalloon(new Vector2Int(3, 4));
            var c = PlaceBalloon(FarSlot);
            Detonate(nuke);

            _blast.Advance(EarlyStep);
            _blast.Advance(EarlyStep);
            _blast.Advance(WholeBlast);

            foreach (var balloon in new IBalloonModel[] { a, b, c, nuke })
            {
                _popper.Received(1).TryPopSingle(balloon, Arg.Any<bool>());
            }

            Assert.IsFalse(_blast.IsRunning);
            _view.Received(1).Hide();
        }

        [Test]
        public void RewardBalloons_AreSpared_AndNudgedOnceAwayFromThePop()
        {
            var golden = new GoldenBalloonModel(new BalloonModelConfig(BalloonType.Golden, hitsToPop: 1));
            var heart = new HeartBalloonModel(new BalloonModelConfig(BalloonType.Heart, hitsToPop: 1));
            var disco = new DiscoBalloonModel(new BalloonModelConfig(BalloonType.Disco, hitsToPop: 1));
            _grid.Place(golden, null, NearSlot);
            _grid.Place(heart, null, new Vector2Int(2, 2));
            _grid.Place(disco, null, FarSlot);

            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);
            _blast.Advance(EarlyStep);
            _blast.Advance(WholeBlast);

            foreach (var reward in new IBalloonModel[] { golden, heart, disco })
            {
                _popper.DidNotReceive().TryPopSingle(reward, Arg.Any<bool>());
                _nudgePublisher.Received(1).Publish(Arg.Is<NudgeMessage>(m =>
                    ReferenceEquals(m.Actor, reward) && m.Source == NudgeType.Wave));
            }

            Assert.IsFalse(_grid.IsEmpty(NearSlot.x, NearSlot.y), "the golden balloon is still standing");
        }

        [Test]
        public void ABalloonArrivingWhereTheDiscHasPassed_IsStillClearedByTheLastSweep()
        {
            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);
            var late = PlaceBalloon(NearSlot);

            _blast.Advance(WholeBlast);

            _popper.Received(1).TryPopSingle(late, Arg.Any<bool>());
        }

        [Test]
        public void ABalloonThePopperRefuses_IsTriedOnce_AndTheBlastStillEnds()
        {
            var stray = PlaceBalloon(NearSlot);
            _popper.TryPopSingle(stray, Arg.Any<bool>()).Returns(false);

            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);
            _blast.Advance(EarlyStep);
            _blast.Advance(WholeBlast);

            _popper.Received(1).TryPopSingle(stray, Arg.Any<bool>());
            Assert.IsFalse(_blast.IsRunning);
            _breather.Received(1).Grant();
        }

        [Test]
        public void TheEnd_GrantsTheBreatherOnce_AfterTheSweep_AndAnnouncesIt()
        {
            PlaceBalloon(FarSlot);
            var center = _grid.IndexToWorldPosition(NukeSlot);
            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);
            _breather.DidNotReceive().Grant();

            _blast.Advance(WholeBlast);
            _blast.Advance(WholeBlast);

            _breather.Received(1).Grant();
            _breatherPublisher.Received(1).Publish(Arg.Is<NukeBreatherMessage>(m => m.Position == center));
        }

        [Test]
        public void ALevelChange_StopsTheBlast_WithNoBreatherAndNoMorePops_AndRemovesTheCloud()
        {
            var far = PlaceBalloon(FarSlot);
            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);

            _level.Value = 2;
            _blast.Advance(WholeBlast);

            Assert.IsFalse(_blast.IsRunning);
            _popper.DidNotReceive().TryPopSingle(far, Arg.Any<bool>());
            _breather.DidNotReceive().Grant();
            _breatherPublisher.DidNotReceiveWithAnyArgs().Publish(default);
            _view.Received(1).Hide();
            _cloud.Received(1).Stop();
        }

        [Test]
        public void GameOver_StopsTheBlast_WithNoBreatherAndNoMorePops()
        {
            var far = PlaceBalloon(FarSlot);
            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);

            _gameOverHandler.Handle(new GameOverMessage(3, 100));
            _blast.Advance(WholeBlast);

            Assert.IsFalse(_blast.IsRunning);
            _popper.DidNotReceive().TryPopSingle(far, Arg.Any<bool>());
            _breather.DidNotReceive().Grant();
        }

        [Test]
        public void ARunReset_StopsTheBlast_WithNoBreatherAndNoMorePops_AndRemovesTheCloud()
        {
            var far = PlaceBalloon(FarSlot);
            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);

            _blast.ResetRun(1);
            _blast.Advance(WholeBlast);

            Assert.IsFalse(_blast.IsRunning);
            _popper.DidNotReceive().TryPopSingle(far, Arg.Any<bool>());
            _breather.DidNotReceive().Grant();
            _view.Received(1).Hide();
            _cloud.Received(1).Stop();
        }

        [Test]
        public void ASecondDetonationWhileRunning_FinishesTheFirstWithItsBreather_ThenStartsAnother()
        {
            var far = PlaceBalloon(FarSlot);
            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);
            var second = new NukeBalloonModel(new BalloonModelConfig(BalloonType.Nuke, hitsToPop: 3));
            _grid.Place(second, null, new Vector2Int(2, 2));

            _blast.Detonate(second, _grid.IndexToWorldPosition(new Vector2Int(2, 2)));

            _popper.Received(1).TryPopSingle(far, Arg.Any<bool>());
            _breather.Received(1).Grant();
            Assert.IsTrue(_blast.IsRunning);
            _view.Received(2).Show(Arg.Any<Vector3>());
        }

        [Test]
        public void ANewDetonation_AfterTheFirstEnded_NudgesAndSweepsAgain()
        {
            var first = PlaceNuke();
            Detonate(first);
            _blast.Advance(WholeBlast);
            var nuke = PlaceNuke();
            var balloon = PlaceBalloon(NearSlot);

            Detonate(nuke);
            _blast.Advance(WholeBlast);

            _popper.Received(1).TryPopSingle(balloon, Arg.Any<bool>());
            _breather.Received(2).Grant();
        }

        // The meadow's mushrooms: the nuke spares none. A cap goes as the edge reaches its mushroom, near before
        // far, each told once with the blast's centre, and the mushroom itself stays on the grid.
        [Test]
        public void TheEdge_BlowsTheCapOffNearMushroomsBeforeFarOnes_EachOnce_FromTheNukesCentre()
        {
            var center = (Vector2)_grid.IndexToWorldPosition(NukeSlot);
            _grid.Place(new MushroomBumperModel(0), null, NearSlot);
            _grid.Place(new MushroomBumperModel(1), null, FarSlot);

            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);

            _mushrooms.Received(1).BlowOff(NearSlot, center);
            _mushrooms.DidNotReceive().BlowOff(FarSlot, Arg.Any<Vector2>());

            _blast.Advance(EarlyStep);
            _blast.Advance(WholeBlast);

            _mushrooms.Received(1).BlowOff(NearSlot, center);
            _mushrooms.Received(1).BlowOff(FarSlot, center);
            _mushrooms.DidNotReceiveWithAnyArgs().Bomb(default, default, default, default);
            Assert.IsFalse(_grid.IsEmpty(NearSlot.x, NearSlot.y), "the mushroom stands, a bare stem");
            Assert.IsFalse(_grid.IsEmpty(FarSlot.x, FarSlot.y));
            _popper.Received(1).TryPopSingle(Arg.Any<IBalloonModel>(), Arg.Any<bool>());
        }

        // A blast that a level change stops has blown the caps it reached and no others: the far mushroom is
        // never told, and the new level's board is placed whole.
        [Test]
        public void ALevelChange_StopsTheCapsGoing_WhereTheEdgeStood()
        {
            _grid.Place(new MushroomBumperModel(0), null, NearSlot);
            _grid.Place(new MushroomBumperModel(1), null, FarSlot);
            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);

            _level.Value = 2;
            _blast.Advance(WholeBlast);

            _mushrooms.Received(1).BlowOff(NearSlot, Arg.Any<Vector2>());
            _mushrooms.DidNotReceive().BlowOff(FarSlot, Arg.Any<Vector2>());
        }

        // The same with nothing stood in for: the real MushroomBlast over the real board. A nuke that a level
        // change stops has blown the near cap and no other; the next level's mushrooms are placed whole, the
        // blast blows none of them, nothing of the old board is cut on the new one, and no regrow follows.
        [Test]
        public void ANukeStoppedByALevelChange_LeavesNothingBlownOrCutOnTheNextLevel()
        {
            var settings = new MushroomSettings();
            var origin = Substitute.For<IShotOrigin>();
            origin.ProjectileContactRadius.Returns(0.125f);
            var board = new MushroomBoard(_grid, settings, origin);
            var blown = new List<Vector2Int>();
            var cutPublisher = Substitute.For<IPublisher<MushroomCutMessage>>();
            cutPublisher.Publish(Arg.Do<MushroomCutMessage>(message => blown.Add(message.Slot)));
            var real = new MushroomBlast(
                board, _grid, settings, cutPublisher, Substitute.For<IPublisher<MushroomSwayedMessage>>());
            _mushrooms
                .When(mushrooms => mushrooms.BlowOff(Arg.Any<Vector2Int>(), Arg.Any<Vector2>()))
                .Do(call => real.BlowOff(call.Arg<Vector2Int>(), call.Arg<Vector2>()));
            var beat = new Beat();
            var regrown = 0;
            var regrownPublisher = Substitute.For<IPublisher<MushroomRegrownMessage>>();
            regrownPublisher.Publish(Arg.Do<MushroomRegrownMessage>(_ => regrown++));
            var balls = Substitute.For<IProjectileRegistry>();
            var regrow = new MushroomRegrowController(board, beat, balls, regrownPublisher);
            _grid.Place(new MushroomBumperModel(0), null, NearSlot);
            _grid.Place(new MushroomBumperModel(1), null, FarSlot);

            Detonate(PlaceNuke());
            _blast.Advance(EarlyStep);
            regrow.Tick();

            CollectionAssert.AreEqual(new[] { NearSlot }, blown, "the near cap, before the level changed");
            Assert.AreEqual(1, board.Bumpers.Count);
            Assert.AreEqual(1, board.CutCount);

            // The level changes: the blast stops, the old mushrooms leave and the next level's are placed.
            _level.Value = 2;
            _grid.Remove(NearSlot);
            _grid.Remove(FarSlot);
            var nextNear = new Vector2Int(2, 1);
            _grid.Place(new MushroomBumperModel(0), null, nextNear);
            _grid.Place(new MushroomBumperModel(1), null, FarSlot);
            _blast.Advance(WholeBlast);
            for (var i = 0; i < 8; i++)
            {
                beat.Count++;
                regrow.Tick();
            }

            Assert.IsFalse(_blast.IsRunning);
            Assert.AreEqual(1, blown.Count, "nothing blown on the new level");
            Assert.AreEqual(2, board.Bumpers.Count, "its caps are whole");
            Assert.AreEqual(0, board.CutCount, "and nothing of the old board is cut on it");
            Assert.AreEqual(0, regrown, "so nothing grows back onto it");
        }

        // The other grid actors are spared as before, and told nothing.
        [Test]
        public void AGridActorThatIsNoMushroom_IsNotBlown()
        {
            _grid.Place(new BushObstacleModel(), null, NearSlot);

            Detonate(PlaceNuke());
            _blast.Advance(WholeBlast);

            _mushrooms.DidNotReceiveWithAnyArgs().BlowOff(default, default);
            Assert.IsFalse(_grid.IsEmpty(NearSlot.x, NearSlot.y));
        }

        private void Detonate(NukeBalloonModel nuke)
        {
            _blast.Detonate(nuke, _grid.IndexToWorldPosition(nuke.SlotIndex.Value));
        }

        private NukeBalloonModel PlaceNuke()
        {
            var nuke = new NukeBalloonModel(new BalloonModelConfig(BalloonType.Nuke, hitsToPop: 3));
            _grid.Place(nuke, null, NukeSlot);
            return nuke;
        }

        private sealed class Beat : IBoardBeat
        {
            internal int Count;

            public int OffBeats(float share)
            {
                return Count;
            }
        }

        private BalloonModel PlaceBalloon(Vector2Int slot)
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            model.Color.Value = "Red";
            _grid.Place(model, null, slot);
            return model;
        }
    }
}

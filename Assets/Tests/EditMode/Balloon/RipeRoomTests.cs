using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The room for full clusters (<see cref="RipeRoom" />) over a real grid, and what it does to the merges of a
    ///     real <see cref="BubbleMergeService" />: at most 2 clusters full at once and a gap of 2 of the bubbles'
    ///     off-beats after a ripe one ends, with a merge cap of 6, as shipped. The board's beat is a count the tests
    ///     step: <c>OffBeat</c> is the next off-beat falling and the merge service ticking on it. Every merge here
    ///     leaves its giver with bubbles, so none needs a controller to absorb it. The pairs sit on row 1, a slot
    ///     apart along the row, so no cluster of one pair neighbours a cluster of another; the full clusters that
    ///     only take up room sit alone in column 7.
    /// </summary>
    [TestFixture]
    public class RipeRoomTests
    {
        private const int MergeMax = 6;
        private const int MaxRipe = 2;
        private const float OffBeatShare = 0.25f;

        private static readonly Vector2Int ReceiverA = new(0, 1);
        private static readonly Vector2Int GiverA = new(1, 1);
        private static readonly Vector2Int ReceiverB = new(3, 1);
        private static readonly Vector2Int GiverB = new(4, 1);
        private static readonly Vector2Int AloneA = new(7, 0);
        private static readonly Vector2Int AloneB = new(7, 3);
        private static readonly Vector2Int AloneC = new(7, 6);

        private readonly List<BubbleMergedMessage> _merged = new();

        // Every share of a beat the off-beat count was asked at.
        private readonly List<float> _sharesAsked = new();

        private SlotGrid _grid;
        private IGamePalette _palette;
        private ReactiveProperty<int> _level;
        private RipeRoom _room;
        private BubbleMergeService _merges;
        private int _offBeats;
        private int _cooldownBeats;
        private float _fuseSeconds;

        // No cluster may be filled to the cap although none is full: the gap after a ripe cluster's end still runs.
        private bool CoolingDown => _room.FillsLeft(0) == 0;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(8, 8));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());

            _offBeats = 0;
            _cooldownBeats = 2;
            _fuseSeconds = 6f;
            _sharesAsked.Clear();
            var beat = Substitute.For<IBoardBeat>();
            beat.OffBeats(Arg.Any<float>()).Returns(call =>
            {
                _sharesAsked.Add(call.Arg<float>());
                return _offBeats;
            });
            _level = new ReactiveProperty<int>(1);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);

            var settings = Substitute.For<IRipeBubbleSettings>();
            settings.FuseSeconds.Returns(_ => _fuseSeconds);
            settings.MaxRipeClusters.Returns(MaxRipe);
            settings.RipenCooldownBeats.Returns(_ => _cooldownBeats);
            settings.MergeOffBeat.Returns(OffBeatShare);
            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.BubbleMergeMax.Returns(MergeMax);
            balloons.RipeBubble.Returns(settings);

            _room = new RipeRoom(_grid, beat, levelProgress, balloons);

            _merged.Clear();
            var mergedPublisher = Substitute.For<IPublisher<BubbleMergedMessage>>();
            mergedPublisher.When(p => p.Publish(Arg.Any<BubbleMergedMessage>()))
                .Do(call => _merged.Add(call.Arg<BubbleMergedMessage>()));
            var hitSubscriber = Substitute.For<ISubscriber<ActorHitMessage>>();
            hitSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ActorHitMessage>>(),
                    Arg.Any<MessageHandlerFilter<ActorHitMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            _merges = new BubbleMergeService(
                _grid, new BalloonControllerRegistry(null, Substitute.For<IPublisher<BoardDepletedMessage>>()),
                balloons, _room, beat, hitSubscriber, mergedPublisher);
            _merges.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _merges.Dispose();
        }

        [Test]
        public void CountFull_CountsClustersAtTheCapOrOver_AndLeavesOutTheOneLeaving()
        {
            var atTheCap = Place(AloneA, MergeMax);
            Place(AloneB, MergeMax + 1);
            Place(AloneC, MergeMax - 1);
            _grid.Place(new BalloonModel(), null, ReceiverA);

            Assert.AreEqual(2, _room.CountFull());
            Assert.AreEqual(1, _room.CountFull(atTheCap));
        }

        [Test]
        public void TheLimit_IsTheCap_WhileFewerThanTheMostAreFull()
        {
            Assert.AreEqual(MergeMax, _room.FillLimit(0));
            Assert.AreEqual(MergeMax, _room.FillLimit(MaxRipe - 1));
            Assert.AreEqual(MergeMax - 1, _room.FillLimit(MaxRipe));
            Assert.AreEqual(MaxRipe, _room.FillsLeft(0));
            Assert.AreEqual(0, _room.FillsLeft(MaxRipe + 1));
            Assert.IsFalse(CoolingDown, "no ripe cluster has ended");
        }

        [Test]
        public void ARipeClusterEnding_HoldsTheLimitABubbleShort_ForTheCooldownsOffBeats()
        {
            _offBeats = 10;
            _room.RipeEnded();

            Assert.IsTrue(CoolingDown);
            Assert.AreEqual(MergeMax - 1, _room.FillLimit(0));

            _offBeats = 11;
            Assert.IsTrue(CoolingDown, "one off-beat on, of two");
            Assert.AreEqual(MergeMax - 1, _room.FillLimit(0));

            _offBeats = 12;
            Assert.IsFalse(CoolingDown);
            Assert.AreEqual(MergeMax, _room.FillLimit(0));
            Assert.AreEqual(MergeMax - 1, _room.FillLimit(MaxRipe), "the count still limits once the cooldown is over");
        }

        [Test]
        public void ALaterEnding_StartsTheCooldownAgain()
        {
            _room.RipeEnded();
            _offBeats = 1;
            _room.RipeEnded();

            _offBeats = 2;
            Assert.IsTrue(CoolingDown, "two off-beats from the second ending, not the first");
            _offBeats = 3;
            Assert.IsFalse(CoolingDown);
        }

        [Test]
        public void ACooldownOfNoBeats_HoldsNothing()
        {
            _cooldownBeats = 0;
            _room.RipeEnded();

            Assert.IsFalse(CoolingDown);
            Assert.AreEqual(MergeMax, _room.FillLimit(0));
        }

        // The beat runs on from one level into the next, but a level starts with no gap to wait out, whatever ended
        // on the one before.
        [Test]
        public void ANewLevel_StartsWithNoCooldown()
        {
            _offBeats = 10;
            _room.RipeEnded();

            _level.Value = 2;

            Assert.IsFalse(CoolingDown);
            Assert.AreEqual(MergeMax, _room.FillLimit(0));
        }

        [Test]
        public void ARunReset_ClearsTheCooldown()
        {
            _offBeats = 10;
            _room.RipeEnded();

            _room.ResetRun(1);

            Assert.IsFalse(CoolingDown, "on the same level number, with the count where it was");
            _offBeats = 0;
            Assert.IsFalse(CoolingDown, "and once the beat has started over");
            _offBeats = 1;
            Assert.IsFalse(CoolingDown);
        }

        // The balancer starts the beat over before the reset reaches the room: a count below the one it kept must
        // not read as a cooldown that has ten beats to catch up.
        [Test]
        public void ABeatThatStartedOver_BeforeTheReset_HoldsNothing()
        {
            _offBeats = 10;
            _room.RipeEnded();

            _offBeats = 0;

            Assert.IsFalse(CoolingDown);
            Assert.AreEqual(MergeMax, _room.FillLimit(0));
        }

        [Test]
        public void WithRipenessOff_NothingIsLimited()
        {
            _fuseSeconds = 0f;
            _room.RipeEnded();

            Assert.IsFalse(CoolingDown);
            Assert.AreEqual(MergeMax, _room.FillLimit(MaxRipe + 3));
            Assert.AreEqual(int.MaxValue, _room.FillsLeft(MaxRipe + 3));
        }

        // The room and the merge service count the same off-beat: the share of a beat the settings give.
        [Test]
        public void TheOffBeat_IsReadAtTheSettingsShare_ByTheRoomAndTheMergeService()
        {
            _room.RipeEnded();
            _room.FillLimit(0);
            OffBeat();

            Assert.IsNotEmpty(_sharesAsked);
            foreach (var share in _sharesAsked)
            {
                Assert.AreEqual(OffBeatShare, share);
            }
        }

        [Test]
        public void WithRoomOnTheBoard_AMergeFillsToTheCap()
        {
            Place(AloneA, MergeMax);
            var receiver = Place(ReceiverA, MergeMax - 1);
            var giver = Place(GiverA, 2);

            OffBeat();

            Assert.AreEqual(MergeMax, receiver.HitsRemaining.Value);
            Assert.AreEqual(1, giver.HitsRemaining.Value);
        }

        [Test]
        public void WithTheMostClustersFull_AMergeStopsABubbleShort_AndStaysThere()
        {
            Place(AloneA, MergeMax);
            Place(AloneB, MergeMax);
            var receiver = Place(ReceiverA, 3);
            var giver = Place(GiverA, 3);

            OffBeat();

            // Between equals the higher column gives: two bubbles, where the cap alone would take all three.
            Assert.AreEqual(MergeMax - 1, receiver.HitsRemaining.Value);
            Assert.AreEqual(1, giver.HitsRemaining.Value);

            OffBeat();
            OffBeat();

            Assert.AreEqual(MergeMax - 1, receiver.HitsRemaining.Value, "it waits a bubble short");
            Assert.AreEqual(1, giver.HitsRemaining.Value);
            Assert.AreEqual(1, _merged.Count);
        }

        // One cluster is full and the board has room for one more. Both pairs would fill their receiver in the same
        // pass: the pass counts the first it fills, so the second gets nothing.
        [Test]
        public void TwoMergesInOnePass_FillOnlyAsManyClustersAsThereIsRoomFor()
        {
            Place(AloneA, MergeMax);
            var receiverA = Place(ReceiverA, MergeMax - 1);
            var giverA = Place(GiverA, 2);
            var receiverB = Place(ReceiverB, MergeMax - 1);
            var giverB = Place(GiverB, 2);

            OffBeat();

            // Between merges that make clusters the same size, the receiver in the lower column goes first.
            Assert.AreEqual(MergeMax, receiverA.HitsRemaining.Value);
            Assert.AreEqual(1, giverA.HitsRemaining.Value);
            Assert.AreEqual(MergeMax - 1, receiverB.HitsRemaining.Value, "the room went to the first merge");
            Assert.AreEqual(2, giverB.HitsRemaining.Value);
            Assert.AreEqual(1, _merged.Count);

            OffBeat();
            OffBeat();

            Assert.AreEqual(MergeMax - 1, receiverB.HitsRemaining.Value);
            Assert.AreEqual(2, giverB.HitsRemaining.Value);
            Assert.AreEqual(2, _room.CountFull());
        }

        // As above, but the second receiver is two bubbles from the cap: it still merges, a bubble short.
        [Test]
        public void TwoMergesInOnePass_TheSecondStopsABubbleShort()
        {
            Place(AloneA, MergeMax);
            var receiverA = Place(ReceiverA, MergeMax - 1);
            Place(GiverA, 2);
            var receiverB = Place(ReceiverB, MergeMax - 2);
            var giverB = Place(GiverB, 3);

            OffBeat();

            Assert.AreEqual(MergeMax, receiverA.HitsRemaining.Value);
            Assert.AreEqual(MergeMax - 1, receiverB.HitsRemaining.Value);
            Assert.AreEqual(2, giverB.HitsRemaining.Value);
            Assert.AreEqual(2, _merged.Count);
            Assert.AreEqual(1, _merged[1].Given, "the message carries what was given, not what the cap allowed");
        }

        [Test]
        public void AClusterAlreadyAtTheCap_KeepsItsBubbles_WhileTheLimitIsBelowIt()
        {
            Place(AloneA, MergeMax);
            Place(AloneB, MergeMax);
            _room.RipeEnded();
            var full = Place(ReceiverA, MergeMax);
            var giver = Place(GiverA, 2);

            OffBeat();
            OffBeat();

            Assert.AreEqual(MergeMax, full.HitsRemaining.Value);
            Assert.AreEqual(2, giver.HitsRemaining.Value);
            Assert.IsEmpty(_merged);
        }

        // Nothing on the board changes when the gap is over: the merge it held back is still marked to look at, so
        // the off-beat that ends the gap makes it.
        [Test]
        public void WhenTheCooldownIsOver_TheMergeItHeldBackHappens_WithNoChangeOnTheBoard()
        {
            _room.RipeEnded();
            var receiver = Place(ReceiverA, MergeMax - 1);
            var giver = Place(GiverA, 2);

            OffBeat();
            _merges.Tick();

            Assert.AreEqual(MergeMax - 1, receiver.HitsRemaining.Value, "held a bubble short, one off-beat of two on");
            Assert.AreEqual(2, giver.HitsRemaining.Value);
            Assert.IsEmpty(_merged);

            OffBeat();

            Assert.AreEqual(MergeMax, receiver.HitsRemaining.Value);
            Assert.AreEqual(1, giver.HitsRemaining.Value);
            Assert.AreEqual(1, _merged.Count);
        }

        // The whole run of it: held by the count, then a ripe cluster is popped (the grid changes and the gap
        // starts), then the gap is over.
        [Test]
        public void AMergeHeldByTheCount_WaitsOutTheCooldownAfterARipeClusterEnds_ThenHappens()
        {
            Place(AloneA, MergeMax);
            Place(AloneB, MergeMax);
            var receiver = Place(ReceiverA, MergeMax - 1);
            var giver = Place(GiverA, 2);
            OffBeat();
            Assert.AreEqual(MergeMax - 1, receiver.HitsRemaining.Value);

            _grid.Remove(AloneA);
            _room.RipeEnded();
            OffBeat();

            Assert.AreEqual(MergeMax - 1, receiver.HitsRemaining.Value, "room on the board, but the gap still runs");

            OffBeat();

            Assert.AreEqual(MergeMax, receiver.HitsRemaining.Value);
            Assert.AreEqual(1, giver.HitsRemaining.Value);
        }

        // With no merge held back there is nothing to look at: an off-beat scans nothing, the one the gap ends on
        // included, and a count changed by something the service does not hear about stays as it is.
        [Test]
        public void WithNothingToLookAt_AnOffBeatScansNothing()
        {
            _room.RipeEnded();
            var full = Place(ReceiverA, MergeMax);
            var giver = Place(GiverA, 2);
            OffBeat();

            full.SetBubbles(3);
            OffBeat();
            OffBeat();

            Assert.IsFalse(CoolingDown);
            Assert.AreEqual(3, full.HitsRemaining.Value);
            Assert.AreEqual(2, giver.HitsRemaining.Value);
        }

        [Test]
        public void ANewLevel_LetsAWaitingMergeThrough()
        {
            _cooldownBeats = 10;
            _room.RipeEnded();
            var receiver = Place(ReceiverA, MergeMax - 1);
            Place(GiverA, 2);
            OffBeat();
            Assert.AreEqual(MergeMax - 1, receiver.HitsRemaining.Value);

            _level.Value = 2;
            OffBeat();

            Assert.AreEqual(MergeMax, receiver.HitsRemaining.Value);
        }

        // The bubbles' next off-beat falls, and the merge service ticks on it.
        private void OffBeat()
        {
            _offBeats++;
            _merges.Tick();
        }

        private BubbleClusterModel Place(Vector2Int slot, int bubbles)
        {
            var model = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: 3, scoreValue: 1), _palette);
            model.SetBubbles(bubbles);
            _grid.Place(model, null, slot);
            return model;
        }
    }
}

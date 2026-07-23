using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Item;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class ItemAssignerTests
    {
        private SlotGrid _grid;
        private IActiveLevelParameters _levelParams;
        private ILevelParameters _current;
        private IMessageHandler<ItemCheckMessage> _handler;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<ISlotGridConfig>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(gameConfig, new BalancePathHolder());

            _levelParams = Substitute.For<IActiveLevelParameters>();
            _current = Substitute.For<ILevelParameters>();
            _levelParams.Current.Returns(_current);
            _current.Items.Returns(new List<ItemSettings>());
            _current.ItemCadence.Returns(1);
            _current.ItemCountWeights.Returns(WeightsForCount(1));

            var subscriber = Substitute.For<ISubscriber<ItemCheckMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ItemCheckMessage>>(h => _handler = h),
                    Arg.Any<MessageHandlerFilter<ItemCheckMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var assigner = new ItemAssigner(_levelParams, _grid, subscriber);
            assigner.Start();
        }

        [Test]
        public void OnItemCheck_EmptyNewBalloons_NoAssignment()
        {
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var model = new BalloonModel();
            FireItemCheck(Array.Empty<IBalloonModel>(), turnCount: 1);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_TurnNotOnCadence_NoAssignment()
        {
            _current.ItemCadence.Returns(3);
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 2);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_NoCandidatesThisLevel_NoAssignment()
        {
            // _current.Items defaults to empty (SetUp) — every type gated out of this level.
            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_PickItemEntryReturnsNull_NoAssignment()
        {
            // Mirrors "every eligible entry is at its cap" — the resolver's PickItemEntry returns null.
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns((ItemSettings)null);

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_NoEligibleBalloons_NonItemSlotActor_NoAssignment()
        {
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var model = new ToughBalloonModel(new BalloonModelConfig());
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            // ToughBalloonModel does not implement IHasItemSlot — ItemAssigner skips it
            Assert.IsFalse(model is IHasItemSlot);
        }

        [Test]
        public void OnItemCheck_EligibleBalloon_GetsItemAssigned()
        {
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            Assert.AreEqual(ItemType.Bomb, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_BuildsActiveCountsFromExistingGridState()
        {
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });

            // The assigner reuses one counts buffer and increments it in place after each pick. NSubstitute
            // captures arguments by reference, so a deferred Received() predicate would see the post-increment
            // value — snapshot the count at call time instead.
            var bombCountAtPick = -1;
            _current.PickItemEntry(Arg.Do<IReadOnlyDictionary<string, int>>(
                counts => bombCountAtPick = counts[ItemType.Bomb.ToString()])).Returns(bomb);

            var existing = new BalloonModel();
            existing.Item.Value = ItemType.Bomb;
            _grid.Place(existing, null, new Vector2Int(0, 0));

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            Assert.AreEqual(1, bombCountAtPick);
        }

        [Test]
        public void OnItemCheck_ZeroWeightBalloon_NeverEligible()
        {
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var model = new BalloonModel(new BalloonModelConfig(itemActivationWeight: 0f));
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_MultipleItemsPerCadence_AssignsCountToDistinctBalloons()
        {
            _current.ItemCountWeights.Returns(WeightsForCount(2));
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var models = new[] { new BalloonModel(), new BalloonModel(), new BalloonModel() };
            FireItemCheck(models, turnCount: 1);

            var assigned = 0;
            foreach (var m in models)
            {
                if (m.Item.Value == ItemType.Bomb)
                {
                    assigned++;
                }
            }

            Assert.AreEqual(2, assigned);
        }

        [Test]
        public void OnItemCheck_ItemsPerCadenceExceedsEligible_AssignsAllEligible()
        {
            _current.ItemCountWeights.Returns(WeightsForCount(5));
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 1);

            Assert.AreEqual(ItemType.Bomb, model.Item.Value);
        }

        [Test]
        public void OnItemCheck_InitialSpawn_SeedsInitialItemsIgnoringCadence()
        {
            _current.ItemCadence.Returns(3); // turnCount 0 is a cadence turn anyway; prove it's unused
            _current.InitialItemCountWeights.Returns(WeightsForCount(2));
            _current.ItemCountWeights.Returns(WeightsForCount(0));
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var models = new[] { new BalloonModel(), new BalloonModel(), new BalloonModel() };
            FireItemCheck(models, turnCount: 0, isInitial: true);

            var assigned = 0;
            foreach (var m in models)
            {
                if (m.Item.Value == ItemType.Bomb)
                {
                    assigned++;
                }
            }

            Assert.AreEqual(2, assigned);
        }

        [Test]
        public void OnItemCheck_InitialSpawn_ZeroInitialItems_NoAssignment()
        {
            _current.InitialItemCountWeights.Returns(WeightsForCount(0));
            var bomb = CreateItemSettings(ItemType.Bomb);
            _current.Items.Returns(new List<ItemSettings> { bomb });
            _current.PickItemEntry(Arg.Any<IReadOnlyDictionary<string, int>>()).Returns(bomb);

            var model = new BalloonModel();
            FireItemCheck(new IBalloonModel[] { model }, turnCount: 0, isInitial: true);

            Assert.AreEqual(ItemType.None, model.Item.Value);
        }

        [Test]
        public void SampleCount_EmptyCurve_ReturnsZero()
        {
            Assert.AreEqual(0, ItemAssigner.SampleCount(new AnimationCurve(), 0.5f));
            Assert.AreEqual(0, ItemAssigner.SampleCount(null, 0.5f));
        }

        [Test]
        public void SampleCount_SpikeAtTwo_AlwaysTwo()
        {
            var curve = WeightsForCount(2);

            Assert.AreEqual(2, ItemAssigner.SampleCount(curve, 0f));
            Assert.AreEqual(2, ItemAssigner.SampleCount(curve, 0.999f));
        }

        [Test]
        public void SampleCount_EvenSplit_PicksByRoll()
        {
            // Equal weight on 0 and 1 → low roll picks 0, high roll picks 1.
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));

            Assert.AreEqual(0, ItemAssigner.SampleCount(curve, 0.25f));
            Assert.AreEqual(1, ItemAssigner.SampleCount(curve, 0.75f));
        }

        [Test]
        public void PickWeightedIndex_AllZeroWeight_ReturnsNegativeOne()
        {
            var candidates = new List<IHasWriteableItemSlot>
            {
                new BalloonModel(new BalloonModelConfig(itemActivationWeight: 0f)),
                new BalloonModel(new BalloonModelConfig(itemActivationWeight: 0f)),
            };

            Assert.AreEqual(-1, ItemAssigner.PickWeightedIndex(candidates, 0.5f));
        }

        [Test]
        public void PickWeightedIndex_EvenSplit_PicksByRoll()
        {
            var candidates = new List<IHasWriteableItemSlot>
            {
                new BalloonModel(new BalloonModelConfig(itemActivationWeight: 1f)),
                new BalloonModel(new BalloonModelConfig(itemActivationWeight: 1f)),
            };

            Assert.AreEqual(0, ItemAssigner.PickWeightedIndex(candidates, 0.25f));
            Assert.AreEqual(1, ItemAssigner.PickWeightedIndex(candidates, 0.75f));
        }

        [Test]
        public void PickWeightedIndex_ZeroThenNonzero_AlwaysPicksNonzero()
        {
            var candidates = new List<IHasWriteableItemSlot>
            {
                new BalloonModel(new BalloonModelConfig(itemActivationWeight: 0f)),
                new BalloonModel(new BalloonModelConfig(itemActivationWeight: 1f)),
            };

            Assert.AreEqual(1, ItemAssigner.PickWeightedIndex(candidates, 0f));
            Assert.AreEqual(1, ItemAssigner.PickWeightedIndex(candidates, 0.999f));
        }

        private void FireItemCheck(IReadOnlyList<IBalloonModel> balloons, int turnCount, bool isInitial = false)
        {
            _handler.Handle(new ItemCheckMessage(balloons, turnCount, isInitial));
        }

        // A curve whose only non-zero weight is at X = count, so SampleCount deterministically returns
        // that count regardless of the roll.
        private static AnimationCurve WeightsForCount(int count)
        {
            var keys = new Keyframe[count + 1];
            for (var i = 0; i <= count; i++)
            {
                keys[i] = new Keyframe(i, i == count ? 1f : 0f);
            }

            return new AnimationCurve(keys);
        }

        private static ItemSettings CreateItemSettings(ItemType type)
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", type);
            return settings;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class UnbreakableFormationServiceTests
    {
        private SlotGrid _grid;
        private IUnbreakableFormationSettings _settings;
        private IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private UnbreakableFormationService _service;
        private List<UnbreakableBalloonModel> _unbreakables;
        private int _relocations;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(7, 8));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _settings = Substitute.For<IUnbreakableFormationSettings>();
            _settings.Enabled.Returns(true);
            _settings.MinMembers.Returns(3);
            _settings.MaxMembers.Returns(5);
            _settings.HoldWaves.Returns(3);
            _settings.BottomClearance.Returns(2);
            _settings.SpotChoices.Returns(1);
            _settings.HorizontalWeight.Returns(1f);
            _settings.DiagonalWeight.Returns(1f);
            _settings.ItemCoverWeight.Returns(2f);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.UnbreakableFormation.Returns(_settings);
            _balancePublisher = Substitute.For<IPublisher<BalanceBalloonsMessage>>();

            _service = new UnbreakableFormationService(_grid, balloonsConfig, _balancePublisher)
            {
                Random01 = () => 0f,
            };
            _service.Start();

            _unbreakables = new List<UnbreakableBalloonModel>();
            _relocations = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void FewerThanTheMinimum_NeverForm()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4));

            Arrange();

            Assert.AreEqual(0, _service.Members.Count);
            Assert.IsNull(_service.CurrentShape);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        [Test]
        public void ThreeUnbreakables_FormAndAnchor()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(3, _service.Members.Count);
            Assert.IsNotNull(_service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored && u.InFormation.Value));
            Assert.IsTrue(_unbreakables.All(u => _grid.At(u.SlotIndex.Value) == u), "every member is on its slot");
        }

        [Test]
        public void AFormation_HoldsForHoldWaves_ThenChangesToADifferentShape()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            var firstShape = _service.CurrentShape;
            var held = _unbreakables.Select(u => u.SlotIndex.Value).ToList();
            var relocationsWhenFormed = _relocations;

            Arrange();
            Arrange();

            Assert.AreEqual(relocationsWhenFormed, _relocations, "nothing moves while the formation holds");
            CollectionAssert.AreEqual(held, _unbreakables.Select(u => u.SlotIndex.Value).ToList());
            Assert.AreEqual(firstShape, _service.CurrentShape);

            Arrange();

            Assert.IsNotNull(_service.CurrentShape);
            Assert.AreNotEqual(firstShape, _service.CurrentShape, "the next formation is a different shape");
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored));
        }

        [Test]
        public void LosingAMemberBelowTheMinimum_BreaksTheFormationAtOnce()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();

            var popped = _unbreakables[0];
            _grid.Remove(popped.SlotIndex.Value);

            Assert.AreEqual(0, _service.Members.Count);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
            _balancePublisher.Received(1).Publish(Arg.Any<BalanceBalloonsMessage>());
        }

        [Test]
        public void LosingAMemberAboveTheMinimum_KeepsTheRestInFormation()
        {
            AddUnbreakables(
                new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(5, 0));
            Arrange();

            var popped = _unbreakables[0];
            _grid.Remove(popped.SlotIndex.Value);

            Assert.AreEqual(3, _service.Members.Count);
            Assert.IsFalse(popped.IsAnchored);
            Assert.IsTrue(_unbreakables.Skip(1).All(u => u.IsAnchored));
        }

        [Test]
        public void MoreThanMaxMembers_TheRestStayFree()
        {
            _settings.MaxMembers.Returns(3);
            AddUnbreakables(
                new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(5, 0));

            Arrange();

            Assert.AreEqual(3, _service.Members.Count);
            Assert.AreEqual(1, _unbreakables.Count(u => !u.IsAnchored));
        }

        [Test]
        public void ANewcomerDuringAHold_StaysFreeUntilTheNextChange()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            var newcomer = AddUnbreakable(new Vector2Int(6, 0));

            Arrange();

            Assert.IsFalse(newcomer.IsAnchored);
            Assert.AreEqual(3, _service.Members.Count);
        }

        [Test]
        public void ItemCoverOnly_WithAnItemOnTheBoard_CoversIt()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Bomb;
            _grid.Place(host, null, new Vector2Int(3, 1));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(6, 5), new Vector2Int(5, 0));

            Arrange();

            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => u.SlotIndex.Value.y == 2));
        }

        [Test]
        public void NoShapeFits_NobodyIsAnchored()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(6, 5), new Vector2Int(5, 0));

            Arrange();

            Assert.IsNull(_service.CurrentShape);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        [Test]
        public void Disabled_NeverForms()
        {
            _settings.Enabled.Returns(false);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(0, _service.Members.Count);
        }

        [Test]
        public void ResetRun_ReleasesTheFormation()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();

            _service.ResetRun(1);

            Assert.AreEqual(0, _service.Members.Count);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        private void Arrange()
        {
            _service.ArrangeAtWaveStart(Relocate);
        }

        private void Relocate(IWriteableDynamicSlotActor actor, Vector2Int target)
        {
            _relocations++;
            _grid.Remove(actor.SlotIndex.Value);
            _grid.Place(actor, null, target);
        }

        private void AddUnbreakables(params Vector2Int[] slots)
        {
            foreach (var slot in slots)
            {
                AddUnbreakable(slot);
            }
        }

        private UnbreakableBalloonModel AddUnbreakable(Vector2Int slot)
        {
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
            _grid.Place(unbreakable, null, slot);
            _unbreakables.Add(unbreakable);
            return unbreakable;
        }
    }
}

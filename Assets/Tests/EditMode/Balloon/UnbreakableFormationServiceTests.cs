using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Level;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
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
        private UnbreakableFormationService _service;
        private ProjectileRegistry _projectiles;
        private ILevelParameters _levelParameters;
        private List<UnbreakableBalloonModel> _unbreakables;
        private int _relocations;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(7, 8));
            gridConfig.SlotSeparation.Returns(new Vector2(0.5f, 0.85f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _settings = Substitute.For<IUnbreakableFormationSettings>();
            _settings.Enabled.Returns(true);
            _settings.MinMembers.Returns(3);
            _settings.MaxMembers.Returns(5);
            _settings.ReshapeEveryCycles.Returns(3);
            _settings.BottomClearance.Returns(2);
            _settings.SpotChoices.Returns(1);
            _settings.HorizontalWeight.Returns(1f);
            _settings.DiagonalWeight.Returns(1f);
            _settings.ItemCoverWeight.Returns(2f);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.UnbreakableFormation.Returns(_settings);

            _levelParameters = Substitute.For<ILevelParameters>();
            var level = Substitute.For<IActiveLevelParameters>();
            level.Current.Returns(_levelParameters);
            Allow(FormationShapes.All);

            _projectiles = new ProjectileRegistry();
            _service = new UnbreakableFormationService(_grid, balloonsConfig,
                new ThreatEase(Substitute.For<IPlayerNeed>(), Substitute.For<IAssistSettings>()), _projectiles,
                level)
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
        public void AFormation_HoldsUntilTheNextReshape_ThenChangesToADifferentShape()
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
        public void EveryShape_HoldsUntilTheBoardsNextReshape()
        {
            _settings.ReshapeEveryCycles.Returns(2);
            FormSnake();
            _settings.SnakeWeight.Returns(0f);
            _settings.HorizontalWeight.Returns(1f);

            Arrange();
            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape, "formed on cycle 1, held through cycle 2");

            Arrange();
            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape, "cycle 3 reshapes");

            _settings.HorizontalWeight.Returns(0f);
            _settings.SnakeWeight.Returns(1f);
            Arrange();
            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape, "a line holds as long as the snake did");

            Arrange();
            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape, "cycle 5 reshapes");
        }

        [Test]
        public void ABalloonOnAFormationSlot_GoesToASlotAMemberLeft()
        {
            _settings.DiagonalWeight.Returns(0f);
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    _grid.Place(new BalloonModel(), null, new Vector2Int(col, row));
                }
            }

            var spots = new[] { new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5) };
            foreach (var spot in spots)
            {
                _grid.Remove(spot);
            }

            AddUnbreakables(spots);
            var balloons = AllBalloons();

            Arrange();

            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape, "a full board doesn't keep them apart");
            Assert.IsTrue(_unbreakables.All(u => _grid.At(u.SlotIndex.Value) == u));
            Assert.IsTrue(balloons.All(b => _grid.At(b.SlotIndex.Value) == b), "every balloon has a slot of its own");
            Assert.AreEqual(_grid.Columns * _grid.Rows, balloons.Count + _unbreakables.Count, "none lost");
        }

        [Test]
        public void FreeUnbreakables_WaitForTheBoardsReshape_ToFormUp()
        {
            _settings.MinMembers.Returns(2);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            var standing = _service.Formations[0];
            var shape = standing.Shape;

            var arrivals = new[] { AddUnbreakable(new Vector2Int(1, 4)), AddUnbreakable(new Vector2Int(5, 0)) };
            Arrange();
            Arrange();

            Assert.IsFalse(arrivals.Any(u => u.InFormation.Value), "no forming between reshapes");
            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreEqual(shape, standing.Shape);

            Arrange();

            Assert.IsTrue(arrivals.All(u => u.InFormation.Value && u.IsAnchored), "cycle 4 reshapes and takes them in");
        }

        [Test]
        public void TwoUnbreakables_FormUp_WhenTheMinimumIsTwo()
        {
            _settings.MinMembers.Returns(2);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4));

            Arrange();

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored));
        }

        [Test]
        public void EveryMember_SharesTheFormationsHeading()
        {
            _settings.DiagonalWeight.Returns(0f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape);
            var expected = (Vector2)(_grid.IndexToWorldPosition(new Vector2Int(1, 0))
                - _grid.IndexToWorldPosition(Vector2Int.zero));
            Assert.IsTrue(_unbreakables.All(u => Vector2.Angle(u.FormationHeading, expected) < 0.01f),
                "a horizontal formation runs along its row");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ADiagonalFormation_HeadsDownTheDiagonal(bool right)
        {
            _settings.HorizontalWeight.Returns(0f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            _service.Random01 = () => right ? 0f : 0.99f;

            Arrange();

            var shape = _service.CurrentShape;
            Assert.AreEqual(right ? FormationShape.DiagonalDownRight : FormationShape.DiagonalDownLeft, shape);
            var heading = _unbreakables[0].FormationHeading;
            var downRight = shape == FormationShape.DiagonalDownRight;
            Assert.AreEqual(downRight, heading.x > 0f, "the heading leans the diagonal's way");
            var down = _grid.IndexToWorldPosition(new Vector2Int(0, 1)).y
                < _grid.IndexToWorldPosition(Vector2Int.zero).y;
            Assert.AreEqual(down, heading.y < 0f, "the heading follows the rows downward");
            Assert.IsTrue(_unbreakables.All(u => u.FormationHeading == heading));
        }

        [Test]
        public void AnItemCover_HeadsUpAtTheItem()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Bomb;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(4, 5),
                new Vector2Int(5, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.IsTrue(_unbreakables.All(u => u.FormationHeading == Vector2.up));
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
        public void SixUnbreakables_FormTwoFormations_OnSeparateSlots()
        {
            _settings.MaxMembers.Returns(3);
            AddSixSpreadOut();

            Arrange();

            Assert.AreEqual(2, _service.Formations.Count);
            Assert.IsTrue(_service.Formations.All(f => f.Members.Count == 3));
            Assert.IsEmpty(_service.Formations[0].Members.Intersect(_service.Formations[1].Members));
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored && _grid.At(u.SlotIndex.Value) == u));
            Assert.AreEqual(6, _unbreakables.Select(u => u.SlotIndex.Value).Distinct().Count());
        }

        // The formation glow joins only the rings of one formation, and tells them by this.
        [Test]
        public void MembersOfOneFormation_ShareItsId_AndTwoFormationsDiffer()
        {
            _settings.MaxMembers.Returns(3);
            AddSixSpreadOut();

            Arrange();

            var first = _service.Formations[0];
            var second = _service.Formations[1];
            Assert.AreNotEqual(0, first.Id);
            Assert.AreNotEqual(first.Id, second.Id);
            Assert.IsTrue(first.Members.All(m => m.FormationId == first.Id));
            Assert.IsTrue(second.Members.All(m => m.FormationId == second.Id));
        }

        [Test]
        public void FreeUnbreakablesPastAFullFormation_FormTheirOwn()
        {
            AddSixSpreadOut();
            AddUnbreakables(new Vector2Int(2, 2), new Vector2Int(4, 2));

            Arrange();

            CollectionAssert.AreEquivalent(new[] { 5, 3 }, _service.Formations.Select(f => f.Members.Count));
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored));
        }

        [Test]
        public void TooFewFreeUnbreakablesForASecondFormation_StayFree()
        {
            AddSixSpreadOut();

            Arrange();

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreEqual(1, _unbreakables.Count(u => !u.IsAnchored));
        }

        [Test]
        public void AReshape_TakesInAFreeUnbreakable_AndItStaysOutOfAnotherFormation()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            var newcomer = AddUnbreakable(new Vector2Int(6, 0));
            Arrange();
            Arrange();

            Arrange();

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreEqual(4, _service.Formations[0].Members.Count);
            Assert.IsTrue(newcomer.IsAnchored);

            AddUnbreakables(new Vector2Int(1, 5), new Vector2Int(5, 5));
            for (var i = 0; i < 3; i++)
            {
                Arrange();
            }

            Assert.IsTrue(_service.Formations.All(f => f.Members.Count >= 3));
            Assert.AreEqual(_service.Members.Count, _service.Members.Distinct().Count(), "no member in two formations");
        }

        [Test]
        public void PiercingOneFormationBelowTheMinimum_LeavesTheOtherStanding()
        {
            _settings.MaxMembers.Returns(3);
            AddSixSpreadOut();
            Arrange();
            var broken = _service.Formations[0];
            var standing = _service.Formations[1];
            var survivors = broken.Members.Skip(1).Cast<UnbreakableBalloonModel>().ToList();

            _grid.Remove(broken.Members[0].SlotIndex.Value);

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreSame(standing, _service.Formations[0]);
            Assert.IsTrue(standing.Members.Cast<UnbreakableBalloonModel>().All(m => m.IsAnchored));
            Assert.IsFalse(survivors.Any(m => m.IsAnchored));
            Assert.AreEqual(3, _service.Members.Count);
        }

        [Test]
        public void AnUntouchedFormation_InFullNeed_CallsOnTheEasedChance()
        {
            var need = Substitute.For<IPlayerNeed>();
            need.Need.Returns(1f);
            var assist = Substitute.For<IAssistSettings>();
            assist.ThreatEaseAtFullNeed.Returns(0.5f);
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.UnbreakableFormation.Returns(_settings);
            _service = new UnbreakableFormationService(_grid, balloonsConfig, new ThreatEase(need, assist))
            {
                Random01 = () => 0.3f,
            };
            _settings.ReinforcementChance.Returns(0.5f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();
            WaveStart();
            WaveStart();

            Assert.AreEqual(0, _service.TakeReinforcementCalls(), "0.5 eased to 0.25 at the last heart: 0.3 misses it");
        }

        [Test]
        public void AnUntouchedFormation_CallsAReinforcementAtTheNextWaveStart()
        {
            _settings.ReinforcementChance.Returns(0.5f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();
            WaveStart();

            Assert.AreEqual(0, _service.TakeReinforcementCalls(), "the wave it formed in calls nobody");

            WaveStart();

            Assert.AreEqual(1, _service.TakeReinforcementCalls());
            Assert.AreEqual(0, _service.TakeReinforcementCalls(), "a take clears the calls");
        }

        [Test]
        public void EveryUntouchedFormation_RollsOnItsOwn()
        {
            _settings.MaxMembers.Returns(3);
            _settings.ReinforcementChance.Returns(1f);
            AddSixSpreadOut();
            Arrange();
            WaveStart();

            WaveStart();

            Assert.AreEqual(2, _service.TakeReinforcementCalls());
        }

        [Test]
        public void ARollAtOrAboveTheChance_CallsNobody()
        {
            _settings.ReinforcementChance.Returns(0.5f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            WaveStart();
            _service.Random01 = () => 0.5f;

            WaveStart();

            Assert.AreEqual(0, _service.TakeReinforcementCalls());
        }

        [Test]
        public void APiercedFormation_CallsNoMoreReinforcements()
        {
            _settings.ReinforcementChance.Returns(1f);
            AddUnbreakables(
                new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(5, 0));
            Arrange();

            _grid.Remove(_unbreakables[0].SlotIndex.Value);
            WaveStart();
            WaveStart();
            WaveStart();

            Assert.AreEqual(1, _service.Formations.Count, "three members still stand");
            Assert.AreEqual(0, _service.TakeReinforcementCalls());
        }

        [Test]
        public void ResetRun_DropsUntakenCalls()
        {
            _settings.ReinforcementChance.Returns(1f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            WaveStart();
            WaveStart();

            _service.ResetRun(1);

            Assert.AreEqual(0, _service.TakeReinforcementCalls());
        }

        [Test]
        public void ItemCoverOnly_WithAnItemOnTheBoard_CoversIt()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Bomb;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(4, 5),
                new Vector2Int(5, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => HexCoordinates.Distance(u.SlotIndex.Value, new Vector2Int(3, 2)) == 1),
                "all six around the item");
        }

        [Test]
        public void ItemCoverOnly_WithThreeUnbreakables_CoversTheItemFromBelow()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var item = new Vector2Int(3, 2);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Snipe;
            _grid.Place(host, null, item);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored));
            Assert.IsTrue(_unbreakables.All(u => HexCoordinates.Distance(u.SlotIndex.Value, item) == 1));
            var slots = _unbreakables.Select(u => u.SlotIndex.Value).ToList();
            CollectionAssert.Contains(slots, HexCoordinates.Step(item, 1));
            CollectionAssert.Contains(slots, HexCoordinates.Step(item, 2));
        }

        [Test]
        public void ItemCoverOnly_WithAnItemItDoesNotShield_FormsNothing()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            var host = new BalloonModel();
            host.Item.Value = ItemType.Laser;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.IsNull(_service.CurrentShape);
            Assert.IsFalse(_unbreakables.Any(u => u.IsAnchored));
        }

        [Test]
        public void ItemCoverWeighted_WithAnItemItDoesNotShield_PicksAnotherShape()
        {
            var host = new BalloonModel();
            host.Item.Value = ItemType.Laser;
            _grid.Place(host, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.IsNotNull(_service.CurrentShape);
            Assert.AreNotEqual(FormationShape.ItemCover, _service.CurrentShape);
        }

        [Test]
        public void AnItemCover_RecordsTheBalloonItStandsRound_WithItsFormationAndItsSlotsCentre()
        {
            CoverOnly();
            var slot = new Vector2Int(3, 2);
            var host = PlaceHost(slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.AreEqual(1, _service.CoveredHosts.Count);
            var covered = _service.CoveredHosts[0];
            Assert.AreSame(host, covered.Actor);
            Assert.AreEqual(_service.Formations[0].Id, covered.FormationId);
            Assert.AreEqual(_unbreakables[0].FormationId, covered.FormationId, "the id its members carry");
            Assert.AreEqual((Vector2)_grid.IndexToWorldPosition(slot), covered.Centre);
        }

        [Test]
        public void ItemCoverOnly_WithAGiftBalloonOnTheBoard_CoversIt_AndItIsTheHost()
        {
            CoverOnly();
            var slot = new Vector2Int(3, 2);
            var gift = new GoldenBalloonModel(new BalloonModelConfig(BalloonType.Golden, scoreValue: 0));
            _grid.Place(gift, null, slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Arrange();

            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => HexCoordinates.Distance(u.SlotIndex.Value, slot) == 1));
            Assert.AreEqual(1, _service.CoveredHosts.Count);
            Assert.AreSame(gift, _service.CoveredHosts[0].Actor);
        }

        // The meadow's rule stands: its unbreakables form the snake alone, a gift on the board or not.
        [Test]
        public void OnASnakeOnlyLevel_AGiftBalloonIsNotCovered()
        {
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            _settings.GiftCoverPreference.Returns(2f);
            Allow(FormationShapes.Snake);
            _grid.Place(new HeartBalloonModel(new BalloonModelConfig(BalloonType.Heart, scoreValue: 0)), null,
                new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape);
            Assert.AreEqual(0, _service.CoveredHosts.Count);
        }

        [Test]
        public void AFormationOfAnotherShape_CoversNoBalloon()
        {
            _settings.ItemCoverWeight.Returns(0f);
            PlaceHost(new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.IsNotNull(_service.CurrentShape);
            Assert.AreEqual(0, _service.CoveredHosts.Count);
        }

        [Test]
        public void ACover_HoldsItsBalloonThroughItsHold_AndFreesItWhenItChangesShape()
        {
            CoverOnly();
            var host = PlaceHost(new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();
            Assert.IsTrue(host.IsAnchored, "held from the cycle the cover is planned");
            _settings.HorizontalWeight.Returns(1f);

            Arrange();
            Arrange();
            Assert.AreEqual(1, _service.CoveredHosts.Count, "still covered while the cover holds");
            Assert.IsTrue(host.IsAnchored);

            Arrange();

            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape);
            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.IsFalse(host.IsAnchored, "a line holds no balloon");
        }

        // The balance runs right after the arrangement, while the members are still on their way: a balloon held
        // only once they had landed would have floated up out of an open cover by then.
        [Test]
        public void TheCoveredBalloon_IsHeldBeforeAnyMemberSetsOff()
        {
            CoverOnly();
            var host = PlaceHost(new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            var heldWhenTheMembersMoved = false;

            _service.ArrangeOnCycle(moves =>
            {
                heldWhenTheMembersMoved = host.IsAnchored;
                Relocate(moves);
            });

            Assert.AreEqual(3, _relocations, "the three had to travel");
            Assert.IsTrue(heldWhenTheMembersMoved);
        }

        // The picture the owner asked for: the members stand in the slots right beside the balloon and it stays
        // between them. Three leave both slots above it open, which is where the balance would send it.
        [Test]
        public void AThreeMemberCover_KeepsItsBalloonBetweenItsMembers_ThroughABalancePass()
        {
            CoverOnly();
            var slot = new Vector2Int(3, 2);
            var host = PlaceHost(slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();
            var query = new GridBalanceQuery(_grid);
            Assert.IsTrue(query.IsUnbalanced(slot.x, slot.y), "it has room above to rise into");
            var moves = new List<BalanceMove>();

            new BalancePlanner(_grid, query).Plan(new Dictionary<IWriteableDynamicSlotActor, int>(), moves);

            Assert.AreEqual(0, moves.Count);
            Assert.AreSame(host, _grid.At(slot));
            Assert.IsTrue(_unbreakables.All(u => HexCoordinates.Distance(u.SlotIndex.Value, slot) == 1),
                "all three touch it");
            Assert.AreEqual(1, _service.CoveredHosts.Count);
        }

        // With no other shape to change to, the cover is planned again where it stands: the same balloon, the same
        // formation, so the ring a view keeps over it carries on, and it is held again at once.
        [Test]
        public void ACoverPlannedAgainRoundTheSameBalloon_KeepsIt_AndKeepsHoldingIt()
        {
            CoverOnly();
            var host = PlaceHost(new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();
            var formation = _service.CoveredHosts[0].FormationId;

            Arrange();
            Arrange();
            Arrange();

            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
            Assert.AreEqual(1, _service.CoveredHosts.Count);
            Assert.AreSame(host, _service.CoveredHosts[0].Actor);
            Assert.AreEqual(formation, _service.CoveredHosts[0].FormationId);
            Assert.IsTrue(host.IsAnchored);
        }

        // The hold runs out and nothing can be planned next (the level in play allows no shape): the balloon is
        // let go before the plan is tried, and the formation is set loose after it failed. The list the glow reads
        // must not keep the balloon between the two, or its ring stays over a balloon nothing covers.
        [Test]
        public void ACoverWhoseHoldRunsOutWithNoShapeToChangeTo_ListsNoBalloon_AndLeavesNoRing()
        {
            CoverOnly();
            var slot = new Vector2Int(3, 2);
            var host = PlaceHost(slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();
            var sources = new FormationGlowSources();
            var rings = new FormationGlowHosts();
            rings.Sync(_service.CoveredHosts, sources);
            Assert.AreEqual(1, sources.All.Count, "the covered balloon has its ring source");
            Allow(FormationShapes.None);

            Arrange();
            Arrange();
            Arrange();
            rings.Sync(_service.CoveredHosts, sources);

            Assert.IsNull(_service.CurrentShape, "the cover is set loose");
            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.AreEqual(0, sources.All.Count, "no ring source is left over the balloon");
            Assert.AreSame(host, _grid.At(slot));
            Assert.IsFalse(host.IsAnchored);
        }

        // The balloon outlives its cover here: it is still on the board, and must be free to move again.
        [Test]
        public void ACoverBreaking_FreesItsBalloon()
        {
            CoverOnly();
            var slot = new Vector2Int(3, 2);
            var host = PlaceHost(slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();

            _grid.Remove(_unbreakables[0].SlotIndex.Value);

            Assert.IsNull(_service.CurrentShape, "below the minimum the cover breaks");
            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.AreSame(host, _grid.At(slot));
            Assert.IsFalse(host.IsAnchored);
        }

        [Test]
        public void ACoverLosingAMember_ButStillStanding_KeepsHoldingItsBalloon()
        {
            CoverOnly();
            var host = PlaceHost(new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(4, 5),
                new Vector2Int(5, 5), new Vector2Int(6, 5));
            Arrange();

            _grid.Remove(_unbreakables[0].SlotIndex.Value);

            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
            Assert.AreEqual(1, _service.CoveredHosts.Count);
            Assert.AreSame(host, _service.CoveredHosts[0].Actor);
            Assert.IsTrue(host.IsAnchored);
        }

        [Test]
        public void TheCoveredBalloonPopped_IsLetGo_AndTheCoverStandsOn()
        {
            CoverOnly();
            var slot = new Vector2Int(3, 2);
            var host = PlaceHost(slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();

            _grid.Remove(slot);

            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.IsFalse(host.HeldByCover);
            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape, "the cover stays until its next change");
            Assert.IsTrue(_unbreakables.All(u => u.IsAnchored));
        }

        // Nothing that balances the board moves a held balloon, but something may still take it off its slot on
        // purpose. It is then out of its cover and free, and whatever lands on the slot is not what was covered.
        [Test]
        public void TheCoveredBalloonTakenOffItsSlot_IsLetGoAndFree_AndTheBalloonTakingItsPlaceIsNotCovered()
        {
            CoverOnly();
            var slot = new Vector2Int(3, 2);
            var host = PlaceHost(slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();
            var above = HexCoordinates.Step(slot, 5);
            Assert.IsTrue(_grid.IsEmpty(above.x, above.y), "three leave the slots above open");

            _grid.Remove(slot);
            _grid.Place(host, null, above);
            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.IsFalse(host.IsAnchored);

            var newcomer = new BalloonModel();
            _grid.Place(newcomer, null, slot);
            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.IsFalse(newcomer.IsAnchored);
            Assert.AreEqual(FormationShape.ItemCover, _service.CurrentShape);
        }

        // The level's ascent, a nuke's blast and a run's clearing take every balloon off the board, held or not,
        // in whatever order they come to them.
        [TestCase(true)]
        [TestCase(false)]
        public void TheBoardCleared_LeavesNoBalloonHeld_WhicheverGoesFirst(bool theCoveredBalloonFirst)
        {
            CoverOnly();
            var slot = new Vector2Int(3, 2);
            var host = PlaceHost(slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();
            var members = _unbreakables.Select(u => u.SlotIndex.Value).ToList();

            if (theCoveredBalloonFirst)
            {
                _grid.Remove(slot);
            }

            foreach (var member in members)
            {
                _grid.Remove(member);
            }

            if (!theCoveredBalloonFirst)
            {
                _grid.Remove(slot);
            }

            Assert.IsFalse(host.HeldByCover);
            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.AreEqual(0, _service.Formations.Count);
        }

        // The snake would trade places with any other balloon ahead of it. The covered one is held, so the snake
        // turns as it does at a formation's member: right is the cover's own member, left is open.
        [Test]
        public void ASnake_TurnsAsideFromTheCoveredBalloon_RatherThanWalkThroughIt()
        {
            // Both form on the run's first reshape: the snake from row 2 first (a roll of 0.9 picks it over the
            // cover), then the three left over cover the bomb ahead of its head.
            SnakeOnly();
            _settings.SnakeMaxMembers.Returns(3);
            AddUnbreakables(new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2));
            var head = new Vector2Int(2, 2);
            var slot = HexCoordinates.Step(head, 0);
            var host = PlaceHost(slot);
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            var rolls = new Queue<float>(new[] { 0.9f });
            _service.Random01 = () => rolls.Count > 0 ? rolls.Dequeue() : 0f;
            Arrange();
            var snake = _service.Formations.Single(f => f.Shape == FormationShape.Snake);
            Assert.AreEqual(head, snake.Members[0].SlotIndex.Value, "control: heading east from row 2");
            Assert.AreEqual(1, _service.CoveredHosts.Count, "the free three cover the bomb ahead of the snake");

            _service.StepSnakes(Relocate);

            Assert.AreSame(host, _grid.At(slot), "the covered balloon stays where it is");
            Assert.AreEqual(1, _service.CoveredHosts.Count);
            Assert.AreEqual(5, snake.Heading, "east is held and south-east is the cover's member: it turns north-east");
            Assert.AreEqual(HexCoordinates.Step(head, 5), snake.Members[0].SlotIndex.Value);
        }

        // Every move the service makes through another balloon asks this one question: a snake's step, a joiner's
        // line on from a tail, a gap closing, the toughs falling in, a formation's slot.
        [Test]
        public void ABalloonACoverHolds_IsNoBalloonToTradePlacesWith_UntilItIsLetGo()
        {
            var balloon = new BalloonModel();
            Assert.IsTrue(SnakeWalk.CanPassThrough(balloon), "control: a free balloon trades places");

            balloon.SetHeldByCover(true);
            Assert.IsFalse(SnakeWalk.CanPassThrough(balloon));

            balloon.SetHeldByCover(false);
            Assert.IsTrue(SnakeWalk.CanPassThrough(balloon));
        }

        [Test]
        public void ResetRun_FreesTheCoveredBalloon()
        {
            CoverOnly();
            var host = PlaceHost(new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();

            _service.ResetRun(1);

            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.IsFalse(host.IsAnchored);
        }

        [Test]
        public void FormationsSwitchedOff_FreeTheCoveredBalloon()
        {
            CoverOnly();
            var host = PlaceHost(new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));
            Arrange();
            _settings.Enabled.Returns(false);

            Arrange();

            Assert.AreEqual(0, _service.CoveredHosts.Count);
            Assert.IsFalse(host.IsAnchored);
        }

        // Covered as a gift is, a nuke is held before anyone has armed it.
        [Test]
        public void ACoveredNuke_IsHeldUnarmed_AndFreeAgainWhenItsCoverBreaks()
        {
            CoverOnly();
            var nuke = new NukeBalloonModel(new BalloonModelConfig(BalloonType.Nuke, scoreValue: 0, hitsToPop: 3));
            _grid.Place(nuke, null, new Vector2Int(3, 2));
            AddUnbreakables(new Vector2Int(0, 5), new Vector2Int(3, 5), new Vector2Int(6, 5));

            Arrange();
            Assert.IsFalse(nuke.Armed.Value);
            Assert.IsTrue(nuke.IsAnchored);

            _grid.Remove(_unbreakables[0].SlotIndex.Value);
            Assert.IsFalse(nuke.IsAnchored);
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

        private void AddSixSpreadOut()
        {
            AddUnbreakables(
                new Vector2Int(0, 0), new Vector2Int(6, 0), new Vector2Int(3, 1),
                new Vector2Int(0, 5), new Vector2Int(6, 5), new Vector2Int(3, 4));
        }

        [Test]
        public void ASnake_FormsAsALine_LedFromOneEnd()
        {
            SnakeOnly();
            AddUnbreakables(new Vector2Int(1, 0), new Vector2Int(3, 0), new Vector2Int(5, 0));

            Arrange();

            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape);
            var snake = _service.Formations[0];
            Assert.AreEqual(0, snake.Heading, "Random01 0 sends it east");
            var columns = snake.Members.Select(m => m.SlotIndex.Value.x).ToList();
            CollectionAssert.AreEqual(columns.OrderByDescending(x => x).ToList(), columns, "led by its eastmost");
        }

        [Test]
        public void ASnakeStep_MovesTheHeadOn_AndEachMemberIntoTheSlotAhead()
        {
            var snake = FormSnake();
            var before = snake.Members.Select(m => m.SlotIndex.Value).ToList();

            _service.StepSnakes(Relocate);

            var after = snake.Members.Select(m => m.SlotIndex.Value).ToList();
            Assert.AreEqual(HexCoordinates.Step(before[0], 0), after[0], "the head steps east");
            for (var i = 1; i < after.Count; i++)
            {
                Assert.AreEqual(before[i - 1], after[i], "each member takes the slot the one ahead left");
            }

            Assert.IsTrue(snake.Members.All(m => m.Walking && m.InFormation.Value && m is IBalanceAnchor { IsAnchored: true }));
        }

        [Test]
        public void ASnake_AtTheEdge_TurnsTheGentlestWayOpen()
        {
            var snake = FormSnake();
            for (var i = 0; i < 6 && snake.Members[0].SlotIndex.Value.x < _grid.Columns - 1; i++)
            {
                _service.StepSnakes(Relocate);
            }

            var head = snake.Members[0].SlotIndex.Value;
            _service.StepSnakes(Relocate);

            Assert.AreEqual(1, snake.Heading, "east is off the board: it turns right, south-east");
            Assert.AreEqual(HexCoordinates.Step(head, 1), snake.Members[0].SlotIndex.Value);
        }

        [Test]
        public void ASnake_WalksThroughABalloonInItsWay_WhichGoesRoundToTheTail()
        {
            // Every tick is a step: a balloon ahead trades places with the head rather than stop the snake.
            var snake = FormSnake();
            var ahead = HexCoordinates.Step(snake.Members[0].SlotIndex.Value, 0);
            var tailWas = snake.Members[snake.Members.Count - 1].SlotIndex.Value;
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, ahead);

            _service.StepSnakes(Relocate);

            Assert.AreEqual(ahead, snake.Members[0].SlotIndex.Value, "the head steps on east, through the balloon");
            Assert.AreEqual(tailWas, balloon.SlotIndex.Value, "which takes the slot the tail left");
            Assert.AreSame(balloon, _grid.At(tailWas));
        }

        [Test]
        public void ASnakePlowedInTheMiddle_SplitsThere_TheMembersAheadWalkOn()
        {
            var snake = FormSnake();
            var members = snake.Members.ToList();
            var before = members.Select(m => m.SlotIndex.Value).ToList();
            Plow(members[1]);

            _service.StepSnakes(Relocate);

            Assert.AreEqual(HexCoordinates.Step(before[0], 0), members[0].SlotIndex.Value, "the head walks on");
            Assert.AreEqual(before[1], members[1].SlotIndex.Value, "the plowed one stays where it was struck");
            Assert.AreEqual(before[2], members[2].SlotIndex.Value, "and the one behind it waits for the discharge");
        }

        [Test]
        public void ASnakeWithItsHeadPlowed_WalksOnTailFirst()
        {
            var snake = FormSnake();
            var members = snake.Members.ToList();
            var before = members.Select(m => m.SlotIndex.Value).ToList();
            Plow(members[0]);

            _service.StepSnakes(Relocate);

            Assert.AreSame(members[2], snake.Members[0], "the tail leads");
            Assert.AreEqual(1, HexCoordinates.Distance(before[2], members[2].SlotIndex.Value), "and took a step");
            Assert.AreEqual(before[2], members[1].SlotIndex.Value, "the middle one follows it");
            Assert.AreEqual(before[0], members[0].SlotIndex.Value, "the plowed head stays");
        }

        [Test]
        public void ASnakeDischarged_WalksWhole_Again()
        {
            var snake = FormSnake();
            var members = snake.Members.ToList();
            var shot = Plow(members[1]);
            _service.StepSnakes(Relocate);
            shot.Flight.PendingPierceHits.Clear();
            var before = members.Select(m => m.SlotIndex.Value).ToList();

            _service.StepSnakes(Relocate);

            Assert.AreEqual(before[0], members[1].SlotIndex.Value, "the one that held takes the slot ahead");
            Assert.AreEqual(before[1], members[2].SlotIndex.Value);
        }

        [Test]
        public void AFormationWithAPlowedMember_WaitsForTheDischarge_ToChangeShape()
        {
            _settings.ReshapeEveryCycles.Returns(1);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            var formation = _service.Formations[0];
            var shape = formation.Shape;
            var slots = _unbreakables.Select(u => u.SlotIndex.Value).ToList();
            var shot = Plow(_unbreakables[0]);

            Arrange();

            Assert.AreEqual(shape, formation.Shape);
            CollectionAssert.AreEqual(slots, _unbreakables.Select(u => u.SlotIndex.Value).ToList(), "nothing moved");

            shot.Flight.PendingPierceHits.Clear();
            Arrange();

            Assert.AreNotEqual(shape, _service.CurrentShape, "it changes on the cycle after the discharge");
        }

        [Test]
        public void NoMoreUnbreakableSnakes_ThanTheCap_TheThirdPicksAnotherShape()
        {
            // The roll at the top of the range picks the last shape in the pool, the snake, while it's in it.
            _service.Random01 = () => 0.999f;
            _settings.MinMembers.Returns(2);
            _settings.MaxUnbreakableSnakes.Returns(2);
            _settings.SnakeWeight.Returns(100f);
            _settings.SnakeMaxMembers.Returns(2);
            _settings.HorizontalWeight.Returns(0f);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(5, 0), new Vector2Int(6, 0),
                new Vector2Int(0, 5), new Vector2Int(1, 5));

            Arrange();

            Assert.AreEqual(2, _service.Formations.Count(f => f.Shape == FormationShape.Snake));
            Assert.AreEqual(3, _service.Formations.Count, "the third formed all the same, as another shape");
        }

        [Test]
        public void AFormationChangingShape_SendsItsMembersToASnakesTail_OnTheJoinRoll()
        {
            _settings.MinMembers.Returns(2);
            _settings.HorizontalMaxMembers.Returns(2);
            _settings.DiagonalWeight.Returns(0f);
            _settings.ReshapeEveryCycles.Returns(1);
            AddUnbreakables(new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(4, 5), new Vector2Int(6, 5));
            Arrange();
            Assert.AreEqual(2, _service.Formations.Count, "control: two lines of two");
            var first = _service.Formations[0];

            // On the next reshape the first line turns into a snake, and the second joins its tail.
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            _settings.BeatSeconds.Returns(1.7f);
            _settings.JoinSnakeChance.Returns(1f);
            Arrange();

            Assert.AreEqual(FormationShape.Snake, first.Shape);
            var snake = first;
            Assert.AreEqual(1, _service.Formations.Count, "the line left for the snake");
            Assert.AreEqual(4, snake.Members.Count);
            for (var i = 1; i < snake.Members.Count; i++)
            {
                Assert.AreEqual(1, HexCoordinates.Distance(snake.Members[i - 1].SlotIndex.Value,
                    snake.Members[i].SlotIndex.Value), "they fall in on from its tail, one line");
            }

            Assert.IsTrue(_unbreakables.All(u => _grid.At(u.SlotIndex.Value) == u));
        }

        [Test]
        public void ASnake_BoxedIn_TurnsRoundAndItsTailLeads()
        {
            // Toughs, which a snake never trades places with, box the head in.
            var snake = FormSnake();
            var head = snake.Members[0];
            var tail = snake.Members[snake.Members.Count - 1];
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = HexCoordinates.Step(head.SlotIndex.Value, direction);
                if (_grid.InBounds(slot) && _grid.IsEmpty(slot.x, slot.y))
                {
                    _grid.Place(new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: 1)), null,
                        slot);
                }
            }

            var tailWas = tail.SlotIndex.Value;
            _service.StepSnakes(Relocate);

            Assert.AreSame(tail, snake.Members[0], "the tail leads now");
            Assert.AreEqual(1, HexCoordinates.Distance(tailWas, tail.SlotIndex.Value), "and took a step");
        }

        [Test]
        public void ASnake_NeverStepsWhereItWouldWallTheBoard()
        {
            // The snake holds row 2's west end. East of it stand three roamers, and one more sits above its tail:
            // stepping east, the head would link them all from wall to wall, so it turns south-east instead.
            var snake = FormSnake();
            Outsider(new Vector2Int(0, 1));
            Outsider(new Vector2Int(4, 2));
            Outsider(new Vector2Int(5, 2));
            Outsider(new Vector2Int(6, 2));
            var head = snake.Members[0].SlotIndex.Value;

            _service.StepSnakes(Relocate);

            Assert.AreEqual(1, snake.Heading);
            Assert.AreEqual(HexCoordinates.Step(head, 1), snake.Members[0].SlotIndex.Value);
        }

        [Test]
        public void DropWallClosingSlots_DropsTheSlotsThatWouldWallTheBoard()
        {
            for (var col = 0; col < 6; col++)
            {
                Outsider(new Vector2Int(col, 3));
            }

            var roamer = Outsider(new Vector2Int(2, 6));
            var slots = new List<Vector2Int> { new(6, 3), new(6, 0), new(5, 4) };

            _service.DropWallClosingSlots(roamer.SlotIndex.Value, slots);

            CollectionAssert.AreEqual(new[] { new Vector2Int(6, 0), new Vector2Int(5, 4) }, slots);
        }

        [Test]
        public void DropWallClosingSlots_OnABoardWalledAlready_DropsNothing()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                Outsider(new Vector2Int(col, 3));
            }

            var roamer = Outsider(new Vector2Int(2, 6));
            var slots = new List<Vector2Int> { new(6, 4), new(0, 0) };

            _service.DropWallClosingSlots(roamer.SlotIndex.Value, slots);

            Assert.AreEqual(2, slots.Count);
        }

        [Test]
        public void ASnakeMemberPopped_TheOneBehindClosesTheGap()
        {
            _settings.MinMembers.Returns(3);
            _settings.SnakeMaxMembers.Returns(4);
            var snake = FormSnake(4);
            var middle = snake.Members[1];
            var gap = middle.SlotIndex.Value;
            _grid.Remove(gap);
            var headWas = snake.Members[0].SlotIndex.Value;

            _service.StepSnakes(Relocate);

            Assert.AreEqual(3, snake.Members.Count);
            Assert.AreEqual(headWas, snake.Members[1].SlotIndex.Value, "the member behind the gap follows the head");
        }

        [Test]
        public void AnUnbreakableSnake_WalksOnTheBeat()
        {
            _settings.BeatSeconds.Returns(1.7f);
            Assert.IsFalse(_service.HasUnbreakableSnake, "no snake, no clock");

            FormSnake();

            Assert.IsTrue(_service.HasUnbreakableSnake);
            Assert.IsFalse(_service.HasToughSnake);
            Assert.AreEqual(1.7f, _service.BeatSeconds);
        }

        [Test]
        public void ASnakeMember_LeavingItsFormation_StopsWalking()
        {
            var snake = FormSnake();
            _service.StepSnakes(Relocate);
            var member = (UnbreakableBalloonModel)snake.Members[0];

            _service.ResetRun(1);

            Assert.IsFalse(member.Walking);
        }

        [Test]
        public void ASnakeJustArranged_WaitsForTheNextBeat_InTheBalanceThatFormedIt()
        {
            var snake = FormSnake();
            var formed = snake.Members.Select(m => m.SlotIndex.Value).ToList();

            _service.StepSnakes(Relocate, afterArranging: true);

            CollectionAssert.AreEqual(formed, snake.Members.Select(m => m.SlotIndex.Value).ToList());

            _service.StepSnakes(Relocate);

            CollectionAssert.AreNotEqual(formed, snake.Members.Select(m => m.SlotIndex.Value).ToList());
        }

        [Test]
        public void ASnake_ChangingToAnotherShape_StopsWalking()
        {
            var snake = FormSnake();
            _service.StepSnakes(Relocate);
            _settings.SnakeWeight.Returns(0f);
            _settings.HorizontalWeight.Returns(1f);
            for (var wave = 0; wave < 3; wave++)
            {
                Arrange();
            }

            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => u.InFormation.Value && !u.Walking));
        }

        // Horizontal and snake only, weight 1 each, and a roll of 0.4. Three unbreakables: 0.4 of a total of 2 lands
        // on the horizontal. Five, with the snake's weight up half for each of the two past the minimum: the snake
        // weighs 2, and 0.4 of a total of 3 lands past the horizontal's 1, on the snake.
        [TestCase(0.5f, 3, false)]
        [TestCase(0.5f, 5, true)]
        [TestCase(0f, 5, false)]
        public void TheSnakesWeight_GrowsWithTheUnbreakablesOnTheBoard(
            float perExtra, int unbreakables, bool snake)
        {
            _settings.DiagonalWeight.Returns(0f);
            _settings.ItemCoverWeight.Returns(0f);
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            _settings.SnakeWeightPerExtraUnbreakable.Returns(perExtra);
            _service.Random01 = () => 0.4f;
            var spots = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(1, 1),
                new Vector2Int(5, 3),
            };
            AddUnbreakables(spots.Take(unbreakables).ToArray());

            Arrange();

            Assert.AreEqual(snake ? FormationShape.Snake : FormationShape.Horizontal, _service.CurrentShape);
        }

        [TestCase(0f)]
        [TestCase(0.3f)]
        [TestCase(0.6f)]
        [TestCase(0.999f)]
        public void ALevelAllowingOnlyTheSnake_FormsOnlySnakes_WhateverTheRoll(float roll)
        {
            // Every shape weighs something, so only the level keeps the others out.
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            Allow(FormationShapes.Snake);
            _service.Random01 = () => roll;
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape);
            Assert.IsTrue(_unbreakables.All(u => u.InFormation.Value));
        }

        // With every line weighing 1 (and the cover 2, which has no item to cover here), a roll of 0 lands on the
        // horizontal, 0.2 on the first diagonal, 0.4 on the second and the top of the range on the snake.
        [TestCase(0f, nameof(FormationShape.Horizontal))]
        [TestCase(0.2f, nameof(FormationShape.DiagonalDownRight))]
        [TestCase(0.4f, nameof(FormationShape.DiagonalDownLeft))]
        [TestCase(0.999f, nameof(FormationShape.Snake))]
        public void ALevelAllowingEveryShape_PicksAmongThemAll(float roll, string expected)
        {
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            Allow(FormationShapes.All);
            _service.Random01 = () => roll;
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(Enum.Parse<FormationShape>(expected), _service.CurrentShape);
        }

        [TestCase(nameof(FormationShape.Horizontal))]
        [TestCase(nameof(FormationShape.DiagonalDownRight))]
        [TestCase(nameof(FormationShape.DiagonalDownLeft))]
        public void ALevelAllowingOneShape_FormsThatShape_ThoughTheRollPicksTheSnake(string shape)
        {
            // The set's names are the shapes' own.
            var expected = Enum.Parse<FormationShape>(shape);
            _settings.SnakeWeight.Returns(100f);
            _settings.SnakeMaxMembers.Returns(5);
            Allow(Enum.Parse<FormationShapes>(shape));
            _service.Random01 = () => 0.999f;
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));

            Arrange();

            Assert.AreEqual(expected, _service.CurrentShape);
        }

        [Test]
        public void ALevelAllowingNoShape_LeavesTheUnbreakablesLoose()
        {
            _settings.SnakeWeight.Returns(1f);
            Allow(FormationShapes.None);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5), new Vector2Int(1, 1));
            var slots = _unbreakables.Select(u => u.SlotIndex.Value).ToList();

            Assert.DoesNotThrow(Arrange);
            Assert.DoesNotThrow(() => _service.StepSnakes(Relocate));

            Assert.IsNull(_service.CurrentShape);
            Assert.AreEqual(0, _service.Members.Count);
            Assert.AreEqual(0, _relocations, "nothing moved");
            Assert.IsFalse(_unbreakables.Any(u => u.InFormation.Value || u.IsAnchored));
            CollectionAssert.AreEqual(slots, _unbreakables.Select(u => u.SlotIndex.Value).ToList());
        }

        [Test]
        public void OnASnakeOnlyLevel_UnbreakablesPastTheSnakeCap_StayLoose()
        {
            // As NoMoreUnbreakableSnakes_ThanTheCap_TheThirdPicksAnotherShape, but the level allows no other shape.
            _service.Random01 = () => 0.999f;
            _settings.MinMembers.Returns(2);
            _settings.MaxUnbreakableSnakes.Returns(2);
            _settings.SnakeWeight.Returns(100f);
            _settings.SnakeMaxMembers.Returns(2);
            Allow(FormationShapes.Snake);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(5, 0), new Vector2Int(6, 0),
                new Vector2Int(0, 5), new Vector2Int(1, 5));

            Arrange();

            Assert.AreEqual(2, _service.Formations.Count, "the cap holds: no third formation");
            Assert.IsTrue(_service.Formations.All(f => f.Shape == FormationShape.Snake));
            Assert.AreEqual(4, _service.Members.Count);
            Assert.AreEqual(2, _unbreakables.Count(u => !u.InFormation.Value), "the two no snake can take stay loose");
        }

        [Test]
        public void OnASnakeOnlyLevel_ASnakeDoneHolding_StaysASnake_AndTakesInALooseUnbreakable()
        {
            var snake = FormSnake();
            _settings.HorizontalWeight.Returns(1f);
            _settings.DiagonalWeight.Returns(1f);
            Allow(FormationShapes.Snake);
            AddUnbreakable(new Vector2Int(5, 5));

            for (var cycle = 0; cycle < 3; cycle++)
            {
                Arrange();
            }

            Assert.AreEqual(1, _service.Formations.Count);
            Assert.AreEqual(FormationShape.Snake, snake.Shape, "it has no other shape to change to");
            Assert.AreEqual(4, _service.Members.Count, "planning again, it took the loose one in");
            Assert.IsTrue(_unbreakables.All(u => u.InFormation.Value));
        }

        [Test]
        public void ALineLeftFromALevelThatAllowedIt_TurnsIntoASnake_WhenItsHoldEnds()
        {
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape, "the level allows every shape");

            Allow(FormationShapes.Snake);
            Arrange();
            Arrange();
            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape, "a standing line holds its cycles out");

            Arrange();

            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape);
        }

        [Test]
        public void ALineLeftFromALevelThatAllowedIt_IsSetLoose_WhenNoShapeIsAllowedAtTheEndOfItsHold()
        {
            AddUnbreakables(new Vector2Int(0, 0), new Vector2Int(6, 4), new Vector2Int(3, 5));
            Arrange();
            Assert.AreEqual(FormationShape.Horizontal, _service.CurrentShape);

            Allow(FormationShapes.None);
            for (var cycle = 0; cycle < 3; cycle++)
            {
                Arrange();
            }

            Assert.AreEqual(0, _service.Formations.Count, "the line is not planned again as the last resort");
            Assert.IsFalse(_unbreakables.Any(u => u.InFormation.Value));
        }

        // The shapes the level in play lets the unbreakables form.
        private void Allow(FormationShapes shapes)
        {
            _levelParameters.AllowedFormationShapes.Returns(shapes);
        }

        // A shot in flight that plowed through member and holds it for its discharge.
        private ProjectileModel Plow(object member)
        {
            var shot = new ProjectileModel { IsFree = true };
            _projectiles.Load(shot, null);
            _projectiles.Launch(shot);
            shot.Flight.PendingPierceHits.Add(new PendingPierceHit((IBalloonModel)member, Vector3.zero));
            return shot;
        }

        private void SnakeOnly()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
            _settings.SnakeWeight.Returns(1f);
            _settings.SnakeMaxMembers.Returns(5);
            _settings.BeatSeconds.Returns(1.7f);
        }

        // A snake of count along row 2 from the west edge, heading east (Random01 is 0). An even row, so at the
        // east edge the south-east slot is still on the board.
        private StandingFormation FormSnake(int count = 3)
        {
            SnakeOnly();
            for (var i = 0; i < count; i++)
            {
                AddUnbreakable(new Vector2Int(i, 2));
            }

            Arrange();
            Assert.AreEqual(FormationShape.Snake, _service.CurrentShape);
            return _service.Formations[0];
        }

        private List<BalloonModel> AllBalloons()
        {
            var balloons = new List<BalloonModel>();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row) && _grid.At(new Vector2Int(col, row)) is BalloonModel balloon)
                    {
                        balloons.Add(balloon);
                    }
                }
            }

            return balloons;
        }

        private void Arrange()
        {
            _service.ArrangeOnCycle(Relocate);
        }

        private void WaveStart()
        {
            _service.RollReinforcementsAtWaveStart();
        }

        // As the balancer does it: every actor leaves its slot before any lands.
        private void Relocate(IReadOnlyList<SlotMove> moves)
        {
            _relocations += moves.Count;
            foreach (var move in moves)
            {
                _grid.Remove(move.Actor.SlotIndex.Value);
            }

            foreach (var move in moves)
            {
                _grid.Place(move.Actor, null, move.Target);
            }
        }

        // Leaves the item cover the only shape in the pick.
        private void CoverOnly()
        {
            _settings.HorizontalWeight.Returns(0f);
            _settings.DiagonalWeight.Returns(0f);
        }

        // A balloon carrying an item a cover shields.
        private BalloonModel PlaceHost(Vector2Int slot, ItemType item = ItemType.Bomb)
        {
            var host = new BalloonModel();
            host.Item.Value = item;
            _grid.Place(host, null, slot);
            return host;
        }

        private void AddUnbreakables(params Vector2Int[] slots)
        {
            foreach (var slot in slots)
            {
                AddUnbreakable(slot);
            }
        }

        // An unbreakable placed after the formations arranged, so it stays out of them.
        private UnbreakableBalloonModel Outsider(Vector2Int slot)
        {
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
            _grid.Place(unbreakable, null, slot);
            return unbreakable;
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

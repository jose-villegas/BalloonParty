using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     Placing a meadow level's mushrooms on the real board (<c>Plans/PLAN-MeadowMushrooms.md</c>, walls with a
    ///     kick): <see cref="WallSlotSelectionStrategy" /> picks a layout of walls from the baked catalogue for the
    ///     slots the board has free, and <see cref="StaticActorSpawner" /> places its caps before the clouds and
    ///     the bushes, which keep off the slots next to a cap. No view is involved: the models go on the grid.
    /// </summary>
    [TestFixture]
    public class WallSlotSelectionStrategyTests
    {
        private const int Seeds = 24;

        private SlotGrid _grid;
        private MushroomSettings _settings;
        private FakePlacementSource _source;
        private WallSlotSelectionStrategy _strategy;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(MushroomTestBoard.GridSize);
            gridConfig.SlotSeparation.Returns(new Vector2(0.375f, 0.65f));
            gridConfig.SlotsOffset.Returns(new Vector2(5f, 3.25f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());
            _settings = new MushroomSettings();
            _source = new FakePlacementSource { LayoutSeed = 1, BottomRowsReserved = 2 };
            _strategy = new WallSlotSelectionStrategy(_grid, _settings, _source);
        }

        [Test]
        public void SelectSlots_OnTheOpenBoard_AnswersWithALayoutOfTheCatalogue_OfTheWallsAsked(
            [Values(2, 3, 4)] int walls)
        {
            var different = new HashSet<string>();
            for (var seed = 1; seed <= Seeds; seed++)
            {
                _source.LayoutSeed = seed;

                var slots = _strategy.SelectSlots(EmptySlots(), walls);

                var layout = MushroomWallCatalogue.For(walls)
                    .FirstOrDefault(entry => entry.Caps.Select(cap => cap.Slot).SequenceEqual(slots));
                Assert.IsNotNull(layout, $"seed {seed}: the slots are a layout of {walls} walls, cap for cap");
                Assert.AreEqual(slots.Count, slots.Distinct().Count(), $"seed {seed}: no slot twice");
                for (var i = 0; i < slots.Count; i++)
                {
                    Assert.AreEqual(layout.Caps[i].Wall, _strategy.WallOf(i), $"seed {seed}: the wall of cap {i}");
                }

                different.Add(string.Join(" ", slots));
            }

            Assert.Greater(different.Count, Seeds / 2, "the seed is what picks the layout");
        }

        [Test]
        public void SelectSlots_TheSameSeed_GivesTheSameLayout()
        {
            _source.LayoutSeed = 7;
            var first = _strategy.SelectSlots(EmptySlots(), 3);
            var second = _strategy.SelectSlots(EmptySlots(), 3);

            Assert.Greater(first.Count, 0);
            CollectionAssert.AreEqual(first, second, "a retried level stands its mushrooms where they stood");
        }

        [Test]
        public void SelectSlots_TakesOnlySlotsItWasHanded()
        {
            // The clouds took four slots in the middle of the board: every seed still gets its walls, on the rest.
            var taken = new[] { new Vector2Int(2, 4), new Vector2Int(3, 4), new Vector2Int(2, 5), new Vector2Int(3, 6) };
            var left = EmptySlots().Where(slot => !taken.Contains(slot)).ToList();

            for (var seed = 1; seed <= Seeds; seed++)
            {
                _source.LayoutSeed = seed;

                var slots = _strategy.SelectSlots(left, 3);

                Assert.Greater(slots.Count, 0, $"seed {seed}");
                CollectionAssert.IsSubsetOf(slots, left, $"seed {seed}");
            }
        }

        [Test]
        public void SelectSlots_ASlotOfTheSeedsLayoutTaken_PicksTheNextLayoutThatFits()
        {
            _source.LayoutSeed = 5;
            var free = EmptySlots();
            var asSeeded = _strategy.SelectSlots(free, 2);
            free.Remove(asSeeded[0]);

            var next = _strategy.SelectSlots(free, 2);

            Assert.Greater(next.Count, 0, "another layout of two walls fits");
            CollectionAssert.DoesNotContain(next, asSeeded[0]);
            CollectionAssert.AreNotEqual(asSeeded, next);
        }

        [Test]
        public void SelectSlots_NoLayoutOfTheWallsAskedFits_PlacesOneWallFewer()
        {
            // The balloon spawner keeping five rows leaves rows 1 to 5. No layout of four walls of the catalogue
            // stands in those, and one of three does: every seed gets three walls where it asked for four.
            const int reserved = 5;
            var lastRow = MushroomTestBoard.GridSize.y - 1 - reserved;
            Assert.IsFalse(
                MushroomWallCatalogue.For(4).Any(layout => layout.Caps.All(cap => cap.Slot.y <= lastRow)),
                "the catalogue was baked again: find the rows that hold three walls and not four");
            Assert.IsTrue(MushroomWallCatalogue.For(3).Any(layout => layout.Caps.All(cap => cap.Slot.y <= lastRow)));
            _source.BottomRowsReserved = reserved;

            for (var seed = 1; seed <= Seeds; seed++)
            {
                _source.LayoutSeed = seed;

                var slots = _strategy.SelectSlots(EmptySlots(), 4);

                Assert.AreEqual(3, Enumerable.Range(0, slots.Count).Max(i => _strategy.WallOf(i)) + 1, $"seed {seed}");
                foreach (var slot in slots)
                {
                    Assert.LessOrEqual(slot.y, lastRow, $"seed {seed}: {slot}");
                }
            }
        }

        [Test]
        public void SelectSlots_NeverReadsFewerClearRowsThanTheSettingsKeep()
        {
            _source.BottomRowsReserved = 0;
            var lastRow = MushroomTestBoard.GridSize.y - 1 - _settings.Placement.BottomRowsKeptClear;

            for (var seed = 1; seed <= Seeds; seed++)
            {
                _source.LayoutSeed = seed;

                foreach (var slot in _strategy.SelectSlots(EmptySlots(), 4))
                {
                    Assert.LessOrEqual(slot.y, lastRow, $"seed {seed}: {slot}");
                }
            }
        }

        [Test]
        public void SelectSlots_AskedForNone_OrForFewerWallsThanTheFewest_PlacesNone()
        {
            Assert.AreEqual(0, _strategy.SelectSlots(EmptySlots(), 0).Count);
            Assert.AreEqual(0, _strategy.SelectSlots(EmptySlots(), _settings.Placement.MinCount - 1).Count);
            Assert.AreEqual(0, _strategy.Placed.Count);
        }

        [Test]
        public void SelectSlots_AskedForMoreWallsThanTheMost_PlacesTheMost()
        {
            var slots = _strategy.SelectSlots(EmptySlots(), _settings.Placement.MaxCount + 3);

            Assert.AreEqual(
                _settings.Placement.MaxCount, Enumerable.Range(0, slots.Count).Max(i => _strategy.WallOf(i)) + 1);
        }

        [Test]
        public void SelectSlots_NoLayoutFitsAtAll_PlacesNone_AndSaysSo()
        {
            LogAssert.Expect(LogType.Warning, new Regex("No layout of 3 walls or fewer fits the board"));
            var oneRow = EmptySlots().Where(slot => slot.y == 3).ToList();

            var slots = _strategy.SelectSlots(oneRow, 3);

            Assert.AreEqual(0, slots.Count);
            Assert.IsFalse(_strategy.IsBesideACap(new Vector2Int(2, 3)), "and nothing is beside a cap");
        }

        [Test]
        public void IsBesideACap_IsTrueForTheSlotsNextToACap_AndFalseFurtherOff()
        {
            var slots = _strategy.SelectSlots(EmptySlots(), 2);
            var pitch = 0.75f;

            foreach (var slot in MushroomTestBoard.AllSlots())
            {
                var nearest = slots.Min(cap => Vector2.Distance(
                    MushroomTestBoard.SlotCenter(cap), MushroomTestBoard.SlotCenter(slot)));

                Assert.AreEqual(nearest < pitch * 1.1f, _strategy.IsBesideACap(slot), $"{slot}, {nearest} from a cap");
            }
        }

        [Test]
        public void Spawner_PlacesTheWallsFirst_ThenTheCloudsAndTheBushes_WhateverTheConfigurationsOrder()
        {
            // Listed bushes first and mushrooms after them on purpose. Had the bushes or the clouds been placed
            // first, some of them would stand next to a cap, or in a slot a wall needed.
            var spawner = new StaticActorSpawner(
                _grid, Configuration(GridActorType.Bush, GridActorType.Mushroom, GridActorType.Puff),
                LevelWith((GridActorType.Puff, 4), (GridActorType.Mushroom, 3), (GridActorType.Bush, 12)),
                _strategy);

            spawner.SpawnStaticActors();

            var mushrooms = ActorsOf<MushroomBumperModel>();
            Assert.GreaterOrEqual(mushrooms.Count, _settings.Placement.MinCaps);
            Assert.LessOrEqual(mushrooms.Count, _settings.Placement.MaxCapsFor(3));
            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, mushrooms.Count), mushrooms.Select(m => m.ChainIndex), "each cap carries its own id");
            var clouds = ActorsOf<PuffObstacleModel>();
            Assert.AreEqual(4, clouds.Count);
            foreach (var cloud in clouds)
            {
                Assert.IsFalse(_strategy.IsBesideACap(cloud.SlotIndex), $"the cloud at {cloud.SlotIndex}");
            }

            var bushes = ActorsOf<BushObstacleModel>();
            Assert.AreEqual(12, bushes.Count);
            foreach (var bush in bushes)
            {
                Assert.IsFalse(_strategy.IsBesideACap(bush.SlotIndex), $"the bush at {bush.SlotIndex}");
            }
        }

        // The mushrooms are placed first because they are mushrooms, not because the configuration lists them
        // first: listed last, after clouds asked to take every slot, their walls still stand, and the clouds get
        // what is left that is not next to a cap. Had the clouds gone first there would be no slot for a wall.
        [Test]
        public void Spawner_PlacesTheMushroomsBeforeTheClouds_ByTypeAndNotByTheirPlaceInTheList()
        {
            var everySlot = MushroomTestBoard.GridSize.x * MushroomTestBoard.GridSize.y;
            var spawner = new StaticActorSpawner(
                _grid, Configuration(GridActorType.Puff, GridActorType.Bush, GridActorType.Mushroom),
                LevelWith((GridActorType.Puff, everySlot), (GridActorType.Mushroom, 3), (GridActorType.Bush, 4)),
                _strategy);

            spawner.SpawnStaticActors();

            var caps = ActorsOf<MushroomBumperModel>();
            Assert.AreEqual(_strategy.Placed.Count, caps.Count);
            Assert.AreEqual(3, caps.Select(cap => cap.Wall).Distinct().Count(), "the three walls asked for");
            var free = MushroomTestBoard.AllSlots().Count(slot => !_strategy.IsBesideACap(slot));
            Assert.AreEqual(free, ActorsOf<PuffObstacleModel>().Count, "the clouds took every slot not next to a cap");
            Assert.AreEqual(0, ActorsOf<BushObstacleModel>().Count);
        }

        // A retry deals the level again with the same seed, and what is placed with the engine's own random
        // numbers (the clouds, the bushes) lands somewhere else. The walls are placed first, on a board with
        // nothing on it, so they stand where they stood.
        [Test]
        public void Spawner_ARetry_StandsTheSameWalls_WhereverTheCloudsLand([Values(2, 3, 4)] int walls)
        {
            var first = SpawnWithRandomState(11, walls, out var firstClouds);
            SetUp();
            var second = SpawnWithRandomState(987654, walls, out var secondClouds);

            Assert.Greater(first.Count, 0);
            CollectionAssert.AreEqual(first, second, "the same caps in the same slots, cap for cap");
            CollectionAssert.AreNotEquivalent(firstClouds, secondClouds, "though the clouds landed elsewhere");
        }

        [Test]
        public void Spawner_AMushroomGateCountsWalls_AndEveryCapCarriesItsWall([Values(2, 3, 4)] int walls)
        {
            var spawner = new StaticActorSpawner(
                _grid, Configuration(GridActorType.Mushroom), LevelWith((GridActorType.Mushroom, walls)), _strategy);

            spawner.SpawnStaticActors();

            var shotOrigin = Substitute.For<IShotOrigin>();
            shotOrigin.ProjectileContactRadius.Returns(MushroomTestBoard.BallRadius);
            var board = new MushroomBoard(_grid, _settings, shotOrigin);
            Assert.AreEqual(_strategy.Placed.Count, board.Bumpers.Count, "a model for every cap of the layout");
            Assert.AreEqual(walls, board.Bumpers.Select(bumper => bumper.Wall).Distinct().Count(), "the walls asked for");
            for (var i = 0; i < board.Bumpers.Count; i++)
            {
                var bumper = board.Bumpers[i];
                Assert.AreEqual(i, bumper.Id);
                Assert.AreEqual(_strategy.Placed[i].Wall, bumper.Wall, $"cap {i}");
                Assert.AreEqual(_strategy.Placed[i].Slot, board.SlotAt(i), $"cap {i}");
            }

            // Caps of one wall are neighbours in a line, so their circles overlap; caps of two walls never do.
            foreach (var a in board.Bumpers)
            {
                foreach (var b in board.Bumpers)
                {
                    if (a.Id < b.Id && a.Wall != b.Wall)
                    {
                        Assert.GreaterOrEqual(
                            Vector2.Distance(a.Center, b.Center), _settings.Placement.MinSpacing, $"{a.Id} and {b.Id}");
                    }
                }
            }
        }

        [Test]
        public void Spawner_OnALevelWithoutAMushroomGate_PlacesNoMushrooms()
        {
            var spawner = new StaticActorSpawner(
                _grid, Configuration(GridActorType.Puff, GridActorType.Mushroom), LevelWith((GridActorType.Puff, 3)),
                _strategy);

            spawner.SpawnStaticActors();

            Assert.AreEqual(0, ActorsOf<MushroomBumperModel>().Count);
            Assert.AreEqual(3, ActorsOf<PuffObstacleModel>().Count);
        }

        [Test]
        public void Spawner_WithNoWallStrategy_SkipsTheMushroomEntryAndPlacesTheRest()
        {
            var spawner = new StaticActorSpawner(
                _grid, Configuration(GridActorType.Puff, GridActorType.Mushroom, GridActorType.Bush),
                LevelWith((GridActorType.Puff, 3), (GridActorType.Mushroom, 3), (GridActorType.Bush, 4)));

            spawner.SpawnStaticActors();

            Assert.AreEqual(0, ActorsOf<MushroomBumperModel>().Count);
            Assert.AreEqual(3, ActorsOf<PuffObstacleModel>().Count);
            Assert.AreEqual(4, ActorsOf<BushObstacleModel>().Count);
        }

        // Deals a level of clouds, walls and bushes with the engine's random numbers in the given state. Answers
        // the caps' slots in the order of their ids, and where the clouds landed.
        private List<Vector2Int> SpawnWithRandomState(int state, int walls, out List<Vector2Int> clouds)
        {
            Random.InitState(state);
            var spawner = new StaticActorSpawner(
                _grid, Configuration(GridActorType.Puff, GridActorType.Mushroom, GridActorType.Bush),
                LevelWith((GridActorType.Puff, 6), (GridActorType.Mushroom, walls), (GridActorType.Bush, 2)),
                _strategy);

            spawner.SpawnStaticActors();

            clouds = ActorsOf<PuffObstacleModel>().Select(cloud => cloud.SlotIndex).ToList();
            return ActorsOf<MushroomBumperModel>().OrderBy(cap => cap.ChainIndex).Select(cap => cap.SlotIndex).ToList();
        }

        private List<Vector2Int> EmptySlots()
        {
            return new List<Vector2Int>(_grid.AllEmptySlots());
        }

        private List<T> ActorsOf<T>()
            where T : class, IWriteableSlotActor
        {
            return _grid.AllOccupiedSlots().Select(slot => _grid.At(slot) as T).Where(actor => actor != null).ToList();
        }

        private static IGridActorConfiguration Configuration(params GridActorType[] types)
        {
            var entries = new List<GridActorPrefabEntry>();
            foreach (var type in types)
            {
                var entry = new GridActorPrefabEntry();
                SetField(entry, "_actorType", type);
                entries.Add(entry);
            }

            var config = Substitute.For<IGridActorConfiguration>();
            config.Entries.Returns(entries);
            return config;
        }

        private static IActiveLevelParameters LevelWith(params (GridActorType Type, int Count)[] gates)
        {
            var levelParams = Substitute.For<IActiveLevelParameters>();
            var current = Substitute.For<ILevelParameters>();
            levelParams.Current.Returns(current);
            current.TryGetGridActorGate(Arg.Any<GridActorType>(), out Arg.Any<ResolvedGridActorGate>())
                .Returns(call =>
                {
                    var type = (GridActorType)call[0];
                    foreach (var gate in gates)
                    {
                        if (gate.Type == type)
                        {
                            call[1] = new ResolvedGridActorGate(type, gate.Count, 0);
                            return true;
                        }
                    }

                    return false;
                });
            return levelParams;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }

        private sealed class FakePlacementSource : IMushroomPlacementSource
        {
            public int LayoutSeed { get; set; }
            public int BottomRowsReserved { get; set; }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Slots.Actor;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     What every layout of the baked catalogue keeps to by its shape (<c>Plans/PLAN-MeadowMushrooms.md</c>,
    ///     walls with a kick): how many walls and caps, walls that are lines of neighbouring slots, the spacing
    ///     between walls, the rows and columns kept clear, and the slots left to the bushes. What a layout
    ///     measures when it is flown is held by <c>MushroomAimGridTests</c>.
    /// </summary>
    [TestFixture]
    public class MushroomWallCatalogueTests
    {
        // Enough layouts of each count that a run does not see the same board over and over. The bake keeps 64
        // where that many qualify, and every board that does where fewer do: 18 of two walls and 45 of four, of
        // every board the lattice holds. Two walls seldom send the ball to each other from up the board, where the
        // cannon's room puts them, and a layout must be well inside the band to be kept.
        private const int FewestLayouts = 16;

        // The slots the bushes are never left without, of the ones they share with the mushrooms.
        private const int BushSlotsKept = 2;

        // Neighbouring slots stand this far apart, along a row or along a diagonal, to a rounding error.
        private const float Pitch = 0.75f;
        private const float PitchTolerance = 0.01f;

        private readonly MushroomSettings _settings = new();

        [Test]
        public void TheCatalogue_HasLayoutsOfEveryCountALevelAsksFor_AndOfNoOther()
        {
            var limits = _settings.Placement;

            Assert.AreEqual(limits.MinCount, MushroomWallCatalogue.FewestWalls);
            Assert.AreEqual(limits.MaxCount, MushroomWallCatalogue.MostWalls);
            Assert.AreEqual(0, MushroomWallCatalogue.For(limits.MinCount - 1).Count);
            Assert.AreEqual(0, MushroomWallCatalogue.For(limits.MaxCount + 1).Count);
        }

        [Test]
        public void EveryCount_HasEnoughLayouts_AndNoTwoTheSame([Values(2, 3, 4)] int walls)
        {
            var layouts = MushroomWallCatalogue.For(walls);

            Assert.GreaterOrEqual(layouts.Count, FewestLayouts);
            var boards = layouts.Select(layout =>
                string.Join(" ", layout.Caps.Select(cap => cap.Slot).OrderBy(slot => slot.x).ThenBy(slot => slot.y)));
            Assert.AreEqual(layouts.Count, boards.Distinct().Count(), "no board twice");
        }

        [Test]
        public void EveryLayout_HasTheWallsOfItsCount_AndCapsWithinTheBudget([Values(2, 3, 4)] int walls)
        {
            var limits = _settings.Placement;
            foreach (var layout in MushroomWallCatalogue.For(walls))
            {
                var name = Describe(layout);
                Assert.AreEqual(walls, layout.Walls, name);
                Assert.AreEqual(walls, layout.Caps.Select(cap => cap.Wall).Distinct().Count(), name);
                Assert.GreaterOrEqual(layout.Caps.Count, limits.MinCaps, name);
                Assert.LessOrEqual(layout.Caps.Count, limits.MaxCapsFor(walls), name);
                Assert.GreaterOrEqual(
                    _settings.PermanentSlotLimit - layout.Caps.Count, BushSlotsKept, $"{name}: the bushes keep two slots");
                Assert.AreEqual(
                    layout.Caps.Count, layout.Caps.Select(cap => cap.Slot).Distinct().Count(), $"{name}: no slot twice");
            }
        }

        [Test]
        public void EveryWall_IsOneToThreeCapsInALineOfNeighbouringSlots([Values(2, 3, 4)] int walls)
        {
            foreach (var layout in MushroomWallCatalogue.For(walls))
            {
                foreach (var wall in layout.Caps.GroupBy(cap => cap.Wall))
                {
                    var centres = wall.Select(cap => MushroomTestBoard.SlotCenter(cap.Slot)).ToList();
                    var name = $"{Describe(layout)}, wall {wall.Key}";
                    Assert.LessOrEqual(centres.Count, MushroomPlacementLimits.LongestWall, name);
                    for (var i = 1; i < centres.Count; i++)
                    {
                        Assert.AreEqual(Pitch, Vector2.Distance(centres[i - 1], centres[i]), PitchTolerance, name);
                    }

                    // A line: the ends are as far apart as the steps between them add up to.
                    Assert.AreEqual(
                        Pitch * (centres.Count - 1), Vector2.Distance(centres[0], centres[^1]), 2f * PitchTolerance, name);
                }
            }
        }

        // A wall of one cap is only there to fill out a board: none where every wall can be two caps or more
        // within the budget, and one on a board of four walls, which seven caps cannot fill with pairs.
        [TestCase(2, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 1)]
        public void AWallOfOneCap_IsOnlyThereToFillOutABoard(int walls, int singles)
        {
            foreach (var layout in MushroomWallCatalogue.For(walls))
            {
                Assert.AreEqual(
                    singles, layout.Caps.GroupBy(cap => cap.Wall).Count(wall => wall.Count() == 1), Describe(layout));
            }
        }

        [Test]
        public void EveryLayout_KeepsItsWallsTheSpacingApart_AndOffTheRowsAndColumnsKeptClear(
            [Values(2, 3, 4)] int walls)
        {
            var limits = _settings.Placement;
            var spacing = limits.EffectiveMinSpacing(MushroomTestBoard.ContactRadius);
            var nearest = float.MaxValue;
            foreach (var layout in MushroomWallCatalogue.For(walls))
            {
                foreach (var cap in layout.Caps)
                {
                    Assert.IsTrue(limits.Allows(cap.Slot, MushroomTestBoard.GridSize), $"{Describe(layout)}: {cap.Slot}");
                    foreach (var other in layout.Caps.Where(other => other.Wall != cap.Wall))
                    {
                        var apart = Vector2.Distance(
                            MushroomTestBoard.SlotCenter(cap.Slot), MushroomTestBoard.SlotCenter(other.Slot));
                        Assert.GreaterOrEqual(apart, spacing, $"{Describe(layout)}: {cap.Slot} and {other.Slot}");
                        nearest = Mathf.Min(nearest, apart);
                    }
                }
            }

            // The spacing is 1.9 and slots stand on a lattice: the nearest two walls ever come is the next step
            // of it, 1.98. Under 2.25, so the rule is what holds them there and not the boards being sparse.
            Assert.Less(nearest, 2.25f, "some layout stands its walls as near as the spacing lets it");
        }

        [Test]
        public void ACap_PacksToOneNumber_AndComesBackTheSame()
        {
            var seen = new HashSet<int>();
            foreach (var layout in MushroomWallCatalogue.For(4))
            {
                foreach (var cap in layout.Caps)
                {
                    var packed = MushroomWallCatalogue.Pack(cap);

                    Assert.AreEqual(cap.Wall, packed >> 8);
                    Assert.AreEqual(cap.Slot, new Vector2Int((packed >> 4) & 0xF, packed & 0xF));
                    seen.Add(packed);
                }
            }

            Assert.Greater(seen.Count, 20, "the catalogue's caps are many different ones");
        }

        private static string Describe(MushroomWallLayout layout)
        {
            return string.Join(" | ", layout.Caps.GroupBy(cap => cap.Wall)
                .Select(wall => string.Join(" ", wall.Select(cap => cap.Slot))));
        }
    }
}

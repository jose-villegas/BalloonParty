using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Level;
using BalloonParty.Editor;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Solver;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Board = BalloonParty.Tests.Slots.MushroomTestBoard;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     What the mushroom walls were asked to give, on the angles a player can really aim at (<c>Plans/
    ///     PLAN-MeadowMushrooms.md</c>, walls with a kick): every layout of the baked catalogue, flown again from
    ///     every angle of the thrower's aim grid on a board with nothing else on it, arms the lance on a share of
    ///     them inside the settings' band, in a window a player can aim into. Flown by the shot simulator, which is
    ///     what the bot aims with and which the mirror tests hold to the game.
    /// </summary>
    /// <remarks>
    ///     The catalogue is baked once and a level only picks from it, so this is where a layout is held to what
    ///     it was kept for. The cannon, the ball, the walls, the aim grid and the slots are written out in
    ///     <c>MushroomTestBoard</c>, so the sweep runs on plain numbers;
    ///     <see cref="TheNumbersFlownWith_AreTheShippedOnes" /> reads the shipped assets and fails when one of them,
    ///     or a mushroom setting, is no longer what the catalogue was baked with.
    /// </remarks>
    [TestFixture]
    public class MushroomAimGridTests
    {
        private const string LevelPacingPath = "Assets/Configuration/LevelPacingConfiguration.asset";

        // Another runtime rounds a flight's arithmetic differently, and a ball that grazes a cap on one may miss
        // it on the other: a layout may measure this many arming angles, and this many angles of its widest
        // window, off what the baker wrote down, and no more. Measured between the bake's runtime and the
        // Editor's: 1 arming angle on 6 layouts of 127 (3 angles at the most over the earlier bakes), and no angle
        // of window on any. The bake keeps a layout only with more room than this inside the band
        // (TheBakesRoom_IsWiderThanTheDriftALayoutIsAllowed).
        private const int ArmingAnglesOfDrift = 4;
        private const int WindowAnglesOfDrift = 1;

        // The catalogue holds for these numbers and no others. Read from what ships: the thrower's place in the
        // Game scene and its prefab's entrance and muzzle, the ball's collider, the flight config's walls, aim
        // grid and starting shields, the grid's slots, and the mushroom settings on the Level Pacing asset.
        [Test]
        public void TheNumbersFlownWith_AreTheShippedOnes()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<LevelPacingConfiguration>(LevelPacingPath).Mushrooms;

            Assert.AreEqual(
                MushroomWallCatalogue.BakedWith, MushroomWallBaker.Stamp(MushroomWallBaker.ShippedBoard(), shipped),
                "What ships is no longer what the catalogue was baked with: bake it again " +
                "(Tools/BalloonParty/Bake Mushroom Wall Catalogue).");
        }

        // The same for the numbers written in these tests and the settings' own defaults, which is what the sweeps
        // below fly with: it runs outside the Editor too.
        [Test]
        public void TheNumbersWrittenHere_AreTheOnesTheCatalogueWasBakedWith()
        {
            Assert.AreEqual(
                MushroomWallCatalogue.BakedWith, MushroomWallBaker.Stamp(Board.BakeBoard(), new MushroomSettings()));
        }

        [Test]
        public void EveryLayout_FlownAgain_IsInsideTheBand([Values(2, 3, 4)] int walls)
        {
            var settings = new MushroomSettings();
            var band = settings.Placement.Band;
            var board = Board.BakeBoard();
            var shares = new List<float>();
            var windows = new List<float>();
            var fewest = int.MaxValue;
            var drift = 0;
            foreach (var layout in MushroomWallCatalogue.For(walls))
            {
                var measure = MushroomWallBaker.Measure(layout.Caps, board, settings);

                var name = Describe(layout);
                Assert.IsTrue(
                    band.Accepts(measure.ArmingAngles, measure.WidestWindowAngles, measure.Angles, Board.AimStepDegrees),
                    $"{name}: arms on {measure.ArmingAngles} of {measure.Angles} angles, the widest window " +
                    $"{measure.WidestWindowAngles} of them");
                Assert.AreEqual(
                    layout.ArmingAngles, measure.ArmingAngles, ArmingAnglesOfDrift, $"{name}: the angles that arm");
                Assert.AreEqual(
                    layout.WidestWindowAngles, measure.WidestWindowAngles, WindowAnglesOfDrift,
                    $"{name}: the widest window");
                shares.Add(100f * measure.ArmingAngles / measure.Angles);
                windows.Add(measure.WidestWindowAngles * Board.AimStepDegrees);
                fewest = Mathf.Min(fewest, measure.ArmingAngles);
                drift = Mathf.Max(drift, Mathf.Abs(layout.ArmingAngles - measure.ArmingAngles));
            }

            ShotMushroomFreeBoardTests.Report(
                $"Mushroom walls on the aim grid, {walls} walls, {shares.Count} layouts, {Board.Angles} angles each. " +
                $"Angles that arm the lance: mean {shares.Average():F1}%, least {shares.Min():F1}%, most " +
                $"{shares.Max():F1}%, fewest angles {fewest}. Widest window: mean {windows.Average():F1} degrees, " +
                $"narrowest {windows.Min():F1}, widest {windows.Max():F1}. Furthest from the baked count: {drift} angles.");
            Assert.GreaterOrEqual(fewest, band.MinArmingAngles);
        }

        // The owner's rule, after a wall of three stood side-on 2.7 units above the cannon: the cannon's area is
        // not blocked. No cap of any layout stands nearer the cannon's resting pivot than the clear distance, and a
        // ball's first straight leg from the muzzle meets a cap within the near range on no more of the aim angles
        // than the ceiling. Measured here from the pivot and the muzzle written in these tests, which
        // TheNumbersFlownWith_AreTheShippedOnes holds to what ships.
        [Test]
        public void EveryLayout_LeavesTheCannonItsRoom([Values(2, 3, 4)] int walls)
        {
            var settings = new MushroomSettings();
            var cannon = settings.Placement.Cannon;
            var board = Board.BakeBoard();
            var nearest = float.MaxValue;
            var broadest = 0;
            foreach (var layout in MushroomWallCatalogue.For(walls))
            {
                var view = MushroomWallBaker.CannonViewOf(layout.Caps, board, settings);

                var name = Describe(layout);
                Assert.GreaterOrEqual(view.NearestCap, cannon.MinDistance, $"{name}: a cap too near the cannon");
                Assert.LessOrEqual(
                    view.ShadowedAngles, cannon.MaxShadowShare * view.Angles, $"{name}: too broad in front of the cannon");
                Assert.IsTrue(cannon.Allows(view.NearestCap, view.ShadowedAngles, view.Angles), name);
                nearest = Mathf.Min(nearest, view.NearestCap);
                broadest = Mathf.Max(broadest, view.ShadowedAngles);
            }

            ShotMushroomFreeBoardTests.Report(
                $"Mushroom walls and the cannon, {walls} walls: the nearest cap of any layout is {nearest:F2} from the " +
                $"pivot, and the broadest layout takes {100f * broadest / Board.Angles:F1}% of the aim angles within " +
                $"{cannon.NearRange} of the muzzle.");
        }

        // The same, measured from the cannon as it ships: the thrower's place in the Game scene, its entrance and
        // its muzzle (Editor only: it loads the assets).
        [Test]
        public void EveryLayout_LeavesTheShippedCannonItsRoom()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<LevelPacingConfiguration>(LevelPacingPath).Mushrooms;
            var board = MushroomWallBaker.ShippedBoard();
            var cannon = shipped.Placement.Cannon;
            for (var walls = MushroomWallCatalogue.FewestWalls; walls <= MushroomWallCatalogue.MostWalls; walls++)
            {
                foreach (var layout in MushroomWallCatalogue.For(walls))
                {
                    var view = MushroomWallBaker.CannonViewOf(layout.Caps, board, shipped);

                    Assert.IsTrue(
                        cannon.Allows(view.NearestCap, view.ShadowedAngles, view.Angles),
                        $"{Describe(layout)}: the nearest cap {view.NearestCap} from the pivot, {view.ShadowedAngles} " +
                        $"of {view.Angles} angles met within {cannon.NearRange} of the muzzle");
                }
            }
        }

        // What the rule is there to refuse: the wall in the owner's screenshot, three caps along the lowest row a
        // cap may stand in, straight above the cannon and side-on to it. The same wall five rows up is far enough
        // away and out of the near range.
        [Test]
        public void AWallAcrossTheCannonsView_IsRefused_AndTheSameWallUpTheBoardIsNot()
        {
            var settings = new MushroomSettings();
            var cannon = settings.Placement.Cannon;
            var board = Board.BakeBoard();
            MushroomWallCap[] WallAlongRow(int row)
            {
                return new[]
                {
                    new MushroomWallCap(new Vector2Int(2, row), 0), new MushroomWallCap(new Vector2Int(3, row), 0),
                    new MushroomWallCap(new Vector2Int(4, row), 0),
                };
            }

            var low = MushroomWallBaker.CannonViewOf(WallAlongRow(8), board, settings);
            var high = MushroomWallBaker.CannonViewOf(WallAlongRow(3), board, settings);

            Assert.Less(low.NearestCap, 2.8f, "2.7 units above the cannon");
            Assert.Greater(low.ShadowedAngles, low.Angles / 4, "and across a quarter of everything it can aim at");
            Assert.IsFalse(cannon.Allows(low.NearestCap, low.ShadowedAngles, low.Angles));
            Assert.IsTrue(cannon.Allows(high.NearestCap, high.ShadowedAngles, high.Angles));
            Assert.AreEqual(0, high.ShadowedAngles, "out of the near range altogether");
        }

        // The game runs on a third runtime, the device's, which no test flies on. So no entry may sit near the
        // band's edge: as baked, every layout is still inside the band with all the drift the test above allows
        // it, either way, and the bake's own room is wider than that drift.
        [Test]
        public void TheBakesRoom_IsWiderThanTheDriftALayoutIsAllowed([Values(2, 3, 4)] int walls)
        {
            var band = new MushroomSettings().Placement.Band;
            Assert.Greater(MushroomWallBaker.ArmingMarginAngles, ArmingAnglesOfDrift);
            Assert.Greater(MushroomWallBaker.WindowMarginAngles, WindowAnglesOfDrift);

            foreach (var layout in MushroomWallCatalogue.For(walls))
            {
                var name = Describe(layout);
                Assert.IsTrue(
                    MushroomWallBaker.Keeps(
                        new MushroomWallMeasure(layout.ArmingAngles, layout.WidestWindowAngles, Board.Angles), band,
                        Board.AimStepDegrees),
                    $"{name}: kept with the bake's room");
                Assert.IsTrue(
                    band.Accepts(
                        layout.ArmingAngles - ArmingAnglesOfDrift, layout.WidestWindowAngles - WindowAnglesOfDrift,
                        Board.Angles, Board.AimStepDegrees),
                    $"{name}: inside the band with the fewest angles it may drift to");
                Assert.IsTrue(
                    band.Accepts(
                        layout.ArmingAngles + ArmingAnglesOfDrift, layout.WidestWindowAngles, Board.Angles,
                        Board.AimStepDegrees),
                    $"{name}: inside the band with the most angles it may drift to");
            }
        }

        // The band is not one any board passes: the same layouts with the kick switched off, every bounce a mirror
        // reflection, arm on a fraction of the angles and fall out of it. The kick between walls is what is flown.
        [Test]
        public void WithoutTheKick_TheSameLayoutsFallOutOfTheBand([Values(2, 3, 4)] int walls)
        {
            var kicked = new MushroomSettings();
            var plain = new Unkicked(kicked);
            var band = kicked.Placement.Band;
            var board = Board.BakeBoard();
            var withKick = 0;
            var without = 0;
            var stillInside = 0;
            var layouts = MushroomWallCatalogue.For(walls);
            foreach (var layout in layouts)
            {
                var measure = MushroomWallBaker.Measure(layout.Caps, board, plain);

                withKick += layout.ArmingAngles;
                without += measure.ArmingAngles;
                stillInside += band.Accepts(
                    measure.ArmingAngles, measure.WidestWindowAngles, measure.Angles, Board.AimStepDegrees) ? 1 : 0;
            }

            Assert.Less(without * 2, withKick, "under half the arming angles are left");
            Assert.Less(stillInside * 10, layouts.Count, "and under a tenth of the layouts are still inside the band");
        }

        // What the lance a layout pays does next: armed on a cap of one wall, it leaves for the wall it came from,
        // which the kick landed it for, and cuts the cap there. So a flight that arms nearly always cuts a cap, and
        // never bounces off a mushroom again while it is a lance. Logged: how many it cuts.
        [Test]
        public void AFlightThatArms_GoesOnToCutCaps_AndBouncesOffNoneAsALance([Values(2, 3, 4)] int walls)
        {
            var settings = new MushroomSettings();
            var balloons = new[] { ShotBalloonSnapshot.ForToughTarget(new Vector2(1000f, 1000f), 0.1f, 0, int.MaxValue) };
            var workingSet = new ShotBalloonState[balloons.Length];
            var bounces = new List<ShotBumperBounce>();
            var armed = 0;
            var cutting = 0;
            var capsCut = 0;
            var cutEveryCap = 0;
            var layouts = MushroomWallCatalogue.For(walls);
            foreach (var layout in layouts)
            {
                var layer = new ShotBumperLayer(Board.Bumpers(layout), new ForgivingKickRule(settings), settings.Rules, 1.6f)
                {
                    BounceLog = bounces,
                };
                for (var i = 0; i < Board.Angles; i++)
                {
                    var direction = Board.Direction(i);
                    var result = ShotSimulator.Simulate(
                        balloons, Board.WallsClockwise, Board.Muzzle(direction), direction, Board.StartingShields,
                        Board.BallRadius, workingSet, rippleDamage: 1, streakGrantsShields: false, bumpers: layer);
                    var arming = bounces.FindIndex(bounce => bounce.Reward == MushroomReward.Arm);
                    Assert.AreEqual(result.Mushrooms.LancesArmed > 0, arming >= 0);
                    if (arming < 0)
                    {
                        Assert.AreEqual(0, result.Mushrooms.CapsCut, "an ordinary ball cuts nothing");
                        continue;
                    }

                    Assert.AreEqual(arming, bounces.Count - 1, "the bounce that arms is the flight's last bounce");
                    armed++;
                    cutting += result.Mushrooms.CapsCut > 0 ? 1 : 0;
                    capsCut += result.Mushrooms.CapsCut;
                    cutEveryCap += result.Mushrooms.CapsCut == layout.Caps.Count ? 1 : 0;
                }
            }

            // Every layout is in the band, which asks at least this many arming angles of each.
            Assert.GreaterOrEqual(
                armed, layouts.Count * settings.Placement.Band.MinArmingAngles, "each layout arms on many angles");
            Assert.Greater(cutting * 100, armed * 90, $"{cutting} of {armed} arming flights cut a cap");
            Assert.AreEqual(0, cutEveryCap, "no one flight leaves a board bare");
            ShotMushroomFreeBoardTests.Report(
                $"Mushroom walls and the lance they pay, {walls} walls: {armed} arming flights over {layouts.Count} " +
                $"layouts, {100f * cutting / armed:F1}% of them cut a cap, {(float)capsCut / armed:F2} caps a flight on " +
                "average.");
        }

        private static string Describe(MushroomWallLayout layout)
        {
            return string.Join(" | ", layout.Caps.GroupBy(cap => cap.Wall)
                .Select(wall => string.Join(" ", wall.Select(cap => cap.Slot))));
        }

        // The shipped settings with every bounce a mirror reflection.
        private sealed class Unkicked : IMushroomSettings
        {
            private readonly MushroomSettings _shipped;

            public MushroomChainRules Rules => _shipped.Rules;
            public bool ForgivingKick => false;
            public BumperKickTuning Kick => _shipped.Kick;
            public float Radius => _shipped.Radius;
            public MushroomPlacementLimits Placement => _shipped.Placement;
            public int PermanentSlotLimit => _shipped.PermanentSlotLimit;
            public MushroomLeanTuning Lean => _shipped.Lean;
            public MushroomSporeTuning Spores => _shipped.Spores;
            public MushroomCutTuning Cut => _shipped.Cut;
            public int RegrowBeats => _shipped.RegrowBeats;
            public MushroomBlastTuning Blast => _shipped.Blast;
            public MushroomIdleTuning Idle => _shipped.Idle;

            public Unkicked(MushroomSettings shipped)
            {
                _shipped = shipped;
            }
        }
    }
}

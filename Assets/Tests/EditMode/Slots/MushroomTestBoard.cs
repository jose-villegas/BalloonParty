using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Editor;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     The shipped board as the mushroom tests see it: the walls, the cannon, the aim grid and the slots written
    ///     out as plain numbers, so the tests run without the game's assets
    ///     (<c>MushroomAimGridTests.TheNumbersFlownWith_AreTheShippedOnes</c> holds them against what ships), and
    ///     the layouts of the baked catalogue as the ball meets them.
    /// </summary>
    internal static class MushroomTestBoard
    {
        internal const float MushroomRadius = 0.3f;
        internal const float BallRadius = 0.125f;
        internal const float ContactRadius = MushroomRadius + BallRadius;

        // ProjectileFlightConfig's aim arc and step (the thrower reaches every third of a degree) and the shields
        // a ball leaves the cannon with.
        internal const float ArcMin = 5f;
        internal const float ArcMax = 175f;
        internal const float AimStepDegrees = 1f / 3f;
        internal const int StartingShields = 1;

        // The muzzle sits half a unit ahead of the thrower's pivot along the aim and the ball's collider 0.03
        // ahead of that, so every shot's line passes through the pivot whatever the angle.
        internal const float MuzzleAhead = 0.53f;

        // SlotGridConfig's grid and ProjectileFlightConfig's walls.
        internal static readonly Vector2Int GridSize = new(6, 11);
        internal static readonly Vector4 WallsClockwise = new(3.625f, 2.25f, -4.5f, -2.25f);

        // The thrower's pivot at rest: the scene's (0, -5.1) and the half unit its entrance lifts it.
        internal static readonly Vector2 CannonPivot = new(0f, -4.6f);

        // A cannon a little under the bottom wall, where the tests written before the pivot was measured shoot
        // from. The digest of the mushroom-free boards is pinned for it.
        internal static readonly Vector2 Cannon = new(0f, -5f);

        private static readonly Vector2 SlotSeparation = new(0.375f, 0.65f);
        private static readonly Vector2 SlotsOffset = new(5f, 3.25f);

        /// <summary>How many angles the thrower's aim grid has.</summary>
        internal static int Angles => AimAngleGrid.ResolveSweepSampleCount(ArcMin, ArcMax, AimStepDegrees, 1);

        internal static IEnumerable<Vector2Int> AllSlots()
        {
            for (var column = 0; column < GridSize.x; column++)
            {
                for (var row = 0; row < GridSize.y; row++)
                {
                    yield return new Vector2Int(column, row);
                }
            }
        }

        internal static Vector2 SlotCenter(Vector2Int slot)
        {
            return HexCoordinates.IndexToWorldPosition(slot, SlotSeparation, SlotsOffset);
        }

        /// <summary>The board, the cannon and the aim grid as the catalogue's baker takes them.</summary>
        internal static MushroomBakeBoard BakeBoard()
        {
            return new MushroomBakeBoard
            {
                WallsClockwise = WallsClockwise,
                CannonPivot = CannonPivot,
                MuzzleAhead = MuzzleAhead,
                AimMinDegrees = ArcMin,
                AimMaxDegrees = ArcMax,
                AimStepDegrees = AimStepDegrees,
                BallRadius = BallRadius,
                StartingShields = StartingShields,
                GridSize = GridSize,
                SlotCenter = SlotCenter,
            };
        }

        /// <summary>A layout of the baked catalogue: of <paramref name="walls" /> walls, the one at
        /// <paramref name="index" /> counted round the catalogue's list.</summary>
        internal static MushroomWallLayout Layout(int walls, int index)
        {
            var layouts = MushroomWallCatalogue.For(walls);
            return layouts[((index % layouts.Count) + layouts.Count) % layouts.Count];
        }

        /// <summary>The layout's caps as the ball meets them: each with its id and its wall.</summary>
        internal static BumperCircle[] Bumpers(MushroomWallLayout layout)
        {
            return Bumpers(layout.Caps);
        }

        internal static BumperCircle[] Bumpers(IReadOnlyList<MushroomWallCap> caps)
        {
            return caps.Select((cap, index) => new BumperCircle(index, cap.Wall, SlotCenter(cap.Slot), ContactRadius))
                .ToArray();
        }

        /// <summary>The aim grid's angle at <paramref name="index" />, in degrees.</summary>
        internal static float Degrees(int index)
        {
            return AimAngleGrid.ResolveSweepAngle(index, ArcMin, ArcMax, AimStepDegrees, 1);
        }

        /// <summary>The unit direction of the aim grid's angle at <paramref name="index" />.</summary>
        internal static Vector2 Direction(int index)
        {
            return ShotBoardGather.DirectionFromDegrees(Degrees(index));
        }

        /// <summary>Where a ball aimed along <paramref name="direction" /> leaves the cannon.</summary>
        internal static Vector2 Muzzle(Vector2 direction)
        {
            return CannonPivot + (direction * MuzzleAhead);
        }

        /// <summary>
        ///     The aim grid's angles on which the layout arms the lance, flown by the shot simulator on a board
        ///     with nothing else on it: true at an index when that angle arms.
        /// </summary>
        internal static bool[] ArmingAngles(MushroomWallLayout layout, IMushroomSettings settings)
        {
            var layer = new ShotBumperLayer(Bumpers(layout), new ForgivingKickRule(settings), settings.Rules, 1.6f);
            var balloons = new[] { ShotBalloonSnapshot.ForToughTarget(new Vector2(1000f, 1000f), 0.1f, 0, int.MaxValue) };
            var workingSet = new ShotBalloonState[balloons.Length];
            var arms = new bool[Angles];
            for (var i = 0; i < arms.Length; i++)
            {
                var direction = Direction(i);
                var result = ShotSimulator.Simulate(
                    balloons, WallsClockwise, Muzzle(direction), direction, StartingShields, BallRadius, workingSet,
                    rippleDamage: 1, streakGrantsShields: false, bumpers: layer);
                arms[i] = result.Mushrooms.LancesArmed > 0;
            }

            return arms;
        }

        /// <summary>
        ///     The middle angle of the layout's widest window: the longest unbroken run of aim angles that arm the
        ///     lance. It is the shot a player aiming into the window makes, with arming angles either side of it.
        /// </summary>
        internal static int MiddleOfTheWidestWindow(MushroomWallLayout layout, IMushroomSettings settings)
        {
            var arms = ArmingAngles(layout, settings);
            var best = 0;
            var bestStart = 0;
            var run = 0;
            for (var i = 0; i < arms.Length; i++)
            {
                run = arms[i] ? run + 1 : 0;
                if (run > best)
                {
                    best = run;
                    bestStart = i - run + 1;
                }
            }

            return bestStart + (best / 2);
        }

        internal static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var along = to - from;
            var length = along.magnitude;
            if (length < 1e-6f)
            {
                return Vector2.Distance(point, from);
            }

            var unit = along / length;
            var travelled = Vector2.Dot(point - from, unit);
            if (travelled <= 0f)
            {
                return Vector2.Distance(point, from);
            }

            if (travelled >= length)
            {
                return Vector2.Distance(point, to);
            }

            return Mathf.Abs((unit.x * (point.y - from.y)) - (unit.y * (point.x - from.x)));
        }
    }
}

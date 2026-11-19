using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BalloonParty.Configuration;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Level;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using BalloonParty.Thrower;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>What a catalogue of mushroom walls is baked on: the board, the cannon and the thrower's aim.</summary>
    internal sealed class MushroomBakeBoard
    {
        public Vector4 WallsClockwise;

        /// <summary>The thrower's pivot at rest, which every shot's line passes through.</summary>
        public Vector2 CannonPivot;

        /// <summary>How far ahead of the pivot, along the aim, the ball starts.</summary>
        public float MuzzleAhead;

        public float AimMinDegrees;
        public float AimMaxDegrees;
        public float AimStepDegrees;
        public float BallRadius;
        public int StartingShields;
        public Vector2Int GridSize;
        public Func<Vector2Int, Vector2> SlotCenter;
    }

    /// <summary>What one layout measured: of <see cref="Angles" /> aim angles, how many arm the lance, and the
    /// longest unbroken run of them.</summary>
    internal readonly struct MushroomWallMeasure
    {
        public readonly int ArmingAngles;
        public readonly int WidestWindowAngles;
        public readonly int Angles;

        public MushroomWallMeasure(int armingAngles, int widestWindowAngles, int angles)
        {
            ArmingAngles = armingAngles;
            WidestWindowAngles = widestWindowAngles;
            Angles = angles;
        }
    }

    /// <summary>How a layout stands to the cannon at rest: how near its nearest cap is, and on how many of
    /// <see cref="Angles" /> aim angles a shot's first straight leg meets a cap within the near range.</summary>
    internal readonly struct MushroomCannonView
    {
        public readonly float NearestCap;
        public readonly int ShadowedAngles;
        public readonly int Angles;

        public MushroomCannonView(float nearestCap, int shadowedAngles, int angles)
        {
            NearestCap = nearestCap;
            ShadowedAngles = shadowedAngles;
            Angles = angles;
        }
    }

    /// <summary>What a bake found for one count of walls.</summary>
    internal sealed class MushroomBakeCount
    {
        public int Walls;

        /// <summary>Different boards drawn to the limits.</summary>
        public int Drawn;

        /// <summary>Of those, the ones that leave the cannon its room, which are the ones flown.</summary>
        public int Candidates;

        public int Accepted;
        public readonly List<MushroomWallLayout> Layouts = new();

        /// <summary>What every candidate flown measured, for the distribution a band is chosen from, and how each
        /// stands to the cannon.</summary>
        public readonly List<MushroomWallMeasure> Measured = new();

        public readonly List<MushroomCannonView> Views = new();

        /// <summary>The caps of every candidate flown, in the order of <see cref="Measured" />.</summary>
        public readonly List<MushroomWallCap[]> Boards = new();
    }

    /// <summary>
    ///     Bakes the catalogue a meadow level's mushroom walls are dealt from
    ///     (<see cref="MushroomWallCatalogue" />): draws candidate boards to the placement limits, turns away the
    ///     ones that crowd the cannon (<see cref="CannonViewOf" />), flies each of the rest from every angle the
    ///     thrower can aim at with the shot simulator, and keeps the ones inside the settings' band. A level then
    ///     picks a layout and flies nothing, which is why this is done here and not at the deal.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A layout is kept by what it measures, not by its shape: walls that send the ball to each other
    ///         arm on more angles and in wider windows than walls that do not, and the band is what tells them
    ///         apart. A candidate is kept only when it is inside the band with room to spare
    ///         (<see cref="ArmingMarginAngles" />, <see cref="WindowMarginAngles" />). The bake, the Editor's tests
    ///         and the game on a device run on three runtimes that round a flight differently, and a ball that
    ///         grazes a cap on one misses it on another, so a layout measures a few angles apart between them:
    ///         the room is wider than the drift the tests allow an entry, so one that drifts as far as allowed
    ///         is still inside the band.
    ///     </para>
    ///     <para>
    ///         The same <see cref="Measure" /> flies every entry again in an EditMode test, against what ships.
    ///     </para>
    /// </remarks>
    internal static class MushroomWallBaker
    {
        internal const string GeneratedPath = "Assets/Source/Slots/Actor/MushroomWallCatalogue.Generated.cs";

        // How far inside the band a candidate must be to be kept. Arming angles: this many over the least the band
        // asks (its share and its floor) and this many under the most. The widest window: this many angles over
        // its floor. Between the bake's runtime and the Editor's a layout was measured up to 3 arming angles apart
        // and no angle of window; the tests allow 4 and 1.
        internal const int ArmingMarginAngles = 8;
        internal const int WindowMarginAngles = 2;

        private const string MenuPath = "Tools/BalloonParty/Bake Mushroom Wall Catalogue";
        private const string ThrowerPrefabPath = "Assets/Prefabs/Thrower/Thrower.prefab";
        private const string ProjectilePrefabPath = "Assets/Prefabs/Projectile/Projectile.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string FlightConfigPath = "Assets/Configuration/ProjectileFlightConfig.asset";
        private const string GridConfigPath = "Assets/Configuration/SlotGridConfig.asset";
        private const string LevelPacingPath = "Assets/Configuration/LevelPacingConfiguration.asset";

        // Layouts kept for each count of walls, and candidate boards flown to find them.
        private const int LayoutsPerCount = 64;
        private const int CandidatesPerCount = 6000;
        private const int BakeSeed = 20261119;

        // A slot's neighbour stands one slot's width away, to a rounding error.
        private const float SlotTolerance = 0.01f;

        /// <summary>The board as it ships: the thrower's place in the Game scene and its prefab's entrance and
        /// muzzle, the ball's collider, the flight config's walls and aim grid, and the grid's slots.</summary>
        internal static MushroomBakeBoard ShippedBoard()
        {
            var thrower = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowerPrefabPath).GetComponent<ThrowerView>();
            var entrance = thrower.PositionAfterEntrance - thrower.transform.position;
            var muzzle = thrower.transform.InverseTransformPoint(thrower.SpawnPointPosition);
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath).GetComponent<ProjectileView>();
            var collider = projectile.GetComponent<Collider2D>();
            var flight = AssetDatabase.LoadAssetAtPath<ProjectileFlightConfig>(FlightConfigPath);
            var grid = AssetDatabase.LoadAssetAtPath<SlotGridConfig>(GridConfigPath);
            var separation = grid.SlotSeparation;
            var offset = grid.SlotsOffset;
            return new MushroomBakeBoard
            {
                WallsClockwise = flight.LimitsClockwise,
                CannonPivot = ThrowerPlaceInTheGameScene() + (Vector2)entrance,
                MuzzleAhead = muzzle.y + collider.offset.magnitude,
                AimMinDegrees = flight.AimAngleMinDegrees,
                AimMaxDegrees = flight.AimAngleMaxDegrees,
                AimStepDegrees = flight.AimAngleStepDegrees,
                BallRadius = ContactRadius.FromCollider(collider, projectile.transform.lossyScale.x),
                StartingShields = flight.ProjectileStartingShields,
                GridSize = grid.SlotsSize,
                SlotCenter = slot => HexCoordinates.IndexToWorldPosition(slot, separation, offset),
            };
        }

        /// <summary>
        ///     Every number a bake depends on, in one line: the catalogue keeps the line it was baked with
        ///     (<see cref="MushroomWallCatalogue.BakedWith" />), and a test holds it against what ships.
        /// </summary>
        internal static string Stamp(MushroomBakeBoard board, IMushroomSettings settings)
        {
            var limits = settings.Placement;
            var band = limits.Band;
            var corner = new Vector2Int(board.GridSize.x - 1, board.GridSize.y - 1);
            return string.Join("; ", new[]
            {
                "walls " + Text(board.WallsClockwise.x, board.WallsClockwise.y, board.WallsClockwise.z, board.WallsClockwise.w),
                "pivot " + Text(board.CannonPivot.x, board.CannonPivot.y),
                "muzzle " + Text(board.MuzzleAhead),
                "aim " + Text(board.AimMinDegrees, board.AimMaxDegrees, board.AimStepDegrees),
                "ball " + Text(board.BallRadius),
                "shields " + board.StartingShields,
                "grid " + board.GridSize.x + " " + board.GridSize.y,
                "slots " + Text(
                    board.SlotCenter(Vector2Int.zero).x, board.SlotCenter(Vector2Int.zero).y,
                    board.SlotCenter(corner).x, board.SlotCenter(corner).y),
                "cap " + Text(settings.Radius),
                "kick " + (settings.ForgivingKick ? "on " : "off ")
                    + Text(settings.Kick.ConeDegrees, settings.Kick.MinOffset, settings.Kick.MaxOffset),
                "chain " + settings.Rules.ChainTarget + " " + settings.Rules.MaxBouncesPerStretch
                    + (settings.Rules.RepeatHitCounts ? " repeats" : string.Empty),
                "count " + limits.MinCount + " " + limits.MaxCount,
                "caps " + limits.MinCaps + " " + limits.MaxCaps,
                "spacing " + Text(limits.MinSpacing),
                "clear " + limits.EdgeColumnsKeptClear + " " + limits.TopRowsKeptClear + " " + limits.BottomRowsKeptClear,
                "band " + Text(band.MinArmingShare, band.MaxArmingShare, band.MinWindowDegrees) + " " + band.MinArmingAngles,
                "cannon " + Text(limits.Cannon.MinDistance, limits.Cannon.NearRange, limits.Cannon.MaxShadowShare),
            });
        }

        /// <summary>
        ///     Flies the caps from every angle of the board's aim grid on a board with nothing else on it, with
        ///     the game's own bounce, kick and count, and says on how many the lance is armed.
        /// </summary>
        internal static MushroomWallMeasure Measure(
            IReadOnlyList<MushroomWallCap> caps, MushroomBakeBoard board, IMushroomSettings settings)
        {
            var contactRadius = settings.Radius + board.BallRadius;
            var circles = new BumperCircle[caps.Count];
            for (var i = 0; i < caps.Count; i++)
            {
                circles[i] = new BumperCircle(i, caps[i].Wall, board.SlotCenter(caps[i].Slot), contactRadius);
            }

            // One target out of every ball's reach keeps the board from counting as cleared, where the simulator
            // stops a flight.
            var balloons = new[] { ShotBalloonSnapshot.ForToughTarget(new Vector2(1000f, 1000f), 0.1f, 0, int.MaxValue) };
            var workingSet = new ShotBalloonState[balloons.Length];
            var layer = new ShotBumperLayer(circles, new ForgivingKickRule(settings), settings.Rules, 1f);
            var angles = AimAngleGrid.ResolveSweepSampleCount(
                board.AimMinDegrees, board.AimMaxDegrees, board.AimStepDegrees, 1);
            var arming = 0;
            var widest = 0;
            var run = 0;
            for (var i = 0; i < angles; i++)
            {
                var degrees = AimAngleGrid.ResolveSweepAngle(
                    i, board.AimMinDegrees, board.AimMaxDegrees, board.AimStepDegrees, 1);
                var direction = ShotBoardGather.DirectionFromDegrees(degrees);
                var result = ShotSimulator.Simulate(
                    balloons, board.WallsClockwise, board.CannonPivot + (direction * board.MuzzleAhead), direction,
                    board.StartingShields, board.BallRadius, workingSet, rippleDamage: 1, streakGrantsShields: false,
                    bumpers: layer);
                run = result.Mushrooms.LancesArmed > 0 ? run + 1 : 0;
                arming += result.Mushrooms.LancesArmed > 0 ? 1 : 0;
                widest = Mathf.Max(widest, run);
            }

            return new MushroomWallMeasure(arming, widest, angles);
        }

        /// <summary>
        ///     How the caps stand to the cannon at rest: the distance from its pivot to the nearest cap's centre,
        ///     and on how many angles of the aim grid a ball's first straight leg from the muzzle meets a cap
        ///     within the placement's near range. No flight is flown: it is the cannon's view of the board.
        /// </summary>
        internal static MushroomCannonView CannonViewOf(
            IReadOnlyList<MushroomWallCap> caps, MushroomBakeBoard board, IMushroomSettings settings)
        {
            var contactRadius = settings.Radius + board.BallRadius;
            var nearRange = settings.Placement.Cannon.NearRange;
            var circles = new BumperCircle[caps.Count];
            var nearest = float.MaxValue;
            for (var i = 0; i < caps.Count; i++)
            {
                circles[i] = new BumperCircle(i, caps[i].Wall, board.SlotCenter(caps[i].Slot), contactRadius);
                nearest = Mathf.Min(nearest, Vector2.Distance(board.CannonPivot, circles[i].Center));
            }

            var angles = AimAngleGrid.ResolveSweepSampleCount(
                board.AimMinDegrees, board.AimMaxDegrees, board.AimStepDegrees, 1);
            var shadowed = 0;
            for (var i = 0; i < angles; i++)
            {
                var direction = ShotBoardGather.DirectionFromDegrees(AimAngleGrid.ResolveSweepAngle(
                    i, board.AimMinDegrees, board.AimMaxDegrees, board.AimStepDegrees, 1));
                shadowed += BumperSweep.TryFindFirst(
                    circles, board.CannonPivot + (direction * board.MuzzleAhead), direction, nearRange,
                    MushroomChainState.NoBumper, out _, out _) ? 1 : 0;
            }

            return new MushroomCannonView(nearest, shadowed, angles);
        }

        /// <summary>
        ///     Whether what a layout measured is inside the band with the bake's room to spare.
        /// </summary>
        internal static bool Keeps(in MushroomWallMeasure measure, in MushroomLayoutBand band, float stepDegrees)
        {
            return band.Accepts(
                    measure.ArmingAngles - ArmingMarginAngles, measure.WidestWindowAngles - WindowMarginAngles,
                    measure.Angles, stepDegrees)
                && band.Accepts(
                    measure.ArmingAngles + ArmingMarginAngles, measure.WidestWindowAngles, measure.Angles, stepDegrees);
        }

        /// <summary>
        ///     For each count of walls the settings allow: draws up to <paramref name="candidates" /> different
        ///     boards to the placement limits, measures each, and keeps up to <paramref name="perCount" /> of the
        ///     ones inside the band, no two of them the same board but for one cap.
        /// </summary>
        internal static List<MushroomBakeCount> Bake(
            MushroomBakeBoard board, IMushroomSettings settings, int perCount, int candidates, int seed)
        {
            var limits = settings.Placement;
            var usable = new List<Vector2Int>();
            for (var column = 0; column < board.GridSize.x; column++)
            {
                for (var row = 0; row < board.GridSize.y; row++)
                {
                    var slot = new Vector2Int(column, row);
                    if (limits.Allows(slot, board.GridSize))
                    {
                        usable.Add(slot);
                    }
                }
            }

            var walls = WallsOnTheBoard(board, usable);
            var spacing = limits.EffectiveMinSpacing(settings.Radius + board.BallRadius) - 1e-3f;
            var counts = new List<MushroomBakeCount>();
            for (var count = limits.MinCount; count <= limits.MaxCount; count++)
            {
                counts.Add(BakeCount(count, board, settings, walls, spacing, perCount, candidates, seed + count));
            }

            return counts;
        }

        /// <summary>The generated half of <see cref="MushroomWallCatalogue" />, as text.</summary>
        internal static string Emit(
            IReadOnlyList<MushroomBakeCount> counts, string stamp, MushroomBakeBoard board, IMushroomSettings settings)
        {
            var text = new StringBuilder();
            text.Append("// Generated by MushroomWallBaker (" + MenuPath + "). Do not edit by hand: bake again.\n");
            text.Append("namespace BalloonParty.Slots.Actor\n{\n");
            text.Append("    internal static partial class MushroomWallCatalogue\n    {\n");
            text.Append("        /// <summary>Every number the entries were baked with: the board, the cannon, the aim grid and the\n");
            text.Append("        /// mushroom settings. A test holds it against what ships.</summary>\n");
            text.Append("        internal const string BakedWith =\n            \"" + stamp + "\";\n\n");
            text.Append("        private const int FirstWalls = " + (counts.Count > 0 ? counts[0].Walls : 0) + ";\n\n");
            text.Append("        // An entry: the aim angles that arm the lance, the widest run of them, then its caps, each as\n");
            text.Append("        // 0xWCR: its wall, its column, its row.\n");
            text.Append("        private static readonly int[][][] Entries =\n        {\n");
            foreach (var count in counts)
            {
                text.Append("            // " + count.Walls + " walls: " + count.Layouts.Count + " layouts, of "
                    + count.Accepted + " well inside the band among " + count.Candidates + " boards flown ("
                    + count.Drawn + " drawn, the rest too near the cannon or too broad in front of it).\n");
                text.Append("            new[]\n            {\n");
                foreach (var layout in count.Layouts)
                {
                    text.Append("                new[] { " + layout.ArmingAngles + ", " + layout.WidestWindowAngles);
                    foreach (var cap in layout.Caps)
                    {
                        text.Append(", 0x" + MushroomWallCatalogue.Pack(cap).ToString("X3", CultureInfo.InvariantCulture));
                    }

                    text.Append(" },\n");
                }

                text.Append("            },\n");
            }

            text.Append("        };\n    }\n}\n");
            return text.ToString();
        }

        [MenuItem(MenuPath)]
        private static void BakeFromWhatShips()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LevelPacingConfiguration>(LevelPacingPath).Mushrooms;
            var board = ShippedBoard();
            var counts = Bake(board, settings, LayoutsPerCount, CandidatesPerCount, BakeSeed);
            File.WriteAllText(GeneratedPath, Emit(counts, Stamp(board, settings), board, settings));
            AssetDatabase.Refresh();
            foreach (var count in counts)
            {
                Debug.Log($"Mushroom walls, {count.Walls} walls: {count.Accepted} of {count.Candidates} candidate " +
                    $"boards inside the band, {count.Layouts.Count} kept.");
            }
        }

        private static MushroomBakeCount BakeCount(
            int count, MushroomBakeBoard board, IMushroomSettings settings, List<Vector2Int[]>[] walls, float spacing,
            int perCount, int candidates, int seed)
        {
            var limits = settings.Placement;
            var lengths = WallLengthsFor(count, limits);
            var result = new MushroomBakeCount { Walls = count };
            var random = new XorShift32(seed);
            var seen = new HashSet<string>();
            var accepted = new List<MushroomWallLayout>();
            if (lengths.Count == 0)
            {
                return result;
            }

            for (var attempt = 0; attempt < candidates * 40 && result.Candidates < candidates; attempt++)
            {
                var caps = DrawBoard(lengths[random.Next(lengths.Count)], walls, board, spacing, ref random);
                if (caps == null || !seen.Add(KeyOf(caps)))
                {
                    continue;
                }

                // The cannon's room is geometry, so it is asked before the board is flown.
                result.Drawn++;
                var view = CannonViewOf(caps, board, settings);
                if (!limits.Cannon.Allows(view.NearestCap, view.ShadowedAngles, view.Angles))
                {
                    continue;
                }

                result.Candidates++;
                var measure = Measure(caps, board, settings);
                result.Measured.Add(measure);
                result.Views.Add(view);
                result.Boards.Add(caps);
                if (Keeps(measure, limits.Band, board.AimStepDegrees))
                {
                    accepted.Add(new MushroomWallLayout(caps, measure.ArmingAngles, measure.WidestWindowAngles));
                }
            }

            result.Accepted = accepted.Count;

            // First the ones that differ from every one kept by two caps or more, then whatever is left.
            for (var pass = 0; pass < 2 && result.Layouts.Count < perCount; pass++)
            {
                foreach (var layout in accepted)
                {
                    if (result.Layouts.Count < perCount && !result.Layouts.Contains(layout)
                        && (pass == 1 || result.Layouts.All(kept => SharedSlots(kept, layout) < layout.Caps.Count - 1)))
                    {
                        result.Layouts.Add(layout);
                    }
                }
            }

            return result;
        }

        // The ways a count of walls may be made of walls one to three caps long, within the limits' caps: the
        // ones with the fewest walls of a single cap, since a single is only there to fill out a board.
        private static List<int[]> WallLengthsFor(int count, in MushroomPlacementLimits limits)
        {
            var all = new List<int[]>();
            Extend(new int[count], 0, MushroomPlacementLimits.LongestWall, all);
            var fewest = limits.MinCaps;
            var most = limits.MaxCapsFor(count);
            var within = all.Where(lengths => lengths.Sum() >= fewest && lengths.Sum() <= most).ToList();
            if (within.Count == 0)
            {
                return within;
            }

            var fewestSingles = within.Min(lengths => lengths.Count(length => length == 1));
            return within.Where(lengths => lengths.Count(length => length == 1) == fewestSingles).ToList();
        }

        // Every non-increasing run of lengths, so each mix of walls is listed once.
        private static void Extend(int[] lengths, int at, int longest, List<int[]> all)
        {
            if (at == lengths.Length)
            {
                all.Add((int[])lengths.Clone());
                return;
            }

            for (var length = longest; length >= 1; length--)
            {
                lengths[at] = length;
                Extend(lengths, at + 1, length, all);
            }
        }

        // Every wall of one, two and three caps that stands on usable slots, by its length: caps in neighbouring
        // slots, in a line along a row or along either diagonal of the lattice.
        private static List<Vector2Int[]>[] WallsOnTheBoard(MushroomBakeBoard board, List<Vector2Int> usable)
        {
            var pitch = Mathf.Abs(board.SlotCenter(new Vector2Int(1, 0)).x - board.SlotCenter(Vector2Int.zero).x);
            var rise = Mathf.Abs(board.SlotCenter(new Vector2Int(0, 1)).y - board.SlotCenter(Vector2Int.zero).y);
            var directions = new[] { new Vector2(pitch, 0f), new Vector2(0.5f * pitch, rise), new Vector2(-0.5f * pitch, rise) };
            var walls = new List<Vector2Int[]>[MushroomPlacementLimits.LongestWall + 1];
            for (var length = 1; length <= MushroomPlacementLimits.LongestWall; length++)
            {
                walls[length] = new List<Vector2Int[]>();
                foreach (var anchor in usable)
                {
                    foreach (var direction in directions)
                    {
                        var wall = new Vector2Int[length];
                        var stands = true;
                        for (var k = 0; k < length && stands; k++)
                        {
                            stands = TryFindSlot(board, usable, board.SlotCenter(anchor) + (direction * k), out wall[k]);
                        }

                        if (stands)
                        {
                            walls[length].Add(wall);
                        }

                        // A single cap has no direction.
                        if (length == 1)
                        {
                            break;
                        }
                    }
                }
            }

            return walls;
        }

        private static bool TryFindSlot(MushroomBakeBoard board, List<Vector2Int> usable, Vector2 point, out Vector2Int slot)
        {
            foreach (var candidate in usable)
            {
                if (Vector2.Distance(board.SlotCenter(candidate), point) < SlotTolerance)
                {
                    slot = candidate;
                    return true;
                }
            }

            slot = default;
            return false;
        }

        // One board of walls of these lengths, the longest first, each drawn from the walls that stand the
        // spacing or more from the ones already drawn. Null when a wall has nowhere left to stand.
        private static MushroomWallCap[] DrawBoard(
            int[] lengths, List<Vector2Int[]>[] walls, MushroomBakeBoard board, float spacing, ref XorShift32 random)
        {
            var caps = new List<MushroomWallCap>();
            var fitting = new List<Vector2Int[]>();
            for (var wall = 0; wall < lengths.Length; wall++)
            {
                fitting.Clear();
                foreach (var choice in walls[lengths[wall]])
                {
                    if (StandsApart(choice, caps, board, spacing))
                    {
                        fitting.Add(choice);
                    }
                }

                if (fitting.Count == 0)
                {
                    return null;
                }

                foreach (var slot in fitting[random.Next(fitting.Count)])
                {
                    caps.Add(new MushroomWallCap(slot, wall));
                }
            }

            return caps.ToArray();
        }

        private static bool StandsApart(
            Vector2Int[] wall, List<MushroomWallCap> caps, MushroomBakeBoard board, float spacing)
        {
            foreach (var slot in wall)
            {
                foreach (var other in caps)
                {
                    if (Vector2.Distance(board.SlotCenter(other.Slot), board.SlotCenter(slot)) < spacing)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // The same walls in another order are the same board.
        private static string KeyOf(MushroomWallCap[] caps)
        {
            return string.Join("|", caps.GroupBy(cap => cap.Wall)
                .Select(wall => string.Join(",", wall.Select(cap => (cap.Slot.x * 16) + cap.Slot.y).OrderBy(slot => slot)))
                .OrderBy(wall => wall, StringComparer.Ordinal));
        }

        private static int SharedSlots(MushroomWallLayout a, MushroomWallLayout b)
        {
            var shared = 0;
            foreach (var cap in a.Caps)
            {
                shared += b.Caps.Any(other => other.Slot == cap.Slot) ? 1 : 0;
            }

            return shared;
        }

        private static string Text(params float[] values)
        {
            return string.Join(" ", values.Select(value => value.ToString("0.####", CultureInfo.InvariantCulture)));
        }

        // Where the Game scene puts the thrower: the position its prefab instance is given there, read from the
        // scene's own text so the scene the Editor has open is left alone.
        private static Vector2 ThrowerPlaceInTheGameScene()
        {
            var guid = AssetDatabase.AssetPathToGUID(ThrowerPrefabPath);
            var scene = File.ReadAllText(GameScenePath);
            return new Vector2(Override(scene, guid, "x"), Override(scene, guid, "y"));
        }

        private static float Override(string scene, string prefabGuid, string axis)
        {
            var match = Regex.Match(
                scene,
                "guid: " + prefabGuid + @", type: 3\}\s+propertyPath: m_LocalPosition\." + axis + @"\s+value: (\S+)");
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    $"The catalogue cannot be baked: {GameScenePath} does not place the thrower prefab " +
                    $"({ThrowerPrefabPath}) with an m_LocalPosition.{axis} of its own, which is where the cannon's " +
                    "resting pivot is read from.");
            }

            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}

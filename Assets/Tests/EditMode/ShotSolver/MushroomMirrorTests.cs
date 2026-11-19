using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using NUnit.Framework;
using UnityEngine;
using Board = BalloonParty.Tests.Slots.MushroomTestBoard;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     The mushroom mirror (phase 5 of <c>Plans/PLAN-MeadowMushrooms.md</c>): the same shot flown by the
    ///     game's code (<see cref="MushroomLiveGame" />: the real motion resolver in fixed steps, the real chain
    ///     controller and Snipe handler) and by the shot simulator (<see cref="MushroomSimGame" />), held against
    ///     each other turn for turn. Both must bounce off the same mushrooms at the same points, count the chain
    ///     alike, arm the lance on the same bounce and leave every mushroom a lance or not alike, turn at the
    ///     same walls and deflect off the same balloons, fly a lance straight through the mushrooms and cut the
    ///     same caps with it, and end the same way.
    /// </summary>
    /// <remarks>
    ///     The first tests are hand-built, one for each thing the chain can do; the last flies seeded boards of
    ///     the shipped grid from angles across the aim arc. The two sides share the pure rules by design
    ///     (<see cref="BumperSweep" />, <see cref="MushroomChainRule" />, the bounce rule): what the mirror holds
    ///     is that they call them at the same moments, in the same order, with the same state.
    /// </remarks>
    [TestFixture]
    public class MushroomMirrorTests
    {
        // A 10-wide box centred on the origin, as the resolver's own tests use.
        private static readonly Vector4 Box = new(5f, 5f, -5f, -5f);

        // The thrower's pivot at rest, which every shot's line passes through: the scene's (0, -5.1) and the
        // half unit its entrance lifts it.
        private static readonly Vector2 Cannon = new(0f, -4.6f);

        private static readonly BumperCircle Above = new(0, new Vector2(0f, 2f), 0.5f);
        private static readonly BumperCircle Below = new(1, new Vector2(0f, -2f), 0.5f);
        private static readonly MirrorTarget[] NoTargets = new MirrorTarget[0];

        [Test]
        public void TwoFacingMushrooms_TheThirdBounceArms_AndTheLanceCutsBothCaps_OnBothSides()
        {
            // Straight up between two facing mushrooms: every bounce comes straight back. The third arms the
            // ball, and from there it is a lance: down through the lower cap to the floor, and back up through
            // the cap that armed it to the wall it dies at.
            var settings = new MushroomSettings();
            var (game, sim) = Fly(Box, new[] { Above, Below }, settings, new TrueReflectRule(), Vector2.zero,
                Vector2.up, 1, false, NoTargets);

            AssertMirrors(game, sim, "two facing mushrooms");
            CollectionAssert.AreEqual(
                new[]
                {
                    MirrorTurnKind.Bumper, MirrorTurnKind.Bumper, MirrorTurnKind.Bumper, MirrorTurnKind.Wall,
                    MirrorTurnKind.Wall,
                },
                sim.Turns.ConvertAll(turn => turn.Kind), "three bounces, then walls alone");
            Assert.AreEqual(1, sim.LancesArmed);
            Assert.AreEqual(MushroomReward.Arm, sim.Turns[2].Reward, "the third bounce");
            Assert.IsTrue(sim.Turns[2].LeavesPiercing, "a lance from the third bounce on");
            Assert.IsFalse(sim.Turns[1].LeavesPiercing, "and not before it");
            CollectionAssert.AreEqual(new[] { Below.Id, Above.Id }, game.CapsCut, "the game cut both, lower first");
            CollectionAssert.AreEqual(game.CapsCut, sim.CapsCut, "and so did the simulator");
            Assert.AreEqual(2, game.ChargesBanked, "a charge banked for each cap, by the lance the chain made");
            Assert.AreEqual(2, sim.ChargesBanked, "on the same cuts in the simulator");
        }

        // A ball loaded as a lance (the Sharpshooter card; a Snipe pickup's is the same ball) bounces off no
        // mushroom: it flies up through the upper cap, off the wall and down through the lower one, and no chain is
        // counted for it.
        [Test]
        public void ABallLoadedAsALance_CutsTheCapsInItsWay_BouncesOffNone_AndCountsNoChain_OnBothSides()
        {
            var settings = new MushroomSettings();
            var (game, sim) = Fly(Box, new[] { Above, Below }, settings, new TrueReflectRule(), Vector2.zero,
                Vector2.up, 1, true, NoTargets);

            AssertMirrors(game, sim, "a loaded lance");
            Assert.AreEqual(2, sim.Turns.Count, "the top wall, and the floor it dies at");
            Assert.IsTrue(sim.Turns.TrueForAll(turn => turn.Kind == MirrorTurnKind.Wall));
            Assert.AreEqual(5f, sim.Turns[0].Point.y, 1e-3f);
            Assert.AreEqual(-5f, sim.Turns[1].Point.y, 1e-3f);
            CollectionAssert.AreEqual(new[] { Above.Id, Below.Id }, game.CapsCut);
            CollectionAssert.AreEqual(game.CapsCut, sim.CapsCut);
            Assert.AreEqual(0, sim.LancesArmed + sim.ChainsCompleted);
            Assert.AreEqual(2, game.ChargesBanked, "a lance no chain armed banks a charge a cap all the same");
            Assert.AreEqual(2, sim.ChargesBanked);
        }

        // A cap cut by one flight is gone for the next: an ordinary ball flown over the board as the lance left
        // it flies through where the upper cap stood to the wall behind it, and bounces off the lower one, which
        // is whole. The simulator is handed the board as the game's field has it, as a gather would.
        [Test]
        public void ACapCutByAnEarlierFlight_IsAbsentForTheNextBall_OnBothSides()
        {
            var settings = new MushroomSettings();
            var rule = new TrueReflectRule();
            var live = new MushroomLiveGame(Box, new[] { Above, Below }, settings, rule);
            var lance = live.Fly(Vector2.zero, Vector2.up, 0, true, NoTargets);
            CollectionAssert.AreEqual(new[] { Above.Id }, lance.CapsCut, "the lance cut the upper cap and died at the wall");
            Assert.AreEqual(1, live.WholeCaps.Count);

            var game = live.Fly(Vector2.zero, Vector2.up, 2, false, NoTargets, onTheBoardAsLeft: true);
            var sim = new MushroomSimGame(Box, live.WholeCaps, settings, rule).Fly(
                Vector2.zero, Vector2.up, 2, false, NoTargets);

            AssertMirrors(game, sim, "the ball after the lance");
            CollectionAssert.AreEqual(
                new[]
                {
                    MirrorTurnKind.Wall, MirrorTurnKind.Bumper, MirrorTurnKind.Wall, MirrorTurnKind.Bumper,
                    MirrorTurnKind.Wall,
                },
                sim.Turns.ConvertAll(turn => turn.Kind));
            Assert.AreEqual(5f, sim.Turns[0].Point.y, 1e-3f, "through the stem to the wall behind it");
            Assert.IsTrue(
                sim.Turns.TrueForAll(turn => turn.Kind != MirrorTurnKind.Bumper || turn.BumperId == Below.Id),
                "every bounce is off the cap that is whole");

            // And with every cap whole again the same ball bounces off the upper one first.
            var whole = live.Fly(Vector2.zero, Vector2.up, 2, false, NoTargets);
            Assert.AreEqual(Above.Id, whole.Turns[0].BumperId);
        }

        [Test]
        public void TwoFacingCapsOfOneWall_TradeTheBallAndPayNothing_OnBothSides()
        {
            // The same two caps as above, told to be one wall: every bounce is on the wall hit last, so the count
            // stays at one, nothing is paid, and the cap on a stretch's bounces lets the ball through to a wall.
            var settings = new MushroomSettings();
            var bumpers = new[]
            {
                new BumperCircle(0, 0, Above.Center, Above.Radius), new BumperCircle(1, 0, Below.Center, Below.Radius),
            };
            var (game, sim) = Fly(Box, bumpers, settings, new ForgivingKickRule(settings), Vector2.zero, Vector2.up, 1,
                false, NoTargets);

            AssertMirrors(game, sim, "two facing caps of one wall");
            var cap = settings.Rules.MaxBouncesPerStretch;
            Assert.AreEqual(MirrorTurnKind.Wall, game.Turns[cap].Kind, "the stretch's bounces, then a wall");
            for (var i = 0; i < cap; i++)
            {
                Assert.AreEqual(i % 2, game.Turns[i].BumperId, "each bounce names the cap that was hit");
                Assert.AreEqual(1, game.Turns[i].Count, "and the wall counts once");
                Assert.AreEqual(MushroomReward.None, game.Turns[i].Reward);
            }

            Assert.AreEqual(0, sim.LancesArmed);
        }

        // The game and the simulator are handed the same contact: a ball flown from the same point along the same
        // line onto the same cap of two walls of two caps that face each other. Both must count the hit alike,
        // send the ball on to the same cap, and land it at the same point of that cap. From between the two walls
        // the ball is kicked across the gap; a little to either side of the notch where two caps of a wall meet
        // (on the notch itself rounding alone picks the cap) it is bounced into the wall's other cap, with no
        // kick, since a wall never hands the ball along itself.
        [TestCase(-0.375f, false)]
        [TestCase(-0.325f, false)]
        [TestCase(0.3f, false)]
        [TestCase(0.4f, false)]
        [TestCase(-0.03f, true)]
        [TestCase(-0.004f, true)]
        [TestCase(0.004f, true)]
        [TestCase(0.03f, true)]
        public void TheSameContact_IsBentTowardTheSameCap_ByTheGameAndTheSimulator(float startX, bool atTheNotch)
        {
            var settings = new MushroomSettings();
            var caps = new[]
            {
                new BumperCircle(0, 0, new Vector2(-0.375f, 0f), Board.ContactRadius),
                new BumperCircle(1, 0, new Vector2(0.375f, 0f), Board.ContactRadius),
                new BumperCircle(2, 1, new Vector2(-0.375f, 1.95f), Board.ContactRadius),
                new BumperCircle(3, 1, new Vector2(0.375f, 1.95f), Board.ContactRadius),
            };

            // Straight down from the middle of the gap, onto the lower wall.
            var (game, sim) = Fly(Box, caps, settings, new ForgivingKickRule(settings), new Vector2(startX, 0.975f),
                Vector2.down, 0, false, NoTargets);

            // Whatever the flight does later (some of these leave for a corner of the box, which the harness
            // cannot place), its first turns are the contact and where the ball was sent from it.
            Assert.GreaterOrEqual(game.Turns.Count, 3, "a cap, the cap the ball is sent on to, and what follows");
            Assert.GreaterOrEqual(sim.Turns.Count, 3);
            for (var turn = 0; turn < 3; turn++)
            {
                Assert.AreEqual(game.Turns[turn].Kind, sim.Turns[turn].Kind, $"turn {turn}: {game.Turns[turn]}");
                Assert.AreEqual(game.Turns[turn].BumperId, sim.Turns[turn].BumperId, $"turn {turn}: the same cap");
                Assert.AreEqual(game.Turns[turn].Count, sim.Turns[turn].Count, $"turn {turn}: the same count");
                Assert.Less(
                    Vector2.Distance(game.Turns[turn].Point, sim.Turns[turn].Point), 1e-4f,
                    $"turn {turn}: the same point, {game.Turns[turn]} against {sim.Turns[turn]}");
            }

            var first = game.Turns[0];
            var second = game.Turns[1];
            Assert.AreEqual(MirrorTurnKind.Bumper, first.Kind);
            Assert.AreEqual(startX < 0f ? 0 : 1, first.BumperId, "the cap under the ball");
            Assert.AreEqual(MirrorTurnKind.Bumper, second.Kind, "and it is sent on to a cap");
            if (atTheNotch)
            {
                Assert.AreEqual(caps[first.BumperId].Wall, caps[second.BumperId].Wall, "the wall's other cap");
                Assert.AreNotEqual(first.BumperId, second.BumperId);
                Assert.AreEqual(1, second.Count, "which is the same wall, and counts nothing");
            }
            else
            {
                Assert.AreNotEqual(caps[first.BumperId].Wall, caps[second.BumperId].Wall, "a cap of the other wall");
                Assert.AreEqual(2, second.Count, "which counts");
            }
        }

        [Test]
        public void AWallBetweenBounces_ResetsTheChain_OnBothSides()
        {
            // Up into the one mushroom, straight back down to the floor, and up again: bounce, wall, bounce.
            // The mushroom hit before the wall counts again after it, and the count never passes 1.
            var settings = new MushroomSettings();
            var (game, sim) = Fly(Box, new[] { Above }, settings, new TrueReflectRule(), Vector2.zero, Vector2.up, 2,
                false, NoTargets);

            AssertMirrors(game, sim, "a wall between bounces");
            var kinds = sim.Turns.ConvertAll(turn => turn.Kind);
            CollectionAssert.AreEqual(
                new[]
                {
                    MirrorTurnKind.Bumper, MirrorTurnKind.Wall, MirrorTurnKind.Bumper, MirrorTurnKind.Wall,
                    MirrorTurnKind.Bumper, MirrorTurnKind.Wall,
                },
                kinds);
            foreach (var turn in sim.Turns)
            {
                Assert.AreEqual(turn.Kind == MirrorTurnKind.Bumper ? 1 : 0, turn.Count, "each wall starts the count again");
            }

            Assert.AreEqual(0, sim.LancesArmed);
            Assert.AreEqual(0, sim.CapsCut.Count + game.CapsCut.Count, "an ordinary ball cuts nothing");
        }

        [Test]
        public void ASpentBounceCap_LetsTheBallThroughToAWall_OnBothSides()
        {
            // Two bounces a stretch: the third meeting is flown through, on to the wall behind the mushroom,
            // and the wall gives the mushrooms back. A ball let through is no lance: it cuts nothing.
            var settings = Settings(new MushroomChainRules(chainTarget: 3, maxBouncesPerStretch: 2));
            var (game, sim) = Fly(Box, new[] { Above, Below }, settings, new TrueReflectRule(), Vector2.zero,
                Vector2.up, 2, false, NoTargets);

            AssertMirrors(game, sim, "a spent cap");
            Assert.AreEqual(MirrorTurnKind.Bumper, sim.Turns[0].Kind);
            Assert.AreEqual(MirrorTurnKind.Bumper, sim.Turns[1].Kind);
            Assert.AreEqual(MirrorTurnKind.Wall, sim.Turns[2].Kind, "through the third mushroom to the wall");
            Assert.AreEqual(5f, sim.Turns[2].Point.y, 1e-3f, "the wall behind the upper mushroom");
            Assert.AreEqual(MirrorTurnKind.Bumper, sim.Turns[3].Kind, "and past the wall they bounce it again");
            Assert.AreEqual(0, sim.LancesArmed, "two bounces a stretch never reach the target");
            Assert.AreEqual(0, sim.CapsCut.Count + game.CapsCut.Count);
        }

        [Test]
        public void AChainOfTwo_ArmsTheBall_AndTheLapItFliesNextCutsTheCapItComesTo_OnBothSides(
            [Values(false, true)] bool startsAsLance)
        {
            // Two wide mushrooms either side of the middle and a ball on the triangle between them and the
            // floor: up off the right one, across to the left one, back down to the floor, and round again the
            // other way. With a chain of two the first lap arms an ordinary ball on the left mushroom, and what
            // it flies after the floor it flies as a lance, straight through the cap it comes to. A ball loaded
            // as a lance runs through the right one on its way up.
            var settings = Settings(new MushroomChainRules(chainTarget: 2));
            var right = new BumperCircle(0, new Vector2(2.2f, -2.5f), 1.5f);
            var left = new BumperCircle(1, new Vector2(-2.2f, -2.5f), 1.5f);
            var floor = new Vector2(0f, -5f);
            var shot = (right.Center - floor).normalized;
            for (var i = 0; i < 60; i++)
            {
                // Land where the mirror reflection leaves level, toward the other mushroom.
                var landing = right.Center + ((Vector2.left - shot).normalized * right.Radius);
                shot = (landing - floor).normalized;
            }

            var (game, sim) = Fly(Box, new[] { right, left }, settings, new TrueReflectRule(), floor + (shot * 0.1f),
                shot, 1, startsAsLance, NoTargets);

            AssertMirrors(game, sim, "a chain of two");
            Assert.AreEqual(startsAsLance ? 0 : 1, sim.ChainsCompleted, "a lance completes none: it counts no hit");
            Assert.AreEqual(startsAsLance ? 0 : 1, sim.LancesArmed);
            Assert.AreEqual(
                startsAsLance ? 0 : 2, sim.Turns.FindAll(turn => turn.Kind == MirrorTurnKind.Bumper).Count,
                "no bounce once the ball is a lance");
            CollectionAssert.AreEqual(game.CapsCut, sim.CapsCut);
            Assert.Greater(sim.CapsCut.Count, 0, "the cap the lance's path enters is cut");
            if (!startsAsLance)
            {
                Assert.AreEqual(MushroomReward.Arm, sim.Turns[1].Reward, "armed where the chain was completed");
                Assert.AreEqual(MirrorTurnKind.Wall, sim.Turns[2].Kind, "and from there the turns are walls alone");
                Assert.IsTrue(sim.Turns.GetRange(2, sim.Turns.Count - 2).TrueForAll(t => t.Kind == MirrorTurnKind.Wall));
            }
        }

        [Test]
        public void ARicochetOffAnUnbreakable_KeepsTheChain_OnBothSides()
        {
            // Off the upper mushroom, down onto an unbreakable that sends the ball back up: the same mushroom
            // again does not count, and the unbreakable has not reset anything.
            var settings = new MushroomSettings();
            var unbreakable = new MirrorTarget(new Vector2(0f, -1.5f), 0.357f, int.MaxValue);
            var (game, sim) = Fly(Box, new[] { Above }, settings, new TrueReflectRule(), Vector2.zero, Vector2.up, 1,
                false, new[] { unbreakable });

            AssertMirrors(game, sim, "a ricochet");
            Assert.AreEqual(MirrorTurnKind.Bumper, sim.Turns[0].Kind);
            Assert.AreEqual(MirrorTurnKind.Deflect, sim.Turns[1].Kind);
            Assert.AreEqual(MirrorTurnKind.Bumper, sim.Turns[2].Kind);
            Assert.AreEqual(1, sim.Turns[2].Count, "the same mushroom twice in a row counts once");
        }

        [Test]
        public void ALayoutOfTheCatalogue_FlownIntoItsWidestWindow_IsTheSameFlightOnBothSides(
            [Values(2, 3, 4)] int walls, [Values(1, 2, 3, 4, 5, 6)] int pick)
        {
            // A layout a level is dealt and the forgiving kick, with nothing else on the board: the shot into the
            // middle of its widest window and the grid angles round it, with the shield a ball leaves the cannon
            // with. The ordinary ball is armed on the hit that brings its count to three and flies on a lance.
            var layout = Board.Layout(walls, pick * 9);
            var bumpers = Board.Bumpers(layout);
            var settings = new MushroomSettings();
            var middle = Board.MiddleOfTheWidestWindow(layout, settings);
            var armed = 0;
            for (var offset = -3; offset <= 3; offset++)
            {
                var direction = Board.Direction(middle + offset);
                var (game, sim) = Fly(Board.WallsClockwise, bumpers, settings, new ForgivingKickRule(settings),
                    StartInsideTheWalls(direction), direction, Board.StartingShields, false, NoTargets);
                var label = $"{walls} walls, pick {pick}, {Board.Degrees(middle + offset):F2} degrees";

                // A rally is many bounces off round caps, each of which multiplies the rounding the two sides
                // start apart by: a flight is held until that has grown to half a millimetre, or to a real graze
                // (a ball in the notch between two caps of a wall meets one on one side and its neighbour on the
                // other). A step across a corner of the board is one the harness cannot place.
                if (game.Unplaceable != MirrorUnplaceable.None)
                {
                    continue;
                }

                var difference = MushroomMirror.Difference(
                    game, sim, MirrorTolerance.Board, out var held, out _, bumpers);
                Assert.IsNull(difference, $"{label}: {difference}");
                var arming = sim.Turns.FindIndex(turn => turn.Reward == MushroomReward.Arm);
                if (arming >= 0 && held > arming)
                {
                    Assert.AreEqual(MushroomReward.Arm, game.Turns[arming].Reward, label);
                    Assert.AreEqual(
                        settings.Rules.ChainTarget, game.Turns[arming].Count, "armed on the third hit, wall to wall, by both");
                    armed++;
                }
            }

            Assert.GreaterOrEqual(armed, 4, "most angles round the middle of the window are held past the bounce that arms");
        }

        // A layout a level is dealt, cut by a lance and then played on: a ball loaded as a lance is flown into
        // the middle of the layout's widest window and cuts the caps in its way, on both sides alike. Then
        // ordinary balls are flown over the board as it left it, the simulator handed the caps that are whole: the
        // two sides must fly them alike, with the kick bent only toward caps that still stand.
        [Test]
        public void ALayoutALanceHasCut_IsFlownAlikeByTheBallsThatComeAfter(
            [Values(2, 3, 4)] int walls, [Values(1, 2, 3, 4)] int pick)
        {
            var layout = Board.Layout(walls, pick * 13);
            var bumpers = Board.Bumpers(layout);
            var settings = new MushroomSettings();
            var rule = new ForgivingKickRule(settings);
            var live = new MushroomLiveGame(Board.WallsClockwise, bumpers, settings, rule);
            var middle = Board.MiddleOfTheWidestWindow(layout, settings);
            var label = $"{walls} walls, pick {pick}";

            // Angle after angle from the window's middle, until a lance has cut something.
            MirrorFlight lance = null;
            for (var offset = 0; offset < 60 && (lance == null || lance.CapsCut.Count == 0); offset++)
            {
                var aim = Board.Direction(middle + offset);
                lance = live.Fly(StartInsideTheWalls(aim), aim, Board.StartingShields, true, NoTargets);
                var simulated = new MushroomSimGame(Board.WallsClockwise, bumpers, settings, rule).Fly(
                    StartInsideTheWalls(aim), aim, Board.StartingShields, true, NoTargets);
                if (lance.Unplaceable != MirrorUnplaceable.None)
                {
                    lance = null;
                    continue;
                }

                var difference = MushroomMirror.Difference(
                    lance, simulated, MirrorTolerance.Board, out _, out _, bumpers);
                Assert.IsNull(difference, $"{label}, the lance: {difference}");
                Assert.IsTrue(lance.Turns.TrueForAll(turn => turn.Kind == MirrorTurnKind.Wall), "a lance turns at walls");
            }

            Assert.IsNotNull(lance, label);
            Assert.Greater(lance.CapsCut.Count, 0, $"{label}: a lance aimed at the walls cut a cap");
            var left = new List<BumperCircle>(live.WholeCaps);
            Assert.AreEqual(bumpers.Length - lance.CapsCut.Count, left.Count);
            Assert.IsFalse(left.Exists(cap => lance.CapsCut.Contains(cap.Id)), "the cut caps have left the field");

            var sim = new MushroomSimGame(Board.WallsClockwise, left, settings, rule);
            var held = 0;
            var bounced = 0;
            for (var i = 3; i < Board.Angles; i += 7)
            {
                var direction = Board.Direction(i);
                var origin = StartInsideTheWalls(direction);

                // An ordinary ball a chain arms cuts caps of its own, so the board is put back as the lance left
                // it before each.
                live.CutOnly(lance.CapsCut);
                var game = live.Fly(origin, direction, Board.StartingShields, false, NoTargets, onTheBoardAsLeft: true);
                if (game.Unplaceable != MirrorUnplaceable.None)
                {
                    continue;
                }

                var difference = MushroomMirror.Difference(
                    game, sim.Fly(origin, direction, Board.StartingShields, false, NoTargets), MirrorTolerance.Board,
                    out var turns, out _, left);
                Assert.IsNull(difference, $"{label}, {Board.Degrees(i):F2} degrees after the cut: {difference}");
                held += turns;
                for (var turn = 0; turn < turns; turn++)
                {
                    if (game.Turns[turn].Kind != MirrorTurnKind.Bumper)
                    {
                        continue;
                    }

                    bounced++;
                    Assert.IsFalse(
                        lance.CapsCut.Contains(game.Turns[turn].BumperId), $"{label}: a ball bounced off a cut cap");
                }
            }

            Assert.Greater(held, 50, $"{label}: the flights after the cut were held turn for turn");
            Assert.Greater(bounced + (left.Count == 0 ? 1 : 0), 0, $"{label}: and met the caps that still stand");
        }

        // Boards of the shipped grid: a layout of the catalogue, one-hit balloons on four slots in ten, one unbreakable,
        // and shots from every sixth angle of the aim grid, as an ordinary ball and as a lance. Every flight the
        // harness can place must mirror, and between them the flights must have done everything a chain does.
        [Test]
        public void SeededBoards_FlownFromAcrossTheAimArc_MirrorTurnForTurn([Values(false, true)] bool startsAsLance)
        {
            var settings = new MushroomSettings();
            var tally = new SweepTally { UnplaceableBy = new int[6] };
            for (var seed = 1; seed <= 24; seed++)
            {
                var layout = Board.Layout(2 + (seed % 3), seed * 5);
                var bumpers = Board.Bumpers(layout);
                var targets = BuildTargets(seed, layout.Caps.Select(cap => cap.Slot).ToList());
                var live = new MushroomLiveGame(Board.WallsClockwise, bumpers, settings, new ForgivingKickRule(settings));
                var sim = new MushroomSimGame(Board.WallsClockwise, bumpers, settings, new ForgivingKickRule(settings));
                var angles = AimAngleGrid.ResolveSweepSampleCount(5f, 175f, Board.AimStepDegrees, 1);
                for (var i = seed % 6; i < angles; i += 6)
                {
                    var degrees = AimAngleGrid.ResolveSweepAngle(i, 5f, 175f, Board.AimStepDegrees, 1);
                    var direction = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                    var origin = StartInsideTheWalls(direction);
                    var game = live.Fly(origin, direction, 2, startsAsLance, targets);
                    tally.Flights++;
                    if (game.Unplaceable != MirrorUnplaceable.None)
                    {
                        tally.Unplaceable++;
                        tally.UnplaceableBy[(int)game.Unplaceable]++;
                        continue;
                    }

                    var simulated = sim.Fly(origin, direction, 2, startsAsLance, targets);
                    var difference = MushroomMirror.Difference(
                        game, simulated, MirrorTolerance.Board, out var held, out var whole, bumpers);
                    Assert.IsNull(difference, $"board {seed}, {degrees:F2} degrees, lance {startsAsLance}: {difference}");
                    tally.Count(game, simulated, held, whole);
                }
            }

            ShotMushroomFreeBoardTests.Report(
                $"Mushroom mirror, lance {startsAsLance}: {tally.Flights} flights, {tally.Unplaceable} the harness " +
                $"could not place (a balloon then a mushroom in one step " +
                $"{tally.UnplaceableBy[(int)MirrorUnplaceable.BalloonThenMushroom]}, a balloon then a wall " +
                $"{tally.UnplaceableBy[(int)MirrorUnplaceable.BalloonThenWall]}, a balloon then death " +
                $"{tally.UnplaceableBy[(int)MirrorUnplaceable.BalloonThenDeath]}, two walls " +
                $"{tally.UnplaceableBy[(int)MirrorUnplaceable.TwoWalls]}, a deflect into a wall " +
                $"{tally.UnplaceableBy[(int)MirrorUnplaceable.DeflectIntoWall]}), " +
                $"{tally.Compared} mirrored turn for turn ({tally.Whole} to their last wall, the " +
                $"rest until rounding outgrew the ceiling or a lance plowed the unbreakable); {tally.Turns} turns " +
                $"held, {tally.MushroomBounces} of them off mushrooms, {tally.Deflects} deflects; flights that " +
                $"bounced off a mushroom {tally.WithMushrooms}, armed {tally.Armed}, with a wall " +
                $"between two mushrooms {tally.WallBetween}; turns held after a chain armed the ball " +
                $"{tally.TurnsFlownOnArmed}, in {tally.FlightsFlownOnArmed} flights; caps cut by a lance " +
                $"{tally.CapsCut}, in {tally.FlightsThatCut} of the flights held to their last wall.");
            Assert.Greater(tally.Compared, tally.Flights * 0.85f, "nearly every flight is one the harness can place");
            Assert.Greater(tally.FlightsThatCut, 50, "lances cut caps, alike on both sides");
            if (startsAsLance)
            {
                Assert.AreEqual(0, tally.BouncesOfALance, "a ball loaded as a lance bounced off no mushroom while it was one");
            }
            else
            {
                Assert.Greater(tally.WithMushrooms, 50, "the flights met mushrooms");
                Assert.Greater(tally.WallBetween, 5, "and walls between them");
                // Measured: 295 flights armed, 291 of them held past the arming, over 1036 turns. The floors are
                // well under that, and far over what a mirror that stopped at the arming bounce would count.
                Assert.Greater(tally.Armed, 100, "and many were armed by their chain");
                Assert.Greater(tally.FlightsFlownOnArmed, 100, "and held on past the bounce that armed them");
                Assert.Greater(tally.TurnsFlownOnArmed, 300, "turn for turn, as the lance the chain made");
                Assert.Greater(tally.Deflects, 20, "and the unbreakable");
            }
        }

        private static (MirrorFlight Game, MirrorFlight Sim) Fly(
            Vector4 walls, BumperCircle[] bumpers, IMushroomSettings settings, IBumperBounceRule rule, Vector2 origin,
            Vector2 direction, int shields, bool startsAsLance, IReadOnlyList<MirrorTarget> targets)
        {
            var live = new MushroomLiveGame(walls, bumpers, settings, rule);
            var sim = new MushroomSimGame(walls, bumpers, settings, rule);
            var game = live.Fly(origin, direction, shields, startsAsLance, targets);
            return (game, sim.Fly(origin, direction, shields, startsAsLance, targets));
        }

        // A flight with nothing on the board to graze, held to its last wall.
        private static void AssertMirrors(MirrorFlight game, MirrorFlight sim, string label)
        {
            Assert.AreEqual(MirrorUnplaceable.None, game.Unplaceable, label);
            var difference = MushroomMirror.Difference(game, sim, MirrorTolerance.Open, out _, out var whole);
            Assert.IsNull(difference, $"{label}: {difference}");
            Assert.IsTrue(whole, $"{label}: the whole flight was held");
            Assert.IsTrue(game.Died, $"{label}: the flight ran to its last wall");
        }

        // The cannon's pivot rests a tenth of a unit under the bottom wall: the flight is picked up on its line
        // just inside the walls, where both sides agree a ball is in play.
        private static Vector2 StartInsideTheWalls(Vector2 direction)
        {
            return Cannon + (direction * (0.15f / direction.y));
        }

        // One-hit balloons on four free slots in ten and one unbreakable, none on a mushroom's slot.
        internal static List<MirrorTarget> BuildTargets(int seed, IReadOnlyList<Vector2Int> mushrooms)
        {
            var random = new System.Random(seed * 104729);
            var targets = new List<MirrorTarget>();
            var free = new List<Vector2Int>();
            foreach (var slot in Board.AllSlots())
            {
                if (Contains(mushrooms, slot) || slot.y < 2)
                {
                    continue;
                }

                if (random.NextDouble() < 0.4)
                {
                    targets.Add(new MirrorTarget(Board.SlotCenter(slot), 0.271f, 1));
                }
                else
                {
                    free.Add(slot);
                }
            }

            targets.Add(new MirrorTarget(Board.SlotCenter(free[random.Next(free.Count)]), 0.357f, int.MaxValue));
            return targets;
        }

        private static bool Contains(IReadOnlyList<Vector2Int> slots, Vector2Int slot)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private static MushroomSettings Settings(MushroomChainRules rules)
        {
            var settings = new MushroomSettings();
            typeof(MushroomSettings)
                .GetField("_rules", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(settings, rules);
            return settings;
        }

        private struct SweepTally
        {
            public int Flights;
            public int Unplaceable;
            public int[] UnplaceableBy;
            public int Compared;
            public int Turns;
            public int MushroomBounces;
            public int Deflects;
            public int WithMushrooms;
            public int Armed;
            public int WallBetween;

            // Caps cut in the flights held to their last wall (where the two sides' cuts are compared), the
            // flights that cut any, and bounces off a mushroom by a ball that was still the lance it left the
            // cannon as.
            public int CapsCut;
            public int FlightsThatCut;
            public int BouncesOfALance;
            public int Whole;

            // Turns held after the bounce that armed the ball, and the flights that had any: the lance the chain
            // made, flown on by both sides.
            public int TurnsFlownOnArmed;
            public int FlightsFlownOnArmed;

            // Only the turns that were held are counted: what the mirror is seen to cover is what it compared.
            public void Count(MirrorFlight game, MirrorFlight sim, int held, bool whole)
            {
                Compared++;
                Whole += whole ? 1 : 0;
                var mushrooms = 0;
                var wallSinceAMushroom = false;
                var wallBetween = false;
                var armedAt = -1;
                for (var i = 0; i < held; i++)
                {
                    Turns++;
                    if (armedAt < 0 && sim.Turns[i].Reward == MushroomReward.Arm)
                    {
                        armedAt = i;
                    }
                    else if (armedAt >= 0)
                    {
                        TurnsFlownOnArmed++;
                    }

                    switch (sim.Turns[i].Kind)
                    {
                        case MirrorTurnKind.Bumper:
                            mushrooms++;
                            wallBetween |= wallSinceAMushroom;
                            Armed += sim.Turns[i].Reward == MushroomReward.Arm ? 1 : 0;
                            BouncesOfALance += armedAt < 0 && sim.Turns[i].LeavesPiercing
                                && sim.Turns[i].Reward != MushroomReward.Arm ? 1 : 0;
                            break;
                        case MirrorTurnKind.Wall:
                            wallSinceAMushroom = mushrooms > 0;
                            break;
                        default:
                            Deflects++;
                            break;
                    }
                }

                MushroomBounces += mushrooms;
                FlightsFlownOnArmed += armedAt >= 0 && armedAt < held - 1 ? 1 : 0;
                WithMushrooms += mushrooms > 0 ? 1 : 0;
                WallBetween += wallBetween ? 1 : 0;
                CapsCut += whole ? sim.CapsCut.Count : 0;
                FlightsThatCut += whole && sim.CapsCut.Count > 0 ? 1 : 0;
            }
        }
    }
}

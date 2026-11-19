using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Snipe;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using BalloonParty.Tests.Slots;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     The bank (<c>Plans/PLAN-MeadowMushrooms.md</c>, decision K): every cap a lance cuts banks that flight a
    ///     lance charge, which keeps it a lance at the wall where its pierce would end. Flown by the game's own
    ///     step, with the real Snipe handler spending the charges, and held to the two bounds that keep a lance
    ///     that re-arms itself from flying for ever: its shields, and the caps on the board.
    /// </summary>
    [TestFixture]
    public class MushroomBankTests
    {
        private const float BeatSeconds = 1.7f;

        private static readonly Vector4 Box = new(5f, 5f, -5f, -5f);

        // On the line the lance flies, below the unbreakable; and one well off it.
        private static readonly BumperCircle OnTheLine = new(0, new Vector2(0f, -2f), 0.5f);
        private static readonly BumperCircle OffTheLine = new(1, new Vector2(3.5f, 0f), 0.5f);

        private static readonly MirrorTarget[] AnUnbreakable =
        {
            new(new Vector2(0f, 2f), 0.357f, int.MaxValue),
        };

        private MushroomSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = new MushroomSettings();
        }

        // Up through a cap, through an unbreakable, to the top wall. The wall spends the pierce on the
        // unbreakable; the charge the cap banked keeps the ball a lance, and it is one to its last wall.
        [Test]
        public void ALanceThatCutACap_IsStillALanceOffTheWallThatSpendsItsPierce_AndTheChargeIsSpentThere()
        {
            var flight = Fly(OnTheLine, shields: 3);

            CollectionAssert.AreEqual(new[] { OnTheLine.Id }, flight.CapsCut);
            Assert.AreEqual(1, flight.ChargesBanked);
            Assert.AreEqual(1, flight.Discharges, "the top wall, with the unbreakable plowed");
            Assert.AreEqual(1, flight.ChargesSpent, "spent there");
            Assert.AreEqual(1, flight.Pops, "the unbreakable, shattered at the wall");
            Assert.AreEqual(4, flight.Walls, "three shields: three walls survived and the one it died at");
            Assert.AreEqual(4, flight.WallsReachedAsLance, "a lance on every leg");
            Assert.IsTrue(flight.EndsPiercing);
        }

        // The same shot with no cap on its line: nothing is banked, and the wall ends the lance.
        [Test]
        public void ALanceThatCutNoCap_IsAnOrdinaryBallOffThatWall()
        {
            var flight = Fly(OffTheLine, shields: 3);

            Assert.AreEqual(0, flight.ChargesBanked);
            Assert.AreEqual(1, flight.Discharges);
            Assert.AreEqual(0, flight.ChargesSpent);
            Assert.AreEqual(4, flight.Walls, "its shields are what it lives by, lance or not");
            Assert.AreEqual(1, flight.WallsReachedAsLance, "a lance to the first wall only");
            Assert.IsFalse(flight.EndsPiercing);
        }

        // A charge is the shot's own and dies with it: a ball with no shield left dies at the wall where the
        // pierce is spent, and the charge it had banked is never spent.
        [Test]
        public void AShotThatDiesAtTheWall_TakesItsChargesWithIt()
        {
            var flight = Fly(OnTheLine, shields: 0);

            Assert.IsTrue(flight.Died);
            Assert.AreEqual(1, flight.Walls);
            Assert.AreEqual(1, flight.ChargesBanked);
            Assert.AreEqual(0, flight.ChargesSpent);
            Assert.AreEqual(1, flight.Pops, "the unbreakable it plowed is shattered all the same");
        }

        // With nothing plowed, no wall spends the pierce: the lance stays a lance with or without charges, and
        // the charges it banked are simply never used. That is the bank on a board with no unbreakable.
        [Test]
        public void WithNoToughPlowed_NoChargeIsEverSpent()
        {
            var live = new MushroomLiveGame(Box, new[] { OnTheLine }, _settings, new TrueReflectRule(), plowsToughs: true);

            var flight = live.Fly(new Vector2(0f, -4f), Vector2.up, 3, startsAsLance: true, new MirrorTarget[0]);

            Assert.AreEqual(1, flight.ChargesBanked);
            Assert.AreEqual(0, flight.Discharges);
            Assert.AreEqual(0, flight.ChargesSpent);
            Assert.AreEqual(4, flight.WallsReachedAsLance);
        }

        // The loop inside one flight, bound by the shot's life: a lance that keeps re-arming on a seven-cap board
        // with unbreakables to plow still pays a shield at every wall, charges or none. So it flies as many legs
        // as it has shields and one more, cuts each cap once at the most (nothing grows back in these flights),
        // banks a charge a cut and never spends more than it banked.
        [Test]
        public void ALanceWithPlentyOfShields_OnASevenCapBoard_FliesALegForEachShieldAndOneMore_AndCutsNoCapTwice()
        {
            const int shields = 40;
            var layout = MushroomTestBoard.Layout(4, 3);
            var bumpers = MushroomTestBoard.Bumpers(layout);
            Assert.AreEqual(7, bumpers.Length, "the most caps a board holds");
            var live = new MushroomLiveGame(
                MushroomTestBoard.WallsClockwise, bumpers, _settings, new ForgivingKickRule(_settings), plowsToughs: true)
            {
                FliesEveryFlight = true,
            };
            var targets = Balloons(layout, seed: 7);
            var spent = 0;
            var cut = 0;

            for (var angle = 5; angle < MushroomTestBoard.Angles; angle += 23)
            {
                var direction = MushroomTestBoard.Direction(angle);
                var start = MushroomTestBoard.CannonPivot + (direction * (0.15f / direction.y));

                var flight = live.Fly(start, direction, shields, startsAsLance: true, targets, maxSteps: 100000);

                var label = $"angle {angle}";
                Assert.IsTrue(flight.Died, $"{label}: the flight ended");
                Assert.AreEqual(shields + 1, flight.Walls, $"{label}: a shield a wall, lance or not");
                Assert.LessOrEqual(flight.CapsCut.Count, bumpers.Length, label);
                CollectionAssert.AllItemsAreUnique(flight.CapsCut, $"{label}: no cap twice");
                Assert.AreEqual(flight.CapsCut.Count, flight.ChargesBanked, $"{label}: a charge a cap");
                Assert.LessOrEqual(flight.ChargesSpent, flight.ChargesBanked, label);
                Assert.LessOrEqual(flight.ChargesSpent, flight.Discharges, $"{label}: one at a wall at the most");
                Assert.Less(flight.Seconds, (shields + 1) * LongestLegSeconds(), $"{label}: and no leg without end");
                spent += flight.ChargesSpent;
                cut += flight.CapsCut.Count;
            }

            Assert.Greater(cut, 20, "the flights did cut caps");
            Assert.Greater(spent, 5, "and did spend charges, so the lances were re-armed");
        }

        // The other bound, the caps: over the real board with its regrow running, a lance that outlives the
        // regrow many times over cuts a cap again only once it has grown back, four beats and more after it was
        // cut, and banks exactly one charge a cut.
        [Test]
        public void ALanceThatOutlivesTheRegrow_CutsACapAgainOnlyOnceItHasGrownBack_AndBanksAChargeACut()
        {
            const int shields = 60;
            var layout = MushroomTestBoard.Layout(4, 3);
            var grid = new SlotGrid(new GridConfig(), new BalancePathHolder());
            for (var i = 0; i < layout.Caps.Count; i++)
            {
                grid.Place(new MushroomBumperModel(i, layout.Caps[i].Wall), null, layout.Caps[i].Slot);
            }

            var board = new MushroomBoard(grid, _settings, new Origin());
            var beat = new Beat();
            var position = Vector3.zero;
            var balls = Substitute.For<IProjectileRegistry>();
            balls.InFlightCount.Returns(1);
            balls.InFlightPositionAt(Arg.Any<int>()).Returns(_ => position);
            var regrow = new MushroomRegrowController(
                board, beat, balls, Substitute.For<IPublisher<MushroomRegrownMessage>>());
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(MushroomTestBoard.WallsClockwise);
            var resolver = new ProjectileMotionResolver(
                config, new ProjectileTapResolver(config), board, new ForgivingKickRule(_settings), _settings);

            var direction = MushroomTestBoard.Direction(151);
            var ball = new ProjectileModel
            {
                Direction = direction,
                Speed = MushroomLiveGame.BaseSpeed,
                IsFree = true,
            };
            ball.ShieldsRemaining.Value = shields;
            ball.IsPiercing.Value = true;
            position = MushroomTestBoard.CannonPivot + (direction * (0.15f / direction.y));

            var lastCut = new Dictionary<Vector2Int, float>();
            var cuts = 0;
            var cutAgain = 0;
            var walls = 0;
            var seconds = 0f;
            var died = false;
            for (var i = 0; i < 200000 && !died; i++)
            {
                seconds += MushroomLiveGame.FixedStep;
                beat.Count = (int)(seconds / BeatSeconds);
                regrow.Tick();
                var step = resolver.Step(ball, position, MushroomLiveGame.FixedStep);
                foreach (var cut in resolver.StepCapCuts)
                {
                    cuts++;
                    Assert.AreEqual(cuts, cut.Charges, "the flight's count, one a cut");
                    if (lastCut.TryGetValue(cut.Slot, out var before))
                    {
                        cutAgain++;
                        Assert.GreaterOrEqual(
                            seconds - before, (_settings.RegrowBeats - 1) * BeatSeconds,
                            $"the cap in {cut.Slot}: back no sooner than four whole beats after its cut");
                    }

                    lastCut[cut.Slot] = seconds;
                }

                walls += step.Outcome == ProjectileStepOutcome.Bounced || step.Outcome == ProjectileStepOutcome.Destroyed
                    ? 1
                    : 0;
                died = step.Outcome == ProjectileStepOutcome.Destroyed;
                position = step.Position;
            }

            Assert.IsTrue(died, "the flight ended");
            Assert.AreEqual(shields + 1, walls, "by its shields, whatever it banked");
            Assert.Greater(cutAgain, 0, "it flew long enough to cut caps that had grown back");
            Assert.AreEqual(cuts, ball.Flight.CapsCut);
            Assert.AreEqual(cuts, ball.Flight.BankedPierceCharges, "a charge a cut, none spent with nothing to plow");
            Assert.LessOrEqual(
                cuts, layout.Caps.Count * (1 + Mathf.CeilToInt(seconds / ((_settings.RegrowBeats - 1) * BeatSeconds))),
                "no more cuts than the caps and their regrows allow");
        }

        // "Any lance" takes in a pierce no Snipe gave: one a ball earned cruising between empty walls. It cuts
        // caps as any piercing ball does (decision J), so its cuts bank too, and the charge is spent as every
        // banked charge is: at the wall that spends the pierce the ball stays piercing, its cruise ends there as
        // it always did, and the Snipe arms it afresh. From that wall on it is a Snipe's lance, with the Snipe's
        // speed, to its last wall. Pinned as it is; whether a cruise's pierce should bank is the owner's (the
        // plan, "Open for the owner"). With no cap cut the same wall ends the pierce, as before the bank.
        [TestCase(true)]
        [TestCase(false)]
        public void ACruisesPierce_ThatCutACap_IsASnipesLanceFromTheWallThatSpendsItOn(bool cutsACap)
        {
            var live = new MushroomLiveGame(
                Box, new[] { cutsACap ? OnTheLine : OffTheLine }, _settings, new TrueReflectRule(), plowsToughs: true)
            {
                StartsOnACruisesPierce = true,
            };

            var flight = live.Fly(new Vector2(0f, -4f), Vector2.up, 3, startsAsLance: false, AnUnbreakable);

            Assert.AreEqual(1, flight.Discharges, "the top wall, with the unbreakable plowed: without one, no charge is spent");
            Assert.AreEqual(4, flight.Walls);
            Assert.IsFalse(flight.EndsCruising, "the cruise that fed the pierce ends at that wall either way");
            Assert.AreEqual(cutsACap ? 1 : 0, flight.ChargesBanked);
            Assert.AreEqual(cutsACap ? 1 : 0, flight.ChargesSpent);
            Assert.AreEqual(cutsACap, flight.EndsPiercing);
            Assert.AreEqual(cutsACap, flight.EndsWithASpeedBuff, "the Snipe's speed buff, which a cruise's pierce never had");
            Assert.AreEqual(cutsACap ? 4 : 1, flight.WallsReachedAsLance, "piercing until its shields ran out");
            Assert.AreEqual(
                MushroomLiveGame.BaseSpeed * (cutsACap ? MushroomLiveGame.LanceSpeedMultiplier : 1f), flight.LastSpeed,
                1e-3f);
        }

        // The speed a banked charge re-arms the lance with is one number on both sides: the Snipe's speed buff
        // in the item configuration. The game's Snipe handler reads it when it spends the charge; the simulator's
        // layer reads it at the gather and every cut banks its charge at it.
        [Test]
        public void ACutsCharge_ReArmsAtTheSnipesSpeed_InTheSimulatorAsInTheGame()
        {
            var layer = ShotBumperLayer.Gather(
                new OneCap(OnTheLine), new TrueReflectRule(), _settings,
                ItemsWithASnipeOfSpeed(MushroomLiveGame.LanceSpeedMultiplier));
            var state = new ShotFlightState(Vector2.zero, Vector2.up, 1);

            ShotSimulator.BankCutCharge(layer.LanceSpeedBuffMultiplier, ref state);
            ShotSimulator.BankCutCharge(layer.LanceSpeedBuffMultiplier, ref state);

            Assert.AreEqual(2, state.BankedPierceCharges);
            Assert.AreEqual(2, state.Mushrooms.ChargesBanked);
            Assert.AreEqual(0, state.BankedRainbowPierceCharges);
            Assert.AreEqual(MushroomLiveGame.LanceSpeedMultiplier, state.BankedPierceMultiplier);

            // The game, its Snipe configured with the same number: the lance a charge re-armed flies at it.
            var flight = Fly(OnTheLine, shields: 3);
            Assert.AreEqual(1, flight.ChargesSpent);
            Assert.AreEqual(MushroomLiveGame.BaseSpeed * state.BankedPierceMultiplier, flight.LastSpeed, 1e-3f);
        }

        [Test]
        public void WithNoSpeedBuffToReArmWith_ACutsChargeIsBankedAtBaseSpeed()
        {
            var state = new ShotFlightState(Vector2.zero, Vector2.up, 1);

            ShotSimulator.BankCutCharge(0f, ref state);

            Assert.AreEqual(1, state.BankedPierceCharges);
            Assert.AreEqual(1f, state.BankedPierceMultiplier);
        }

        // Against what ships: the number the Snipe handler arms every lance with, a banked charge's included
        // (IItemConfiguration[Snipe].Snipe.SpeedBuffMultiplier), is the number the simulator banks a cut's charge
        // at. Needs the Editor, for the asset.
        [Test]
        public void TheShippedSnipesSpeed_IsWhatTheSimulatorBanksACutsChargeAt()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<ItemConfiguration>("Assets/Configuration/ItemConfiguration.asset");
            Assert.IsNotNull(shipped, "the item configuration should load");
            var snipesSpeed = shipped[ItemType.Snipe].Snipe.SpeedBuffMultiplier;
            Assert.Greater(snipesSpeed, 1f, "a lance is faster than a ball");

            var layer = ShotBumperLayer.Gather(new OneCap(OnTheLine), new TrueReflectRule(), _settings, shipped);
            var state = new ShotFlightState(Vector2.zero, Vector2.up, 1);
            ShotSimulator.BankCutCharge(layer.LanceSpeedBuffMultiplier, ref state);

            Assert.AreEqual(snipesSpeed, layer.LanceSpeedBuffMultiplier);
            Assert.AreEqual(snipesSpeed, state.BankedPierceMultiplier);
        }

        private static IItemConfiguration ItemsWithASnipeOfSpeed(float multiplier)
        {
            var snipe = new ItemSettings();
            SetField(snipe, "_type", ItemType.Snipe);
            SetField(snipe.Snipe, "_snipeSpeedBuffMultiplier", multiplier);
            var items = Substitute.For<IItemConfiguration>();
            items[ItemType.Snipe].Returns(snipe);
            return items;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }

        private MirrorFlight Fly(BumperCircle cap, int shields)
        {
            var live = new MushroomLiveGame(Box, new[] { cap }, _settings, new TrueReflectRule(), plowsToughs: true);
            return live.Fly(new Vector2(0f, -4f), Vector2.up, shields, startsAsLance: true, AnUnbreakable);
        }

        // The longest a leg can last: the board's diagonal at an ordinary ball's speed, and as much again for
        // the turns a ball that is no longer a lance makes off stems that are whole and off unbreakables.
        private static float LongestLegSeconds()
        {
            var walls = MushroomTestBoard.WallsClockwise;
            var diagonal = new Vector2(walls.y - walls.w, walls.x - walls.z).magnitude;
            return 2f * diagonal / MushroomLiveGame.BaseSpeed;
        }

        // Three slots in four of the rows above the cannon's filled, one balloon in seven of them an unbreakable.
        private static List<MirrorTarget> Balloons(MushroomWallLayout layout, int seed)
        {
            var random = new System.Random(seed);
            var caps = new HashSet<Vector2Int>(layout.Caps.Select(cap => cap.Slot));
            var targets = new List<MirrorTarget>();
            foreach (var slot in MushroomTestBoard.AllSlots())
            {
                if (caps.Contains(slot) || slot.y > 8 || random.NextDouble() >= 0.75)
                {
                    continue;
                }

                targets.Add(random.Next(7) == 0
                    ? new MirrorTarget(MushroomTestBoard.SlotCenter(slot), 0.357f, int.MaxValue)
                    : new MirrorTarget(MushroomTestBoard.SlotCenter(slot), 0.271f, 1));
            }

            return targets;
        }

        // A field of one whole cap, for a gather.
        private sealed class OneCap : IBumperField
        {
            private readonly List<BumperCircle> _caps = new();

            public IReadOnlyList<BumperCircle> Bumpers => _caps;

            internal OneCap(BumperCircle cap)
            {
                _caps.Add(cap);
            }

            public Vector2Int SlotAt(int index)
            {
                return new Vector2Int(_caps[index].Id, 0);
            }

            public void Cut(int index)
            {
                _caps.RemoveAt(index);
            }
        }

        private sealed class GridConfig : ISlotGridConfig
        {
            public Vector2Int SlotsSize => MushroomTestBoard.GridSize;
            public Vector2 SlotSeparation => new(0.375f, 0.65f);
            public Vector2 SlotsOffset => new(5f, 3.25f);
        }

        private sealed class Origin : IShotOrigin
        {
            public bool IsAvailable => true;
            public Vector2 RestingPivot => Vector2.zero;
            public float ProjectileContactRadius => MushroomTestBoard.BallRadius;
        }

        private sealed class Beat : IBoardBeat
        {
            internal int Count;

            public int OffBeats(float share)
            {
                return Count;
            }
        }
    }
}

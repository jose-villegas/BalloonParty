using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Effects;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using BalloonParty.Tests.Slots;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     A bomb's blast and the meadow's caps, on both sides (<c>Plans/PLAN-MeadowMushrooms.md</c>, "Blasts take
    ///     caps off"): the game's <see cref="MushroomBlast.Bomb" /> over the real board and the shot solver's
    ///     <see cref="ShotBumperLayer.BlowOff" /> take the same caps on every board of the catalogue, and a
    ///     simulated flight that sets a bomb off finds those caps gone for the rest of it.
    /// </summary>
    [TestFixture]
    public class MushroomBlastMirrorTests
    {
        // The shipped bomb (ItemConfiguration's _bombRadius and its rainbow ring).
        private const float KillRadius = 1.25f;
        private const float RainbowRange = 0.5f;

        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);
        private static readonly Vector2Int HostSlot = new(3, 4);

        // Next to the host's slot, in the row above it.
        private static readonly Vector2Int NextSlot = new(3, 3);

        private MushroomSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = new MushroomSettings();
        }

        // Every layout of the catalogue, a bomb in every slot that holds no mushroom, a normal and a rainbow
        // one: the caps the game announces as blown are the caps the solver's layer drops, by their ids.
        [Test]
        public void OnEveryBoardOfTheCatalogue_ABombTakesTheSameCapsOnBothSides()
        {
            var boards = 0;
            var bombs = 0;
            var taking = 0;
            for (var walls = MushroomWallCatalogue.FewestWalls; walls <= MushroomWallCatalogue.MostWalls; walls++)
            {
                foreach (var layout in MushroomWallCatalogue.For(walls))
                {
                    boards++;
                    var caps = new HashSet<Vector2Int>(layout.Caps.Select(cap => cap.Slot));
                    foreach (var bombSlot in MushroomTestBoard.AllSlots())
                    {
                        if (caps.Contains(bombSlot))
                        {
                            continue;
                        }

                        bombs++;
                        foreach (var isRainbow in new[] { false, true })
                        {
                            var game = BlownInTheGame(layout, bombSlot, isRainbow);
                            var solver = BlownInTheSolver(layout, bombSlot, isRainbow);

                            CollectionAssert.AreEquivalent(
                                game, solver, $"{walls} walls, bomb in {bombSlot}, rainbow {isRainbow}");
                            Assert.LessOrEqual(game.Count, 6, "never more than the six slots next to it");
                            taking += game.Count > 0 && !isRainbow ? 1 : 0;
                        }
                    }
                }
            }

            Assert.Greater(boards, 100, "the whole catalogue");
            Assert.Greater(taking, bombs / 20, "and bombs that take caps are no rarity on it");
        }

        // The ball pops a bomb's host and flies on toward the mushroom next to it. With the bomb the cap is
        // gone before the ball gets there; without it the ball bounces.
        [Test]
        public void ASimulatedBomb_TakesTheCapNextToIt_ForTheRestOfTheFlight()
        {
            var layer = Layer(withSlots: true, Cap(0, NextSlot));

            var plain = Fly(layer, HostWith(ItemType.None));

            Assert.AreEqual(0, plain.Mushrooms.CapsBlown);
            Assert.Greater(plain.Mushrooms.Bounces, 0, "no bomb: the ball meets the cap");

            var bombed = Fly(layer, HostWith(ItemType.Bomb));

            Assert.AreEqual(1, bombed.Mushrooms.CapsBlown);
            CollectionAssert.AreEqual(new[] { 0 }, layer.BlownLog);
            Assert.AreEqual(0, bombed.Mushrooms.Bounces, "the cap is gone when the ball reaches its place");
            Assert.AreEqual(0, bombed.Mushrooms.CapsCut, "blown, not cut");
            Assert.AreEqual(0, bombed.Mushrooms.ChargesBanked, "and a blown cap banks nothing");

            // The next flight starts from the board as gathered.
            var again = Fly(layer, HostWith(ItemType.None));

            Assert.AreEqual(0, layer.BlownLog.Count);
            Assert.AreEqual(plain.Mushrooms.Bounces, again.Mushrooms.Bounces);
            Assert.AreEqual(1, layer.Circles.Count);
        }

        [Test]
        public void ASimulatedBomb_LeavesTheCapsThatAreNotNextToIt()
        {
            var layer = Layer(withSlots: true, Cap(0, new Vector2Int(3, 6)), Cap(1, new Vector2Int(5, 4)));

            var result = Fly(layer, HostWith(ItemType.Bomb));

            Assert.AreEqual(0, result.Mushrooms.CapsBlown, "1.30 and 1.50 away: swayed in the game, and whole");
            Assert.AreEqual(2, layer.Circles.Count);
        }

        // A bomb whose blast pops another bomb's host: each takes the caps next to its own slot.
        [Test]
        public void AChainOfBombs_TakesTheCapsNextToEachOfThem()
        {
            var secondSlot = new Vector2Int(4, 4);
            var pastTheSecond = new Vector2Int(5, 4);
            var layer = Layer(withSlots: true, Cap(0, NextSlot), Cap(1, pastTheSecond));
            var board = HostWith(ItemType.Bomb).Append(ShotBoardBuilder.Green(
                MushroomTestBoard.SlotCenter(secondSlot), 0.05f, "Blue", 1, 1, secondSlot, 0, 0, 0f, false, null,
                item: ItemType.Bomb)).ToArray();

            var result = Fly(layer, board);

            Assert.AreEqual(2, result.Mushrooms.CapsBlown);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, layer.BlownLog);
        }

        // A rainbow bomb takes by its kill radius, a normal one by the slot: a cap of a far slot that stands
        // within the kill radius goes only under the rainbow one.
        [TestCase(false, 0)]
        [TestCase(true, 1)]
        public void ASimulatedRainbowBomb_TakesByItsKillRadius(bool isRainbow, int blown)
        {
            var host = MushroomTestBoard.SlotCenter(HostSlot);
            var drifted = new BumperCircle(0, host + new Vector2(-1.1f, 0.2f), MushroomTestBoard.ContactRadius);
            var layer = new ShotBumperLayer(
                new[] { drifted }, new ForgivingKickRule(_settings), _settings.Rules, 1.6f,
                new[] { new Vector2Int(0, 9) })
            {
                BlownLog = new List<int>(),
            };

            var result = Fly(layer, isRainbow ? RainbowHost() : HostWith(ItemType.Bomb));

            Assert.AreEqual(1, result.Pops, "the host and no other");
            Assert.AreEqual(blown, result.Mushrooms.CapsBlown);
        }

        // The flight's last event set the bomb off and left nothing to fly at: its caps are still told.
        [Test]
        public void ABombThatClearsTheBoard_StillTellsOfItsCaps()
        {
            var layer = Layer(withSlots: true, Cap(0, NextSlot));
            var host = MushroomTestBoard.SlotCenter(HostSlot);
            var board = new[]
            {
                ShotBoardBuilder.Green(host, 0.05f, "Red", 1, 1, HostSlot, 0, 0, 0f, false, null, item: ItemType.Bomb),
            };

            var result = Fly(layer, board);

            Assert.IsTrue(result.BoardCleared);
            Assert.AreEqual(1, result.Mushrooms.CapsBlown);
        }

        // A layer built by hand, without the caps' slots, has nothing to ask the bomb's rule with.
        [Test]
        public void ALayerWithoutSlots_LosesNoCapToABomb()
        {
            var layer = Layer(withSlots: false, Cap(0, NextSlot));

            var result = Fly(layer, HostWith(ItemType.Bomb));

            Assert.AreEqual(0, result.Mushrooms.CapsBlown);
            Assert.Greater(result.Mushrooms.Bounces, 0);
            Assert.AreEqual(
                0, layer.BlowOff(MushroomTestBoard.SlotCenter(HostSlot), HostSlot, false, new BombBlastParams(KillRadius, 0f)));
        }

        // A board without mushrooms has no layer: the bomb is resolved as it always was.
        [Test]
        public void WithNoMushrooms_ABombFliesAsBefore()
        {
            var board = HostWith(ItemType.Bomb);
            var workingSet = new ShotBalloonState[board.Length];
            var direction = Aim();

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, MushroomTestBoard.SlotCenter(HostSlot) - (direction * 2f), direction,
                startingShields: 0, projectileContactRadius: 0f, workingSet: workingSet, rippleDamage: 1,
                items: BombLayer());

            Assert.AreEqual(1, result.Pops);
            Assert.AreEqual(0, result.Mushrooms.CapsBlown);
        }

        private IReadOnlyCollection<int> BlownInTheGame(MushroomWallLayout layout, Vector2Int bombSlot, bool isRainbow)
        {
            var board = Board(layout, out var grid);
            var ids = new Dictionary<Vector2Int, int>();
            for (var i = 0; i < board.Bumpers.Count; i++)
            {
                ids[board.SlotAt(i)] = board.Bumpers[i].Id;
            }

            var blown = new List<int>();
            var cutPublisher = Substitute.For<IPublisher<MushroomCutMessage>>();
            cutPublisher.Publish(Arg.Do<MushroomCutMessage>(message => blown.Add(ids[message.Slot])));
            var blast = new MushroomBlast(
                board, grid, _settings, cutPublisher, Substitute.For<IPublisher<MushroomSwayedMessage>>());

            blast.Bomb(grid.IndexToWorldPosition(bombSlot), bombSlot, isRainbow, KillRadius);

            Assert.AreEqual(layout.Caps.Count - blown.Count, board.Bumpers.Count, "what was announced is what went");
            return blown;
        }

        private IReadOnlyCollection<int> BlownInTheSolver(MushroomWallLayout layout, Vector2Int bombSlot, bool isRainbow)
        {
            var board = Board(layout, out var grid);
            var layer = ShotBumperLayer.Gather(board, new ForgivingKickRule(_settings), _settings, null);
            layer.BlownLog = new List<int>();

            var count = layer.BlowOff(
                grid.IndexToWorldPosition(bombSlot), bombSlot, isRainbow, new BombBlastParams(KillRadius, RainbowRange));

            Assert.AreEqual(count, layer.BlownLog.Count);
            Assert.AreEqual(layout.Caps.Count - count, layer.Circles.Count);
            return layer.BlownLog;
        }

        private MushroomBoard Board(MushroomWallLayout layout, out SlotGrid grid)
        {
            grid = new SlotGrid(new GridConfig(), new BalancePathHolder());
            for (var i = 0; i < layout.Caps.Count; i++)
            {
                grid.Place(new MushroomBumperModel(i, layout.Caps[i].Wall), null, layout.Caps[i].Slot);
            }

            return new MushroomBoard(grid, _settings, new Origin());
        }

        private static (int Id, Vector2Int Slot) Cap(int id, Vector2Int slot)
        {
            return (id, slot);
        }

        private ShotBumperLayer Layer(bool withSlots, params (int Id, Vector2Int Slot)[] caps)
        {
            var circles = caps
                .Select(cap => new BumperCircle(
                    cap.Id, MushroomTestBoard.SlotCenter(cap.Slot), MushroomTestBoard.ContactRadius))
                .ToArray();
            return new ShotBumperLayer(
                circles, new ForgivingKickRule(_settings), _settings.Rules, 1.6f,
                withSlots ? caps.Select(cap => cap.Slot).ToArray() : null)
            {
                BlownLog = new List<int>(),
                BounceLog = new List<ShotBumperBounce>(),
            };
        }

        // The host on its slot and a balloon far off that nothing reaches, so the flight goes on after the pop.
        private static ShotBalloonSnapshot[] HostWith(ItemType item)
        {
            return new[]
            {
                ShotBoardBuilder.Green(
                    MushroomTestBoard.SlotCenter(HostSlot), 0.05f, "Red", 1, 1, HostSlot, 0, 0, 0f, false, null,
                    item: item),
                FarOff(),
            };
        }

        // The same, the host a rainbow balloon: its bomb is a rainbow bomb.
        private static ShotBalloonSnapshot[] RainbowHost()
        {
            return new[]
            {
                ShotBoardBuilder.Rainbow(
                    MushroomTestBoard.SlotCenter(HostSlot), 0.05f, GamePalette.RainbowColorId, 1, 1, HostSlot, 0, 0,
                    0f, false, null, item: ItemType.Bomb),
                FarOff(),
            };
        }

        private static ShotBalloonSnapshot FarOff()
        {
            return ShotBoardBuilder.Green(
                new Vector2(500f, 500f), 0.05f, "Blue", 1, 1, new Vector2Int(40, 40), 0, 0, 0f, false, null);
        }

        // Through the host's centre toward the slot next to it in the row above.
        private static Vector2 Aim()
        {
            return (MushroomTestBoard.SlotCenter(NextSlot) - MushroomTestBoard.SlotCenter(HostSlot)).normalized;
        }

        private static ShotSimulationResult Fly(ShotBumperLayer layer, ShotBalloonSnapshot[] board)
        {
            var workingSet = new ShotBalloonState[board.Length];
            var direction = Aim();
            return ShotSimulator.Simulate(
                board, WideOpenWalls, MushroomTestBoard.SlotCenter(HostSlot) - (direction * 2f), direction,
                startingShields: 0, projectileContactRadius: 0f, workingSet: workingSet, rippleDamage: 1,
                items: BombLayer(), bumpers: layer, rainbowColorId: GamePalette.RainbowColorId,
                allowedColors: new[] { "Red", "Blue" });
        }

        private static ShotItemLayer BombLayer()
        {
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Bomb,
                    new ItemEffectParams(
                        new BombEffectParams(KillRadius, rainbowEffectScale: 1f, RainbowRange), default, default,
                        default, default, 1, DamageFlags.Normal)
                },
            };
            var lattice = default(ShotSlotLattice);
            return new ShotItemLayer(effectParams, in lattice, GamePalette.RainbowColorId);
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
    }
}

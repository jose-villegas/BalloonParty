using System.Collections.Generic;
using System.Linq;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Grid;
using BalloonParty.Tests.Slots;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     What a bomb breaks outright, asked of one thing (<see cref="BombBlast.Pierces" />): the rule that breaks
    ///     an unbreakable balloon, and by which the meadow's mushrooms lose their caps. Held against
    ///     <see cref="BombBlast.Resolve" /> itself, so the two cannot come apart.
    /// </summary>
    [TestFixture]
    public class BombBlastPiercesTests
    {
        // The shipped bomb (ItemConfiguration's _bombRadius) and a balloon's radius.
        private const float KillRadius = 1.25f;
        private const float BalloonRadius = 0.3f;

        private static readonly Vector2Int Host = new(3, 4);
        private static readonly BombBlastParams Blast = new(KillRadius, 0.5f);

        [Test]
        public void ANormalBomb_Pierces_TheSixSlotsNextToIt_AndNoOther()
        {
            var origin = MushroomTestBoard.SlotCenter(Host);

            var pierced = MushroomTestBoard.AllSlots()
                .Where(slot => slot != Host && BombBlast.Pierces(
                    origin, Host, false, in Blast, slot, MushroomTestBoard.SlotCenter(slot), BalloonRadius))
                .ToArray();

            CollectionAssert.AreEquivalent(HexCoordinates.HexNeighborIndices(Host.x, Host.y), pierced);
        }

        // In world units on the shipped lattice: the slots next to the bomb are 0.75 from it, and the next
        // nearest 1.30, past a rainbow bomb's kill radius of 1.25 too. So both bombs break the same six.
        [Test]
        public void ARainbowBombOnItsSlot_PiercesTheSameSix()
        {
            var origin = MushroomTestBoard.SlotCenter(Host);

            var pierced = MushroomTestBoard.AllSlots()
                .Where(slot => slot != Host && BombBlast.Pierces(
                    origin, Host, true, in Blast, slot, MushroomTestBoard.SlotCenter(slot), BalloonRadius))
                .ToArray();

            CollectionAssert.AreEquivalent(HexCoordinates.HexNeighborIndices(Host.x, Host.y), pierced);
            foreach (var slot in pierced)
            {
                Assert.AreEqual(0.75f, Vector2.Distance(origin, MushroomTestBoard.SlotCenter(slot)), 1e-3f);
            }
        }

        // A neighbour the blast does not reach is not broken, as the live bomb only looks among what its
        // circle overlaps.
        [Test]
        public void ANormalBomb_DoesNotPierceANeighbourSlotItsBlastDoesNotReach()
        {
            var origin = MushroomTestBoard.SlotCenter(Host);
            var neighbour = new Vector2Int(Host.x + 1, Host.y);

            Assert.IsTrue(BombBlast.Pierces(origin, Host, false, in Blast, neighbour, origin + (Vector2.right * 1.5f), 0.3f));
            Assert.IsFalse(BombBlast.Pierces(origin, Host, false, in Blast, neighbour, origin + (Vector2.right * 1.6f), 0.3f));
            Assert.IsFalse(BombBlast.Pierces(origin, Host, false, in Blast, neighbour, origin + (Vector2.right * 1.3f), 0f));
        }

        // A rainbow bomb goes by the centre alone: a slot's number and a radius change nothing.
        [Test]
        public void ARainbowBomb_PiercesByTheCentreWithinItsKillRadius_WhateverTheSlot()
        {
            var origin = MushroomTestBoard.SlotCenter(Host);
            var farSlot = new Vector2Int(0, 9);

            Assert.IsTrue(BombBlast.Pierces(origin, Host, true, in Blast, farSlot, origin + (Vector2.up * 1.25f), 0f));
            Assert.IsFalse(BombBlast.Pierces(origin, Host, true, in Blast, farSlot, origin + (Vector2.up * 1.26f), 5f));
        }

        // Pierces is what Resolve marks as a piercing hit: every occupant of a full board, a bomb on its slot
        // and one that went off a little aside, a normal and a rainbow host.
        [TestCase(false, 0f, 0f)]
        [TestCase(true, 0f, 0f)]
        [TestCase(false, 0.31f, -0.22f)]
        [TestCase(true, 0.31f, -0.22f)]
        public void Pierces_IsWhatResolveMarksAsAPiercingHit(bool isRainbow, float asideX, float asideY)
        {
            var board = new FullBoard(Host);
            var origin = MushroomTestBoard.SlotCenter(Host) + new Vector2(asideX, asideY);
            var hits = new List<EffectHit>();

            BombBlast.Resolve(board, origin, Host, isRainbow, "Rainbow", in Blast, hits);

            var marked = hits.Where(hit => hit.Kind == EffectHitKind.PiercingDamage).Select(hit => hit.Handle).ToArray();
            var asked = board.Occupants
                .Where(o => BombBlast.Pierces(origin, Host, isRainbow, in Blast, o.Slot, o.Position, o.Radius))
                .Select(o => o.Handle)
                .ToArray();

            CollectionAssert.AreEquivalent(marked, asked);
            Assert.Greater(marked.Length, 0);
        }

        // Every slot of the shipped grid but the host's, a balloon in each.
        private sealed class FullBoard : IEffectBoard
        {
            private readonly List<EffectOccupant> _occupants = new();

            public IReadOnlyList<EffectOccupant> Occupants => _occupants;
            public int SearchRadius => 0;

            internal FullBoard(Vector2Int host)
            {
                foreach (var slot in MushroomTestBoard.AllSlots())
                {
                    if (slot == host)
                    {
                        continue;
                    }

                    var position = MushroomTestBoard.SlotCenter(slot);
                    _occupants.Add(new EffectOccupant(
                        _occupants.Count, slot, position, position, BalloonRadius, "Red", true, false));
                }
            }

            public bool TryGetOccupantAt(Vector2Int slot, out EffectOccupant occupant)
            {
                occupant = default;
                return false;
            }
        }
    }
}

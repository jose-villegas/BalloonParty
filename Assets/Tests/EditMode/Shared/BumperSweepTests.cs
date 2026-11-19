using BalloonParty.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     The swept test of a ball against the bumper circles (<see cref="BumperSweep" />): the first circle a
    ///     straight run enters and how far along, which the flight and everything that looks ahead of the ball ask.
    /// </summary>
    [TestFixture]
    public class BumperSweepTests
    {
        private const float Tolerance = 1e-4f;
        private const float Radius = 0.5f;

        private static readonly BumperCircle Near = new(0, new Vector2(0f, 2f), Radius);
        private static readonly BumperCircle Far = new(1, new Vector2(0f, 5f), Radius);

        [Test]
        public void TryFindFirst_ABumperOnTheRun_ReportsItsEntry()
        {
            var bumpers = new[] { Near };

            var found = BumperSweep.TryFindFirst(
                bumpers, Vector2.zero, Vector2.up, 3f, MushroomChainState.NoBumper, out var index, out var entry);

            Assert.IsTrue(found);
            Assert.AreEqual(0, index);
            Assert.AreEqual(1.5f, entry, Tolerance, "the circle's near edge, not its centre");
        }

        [Test]
        public void TryFindFirst_ABumperPastTheRunsEnd_IsNotInTheWay()
        {
            var bumpers = new[] { Near };

            Assert.IsFalse(BumperSweep.TryFindFirst(
                bumpers, Vector2.zero, Vector2.up, 1.4f, MushroomChainState.NoBumper, out _, out _));
            Assert.IsTrue(BumperSweep.TryFindFirst(
                bumpers, Vector2.zero, Vector2.up, 1.6f, MushroomChainState.NoBumper, out _, out _));
        }

        [Test]
        public void TryFindFirst_TwoBumpersOnTheRun_TheEarliestEntryWins_WhateverTheListOrder()
        {
            var found = BumperSweep.TryFindFirst(
                new[] { Far, Near }, Vector2.zero, Vector2.up, 10f, MushroomChainState.NoBumper, out var index,
                out var entry);

            Assert.IsTrue(found);
            Assert.AreEqual(1, index, "the nearer one, second in the list");
            Assert.AreEqual(1.5f, entry, Tolerance);
        }

        [Test]
        public void TryFindFirst_TheBumperBeingLeft_IsLeftOut()
        {
            var found = BumperSweep.TryFindFirst(
                new[] { Near, Far }, Vector2.zero, Vector2.up, 10f, Near.Id, out var index, out var entry);

            Assert.IsTrue(found);
            Assert.AreEqual(1, index, "the run is tested against the others only");
            Assert.AreEqual(4.5f, entry, Tolerance);
        }

        [Test]
        public void TryFindFirst_ARunThatMissesOrLeadsAway_FindsNothing()
        {
            var bumpers = new[] { Near };

            Assert.IsFalse(
                BumperSweep.TryFindFirst(
                    bumpers, new Vector2(0.6f, 0f), Vector2.up, 10f, MushroomChainState.NoBumper, out _, out _),
                "passing 0.1 clear of the circle's side");
            Assert.IsFalse(
                BumperSweep.TryFindFirst(
                    bumpers, Vector2.zero, Vector2.down, 10f, MushroomChainState.NoBumper, out _, out _),
                "the bumper is behind the ball");
        }

        [Test]
        public void TryFindEntry_FromInsideTheCircle_IsTheEntryBehindTheBall()
        {
            // 0.2 inside along the line: the line came in at y = 1.5, 0.2 behind.
            var found = BumperSweep.TryFindEntry(new Vector2(0f, 1.7f), Vector2.up, Near, out var distance);

            Assert.IsTrue(found);
            Assert.AreEqual(-0.2f, distance, Tolerance);
        }

        [Test]
        public void TryFindFirst_AnEntryBehindTheBall_WinsOverOneAhead()
        {
            // Inside the near circle already, with the far one still ahead on the same line.
            var found = BumperSweep.TryFindFirst(
                new[] { Far, Near }, new Vector2(0f, 1.7f), Vector2.up, 10f, MushroomChainState.NoBumper,
                out var index, out var entry);

            Assert.IsTrue(found);
            Assert.AreEqual(1, index);
            Assert.AreEqual(-0.2f, entry, Tolerance);
        }

        [Test]
        public void TryFindEntry_OnItsWayOutAtTheEdge_IsNotInside()
        {
            // A rounding error inside the far edge, heading out: leaving, not a contact.
            var atTheEdge = new Vector2(0f, 2.5f - 1e-5f);

            Assert.IsFalse(BumperSweep.TryFindEntry(atTheEdge, Vector2.up, Near, out _));
            Assert.IsTrue(BumperSweep.TryFindEntry(atTheEdge, Vector2.down, Near, out var distance),
                "the same point heading back in has the whole circle ahead of it");
            Assert.LessOrEqual(distance, 0f);
        }

        [Test]
        public void TryFindEntry_ACircleOfNoSize_IsNeverMet()
        {
            var nothing = new BumperCircle(0, new Vector2(0f, 2f), 0f);

            Assert.IsFalse(BumperSweep.TryFindEntry(Vector2.zero, Vector2.up, nothing, out _));
            Assert.IsFalse(BumperSweep.TryFindEntry(new Vector2(0f, 2f), Vector2.up, nothing, out _));
        }
    }
}

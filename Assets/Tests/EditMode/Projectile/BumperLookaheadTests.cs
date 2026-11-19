using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     What looks ahead of a ball for mushrooms (<see cref="BumperLookahead" />): the doomed glide's path check,
    ///     the cruise lookahead and the pierce telegraph. A mushroom is in the way unless the ball's own flight
    ///     takes it out, and that only holds until the ball's next wall.
    /// </summary>
    [TestFixture]
    public class BumperLookaheadTests
    {
        private const float Radius = 0.5f;

        // A 10-wide box centred on the origin.
        private static readonly WallLimits Walls = new(new Vector4(5f, 5f, -5f, -5f));

        private static readonly MushroomChainRules Rules = new(chainTarget: 3);
        private static readonly BumperCircle Above = new(0, new Vector2(0f, 2f), Radius);

        private ProjectileFlightState _flight;

        [SetUp]
        public void SetUp()
        {
            _flight = new ProjectileFlightState();
        }

        [Test]
        public void AMushroomOnTheRun_IsInTheWay_AtItsNearEdge()
        {
            var found = BumperLookahead.TryFindAhead(
                new[] { Above }, _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up, 5f, 0), false, out var distance);

            Assert.IsTrue(found);
            Assert.AreEqual(1.5f, distance, 1e-4f);
        }

        [Test]
        public void ADirectionOfAnyLength_IsReadAsAHeading()
        {
            var found = BumperLookahead.TryFindAhead(
                new[] { Above }, _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up * 7f, 5f, 0), false,
                out var distance);

            Assert.IsTrue(found);
            Assert.AreEqual(1.5f, distance, 1e-4f);
        }

        [Test]
        public void NoMushrooms_OrARunThatStopsShort_IsClear()
        {
            Assert.IsFalse(BumperLookahead.TryFindAhead(
                new BumperCircle[0], _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up, 5f, 0), false, out _));
            Assert.IsFalse(BumperLookahead.TryFindAhead(
                new[] { Above }, _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up, 1.4f, 0), false, out _));
        }

        [Test]
        public void ARunThatStartsInsideAMushroom_FindsItAtNoDistance()
        {
            var found = BumperLookahead.TryFindAhead(
                new[] { Above }, _flight, Rules, new BumperLookahead.Run(new Vector2(0f, 1.7f), Vector2.up, 5f, 0), false,
                out var distance);

            Assert.IsTrue(found);
            Assert.AreEqual(0f, distance);
        }

        // A lance bounces off no mushroom: it cuts the cap and flies on. So nothing is in its way, on the leg it
        // is on or on any leg past a wall, from outside a mushroom or from inside one.
        [Test]
        public void ALance_HasNoMushroomInItsWay_OnAnyLeg()
        {
            var bumpers = new[] { Above };
            for (var leg = 0; leg < 3; leg++)
            {
                Assert.IsFalse(
                    BumperLookahead.TryFindAhead(
                        bumpers, _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up, 5f, leg), true, out _),
                    $"leg {leg}");
                Assert.IsFalse(
                    BumperLookahead.TryFindAhead(
                        bumpers, _flight, Rules, new BumperLookahead.Run(new Vector2(0f, 1.7f), Vector2.up, 5f, leg),
                        true, out _),
                    $"leg {leg}, from inside it");
            }
        }

        [Test]
        public void ATracedPath_OfALance_IsClearThroughAMushroomOnEveryLeg()
        {
            var bumpers = new[] { new BumperCircle(0, new Vector2(1f, -2f), Radius) };
            var start = new Vector3(1f, 0f, 0f);

            Assert.IsFalse(IsClearAhead(bumpers, start, Vector3.down, legs: 1), "an ordinary ball would bounce off it");
            Assert.IsTrue(IsClearAhead(bumpers, start, Vector3.down, legs: 1, piercing: true));
            Assert.IsTrue(IsClearAhead(bumpers, start, Vector3.up, legs: 3, piercing: true));
        }

        [Test]
        public void ASpentStretch_LetsTheBallThroughOnItsOwnLeg_AndOnNoLegPastAWall()
        {
            // The ball has had every bounce its stretch is allowed: the mushrooms let it through, until a wall.
            _flight.MushroomChain.Bounces = Rules.MaxBouncesPerStretch;
            var bumpers = new[] { Above };

            Assert.IsFalse(
                BumperLookahead.TryFindAhead(
                    bumpers, _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up, 5f, 0), false, out _),
                "from the ball to its first wall the mushroom is not in its way");
            Assert.IsTrue(
                BumperLookahead.TryFindAhead(
                    bumpers, _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up, 5f, 1), false, out _),
                "the wall clears the chain, so past it the mushroom bounces the ball again");
            Assert.IsTrue(BumperLookahead.TryFindAhead(
                bumpers, _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up, 5f, 2), false, out _));
        }

        [Test]
        public void TheMushroomBeingLeft_IsOutOfTheWayOnTheBallsOwnLegOnly()
        {
            // The ball has just bounced off the mushroom and flies on through it, as the worst bounce rule could
            // send it. Still leaving on its own leg; a leg past a wall is another line altogether.
            var through = new Vector2(0.6f, 0.8f);
            _flight.LeaveBumper(Above.Id, through);
            var bumpers = new[] { Above };
            var onTheCircle = new Vector2(0f, 1.5f);

            Assert.IsFalse(BumperLookahead.TryFindAhead(
                bumpers, _flight, Rules, new BumperLookahead.Run(onTheCircle, through, 5f, 0), false, out _));
            Assert.IsTrue(BumperLookahead.TryFindAhead(
                bumpers, _flight, Rules, new BumperLookahead.Run(new Vector2(-0.3f, 1.1f), through, 5f, 1), false, out _));
            Assert.IsTrue(
                BumperLookahead.TryFindAhead(
                    bumpers, _flight, Rules, new BumperLookahead.Run(Vector2.zero, Vector2.up, 5f, 0), false, out _),
                "and once the ball has turned it is in the way again");
        }

        [Test]
        public void ATracedPath_WithASpentChain_IsBlockedByTheMushroomItMeetsAfterItsFirstWall()
        {
            // Straight up past nothing to the top wall, back down the same line into the mushroom's top. Traced as
            // ProjectileView traces a cruise lookahead: the legs asked in order, the asks counted.
            _flight.MushroomChain.Bounces = Rules.MaxBouncesPerStretch;
            var bumpers = new[] { new BumperCircle(0, new Vector2(1f, -2f), Radius) };
            var start = new Vector3(1f, 0f, 0f);

            Assert.IsTrue(IsClearAhead(bumpers, start, Vector3.up, legs: 1), "one leg: up to the wall, nothing there");
            Assert.IsFalse(IsClearAhead(bumpers, start, Vector3.up, legs: 2), "two legs: the way back down meets it");

            // The same mushroom on the first leg of a spent ball is flown through, and the wall behind it reached.
            Assert.IsTrue(IsClearAhead(bumpers, start, Vector3.down, legs: 1));
            Assert.IsFalse(IsClearAhead(bumpers, start, Vector3.down, legs: 2), "and met coming back up off the floor");
        }

        [Test]
        public void ATracedPath_WithAFreshChain_IsBlockedByAMushroomOnAnyLeg()
        {
            var bumpers = new[] { new BumperCircle(0, new Vector2(1f, -2f), Radius) };
            var start = new Vector3(1f, 0f, 0f);

            Assert.IsFalse(IsClearAhead(bumpers, start, Vector3.down, legs: 1));
            Assert.IsFalse(IsClearAhead(bumpers, start, Vector3.up, legs: 2));
            Assert.IsTrue(IsClearAhead(bumpers, new Vector3(-3f, 0f, 0f), Vector3.up, legs: 3), "a corridor it is not in");
        }

        private bool IsClearAhead(
            BumperCircle[] bumpers, Vector3 position, Vector3 direction, int legs, bool piercing = false)
        {
            var leg = 0;
            return PathTrace.IsClearAhead(Walls, position, direction, legs, (from, heading, length) =>
                BumperLookahead.TryFindAhead(
                    bumpers, _flight, Rules, new BumperLookahead.Run(from, heading, length, leg++), piercing, out _));
        }
    }
}

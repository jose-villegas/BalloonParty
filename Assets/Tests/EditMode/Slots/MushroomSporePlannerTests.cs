using BalloonParty.Configuration.GridActors;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     What a meadow mushroom lets go when something happens to its cap (<see cref="MushroomSporePlanner" />,
    ///     @ref plan_meadow_mushrooms): a hit's small cone the way the ball was going, scaled by its speed; a
    ///     cut's a little more; a blast's burst; and the two things that keep it quiet, a mushroom's own pause
    ///     and the board's shared budget.
    /// </summary>
    [TestFixture]
    public class MushroomSporePlannerTests
    {
        private const float Radius = 0.3f;

        private static readonly MushroomSporeTuning Shipped = new MushroomSettings().Spores;
        private static readonly Vector2Int Slot = new(3, 5);
        private static readonly Vector2Int OtherSlot = new(1, 6);
        private static readonly Vector2 Centre = new(0.56f, 0f);

        private MushroomSporePlanner _planner;

        [SetUp]
        public void SetUp()
        {
            _planner = new MushroomSporePlanner();
        }

        [TestCase(1f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(-0.6f, 0.8f)]
        public void AHitAtTheCannonsSpeed_PuffsTheWholeCone_OutOfTheFarRim_TheWayTheBallWasGoing(float x, float y)
        {
            var incoming = new Vector2(x, y);

            Assert.IsTrue(Hit(Slot, incoming * 3f, Shipped.FullSpeed, false, 10f, out var burst));

            Assert.AreEqual(MushroomSporeKind.Hit, burst.Kind);
            AssertVector(incoming, burst.Direction, "along the ball's way, whatever the length it was given in");
            AssertVector(Centre + (incoming * Radius), burst.Origin, "from the cap's far rim");
            Assert.AreEqual(Shipped.Length, burst.Length, 1e-5f, "about a cap's width long");
            Assert.AreEqual(Shipped.EndWidth, burst.EndWidth, 1e-5f, "and half as wide at its end");
            Assert.AreEqual(Shipped.Smoke, burst.Smoke, 1e-5f);
            Assert.AreEqual(Shipped.Specks, burst.Specks);
            Assert.AreEqual(Shipped.ConeDegrees, burst.FanDegrees, 1e-4f, "the specks stay inside the cone");
            Assert.AreEqual(0f, burst.Delay);
        }

        [Test]
        public void ASlowerHit_PuffsLess_AndOneUnderTheLeastSpeed_Nothing()
        {
            Assert.IsFalse(Hit(Slot, Vector2.right, Shipped.MinSpeed * 0.9f, false, 0f, out _), "a soft touch");

            var middling = (Shipped.MinSpeed + Shipped.FullSpeed) * 0.5f;
            Assert.IsTrue(Hit(Slot, Vector2.right, middling, false, 0f, out var soft));
            Assert.IsTrue(Hit(OtherSlot, Vector2.right, Shipped.FullSpeed, false, 0f, out var full));

            Assert.Less(soft.Length, full.Length);
            Assert.Less(soft.EndWidth, full.EndWidth);
            Assert.Less(soft.Smoke, full.Smoke);
            Assert.Less(soft.Specks, full.Specks);
            Assert.Less(soft.SpeckSpeed, full.SpeckSpeed);
            Assert.Greater(soft.Specks, 0, "but it is still a puff");
        }

        [Test]
        public void TheHitThatPaysAChain_PuffsMore()
        {
            Assert.IsTrue(Hit(Slot, Vector2.right, Shipped.FullSpeed, false, 0f, out var plain));
            Assert.IsTrue(Hit(OtherSlot, Vector2.right, Shipped.FullSpeed, true, 0f, out var paid));

            Assert.Greater(paid.Specks, plain.Specks);
            Assert.AreEqual(Mathf.RoundToInt(Shipped.Specks * Shipped.PaidScale), paid.Specks);
            Assert.AreEqual(plain.Length, paid.Length, 1e-5f, "more of it, not a longer cone");
        }

        [Test]
        public void AMushroomThatPuffed_KeepsQuietForItsRefractoryTime_AndOnlyThatMushroom()
        {
            Assert.IsTrue(Hit(Slot, Vector2.right, Shipped.FullSpeed, false, 5f, out _));

            Assert.IsFalse(Hit(Slot, Vector2.up, Shipped.FullSpeed, false, 5f + 0.08f, out _), "a rally's next knock");
            Assert.IsFalse(Hit(Slot, Vector2.up, Shipped.FullSpeed, false, 5f + (Shipped.Refractory * 0.95f), out _));
            Assert.IsTrue(Hit(OtherSlot, Vector2.up, Shipped.FullSpeed, false, 5f + 0.08f, out _), "the cap it came from");
            Assert.IsTrue(Hit(Slot, Vector2.up, Shipped.FullSpeed, false, 5f + Shipped.Refractory + 0.01f, out _));
        }

        [Test]
        public void ARally_IsAFewPuffs_NotOneAKnock()
        {
            // Two walls, a knock every 80 ms between them for two seconds.
            var puffs = 0;
            for (var i = 0; i < 25; i++)
            {
                if (Hit(i % 2 == 0 ? Slot : OtherSlot, Vector2.right, Shipped.FullSpeed, false, i * 0.08f, out _))
                {
                    puffs++;
                }
            }

            Assert.That(puffs, Is.InRange(6, 16), "of 25 knocks");
        }

        [Test]
        public void ACut_PuffsALittleMoreThanAHit_AlongTheLancesWay_FromTheCut()
        {
            var point = Centre + new Vector2(0f, 0.1f);

            Assert.IsTrue(_planner.TryCut(Shipped, point, new Vector2(2f, 0f), 0f, out var cut));

            Assert.AreEqual(MushroomSporeKind.Cut, cut.Kind);
            AssertVector(Vector2.right, cut.Direction, "the lance's way");
            AssertVector(point, cut.Origin, "from the cut");
            Assert.Greater(cut.Specks, Shipped.Specks);
            Assert.Greater(cut.Length, Shipped.Length);
            Assert.AreEqual(0f, cut.Delay);
        }

        [Test]
        public void ABlast_IsTheBiggest_Wide_FromWhereTheCapSat_AndWaitsForTheBombsCloudToThin()
        {
            Assert.IsTrue(_planner.TryCut(Shipped, Centre, Vector2.right, 0f, out var cut));
            var planner = new MushroomSporePlanner();

            Assert.IsTrue(planner.TryBlast(Shipped, Centre, new Vector2(0f, 3f), 0f, out var blast));

            Assert.AreEqual(MushroomSporeKind.Blast, blast.Kind);
            AssertVector(Vector2.up, blast.Direction, "the way the cap is blown");
            AssertVector(Centre, blast.Origin, "from where the cap sat");
            Assert.AreEqual(Mathf.RoundToInt(Shipped.Specks * Shipped.BlastScale), blast.Specks);
            Assert.Greater(blast.Specks, 2 * cut.Specks, "a hit, a cut, a blast: three steps");
            Assert.Greater(blast.EndWidth, 1.5f * cut.EndWidth, "a cloud, not a cone");
            Assert.Greater(blast.FanDegrees, 3f * cut.FanDegrees);
            Assert.Less(blast.Smoke, cut.Smoke, "and thinner: the smoke field keeps a wide cloud much longer");
            Assert.AreEqual(Shipped.BlastDelay, blast.Delay);
            Assert.Greater(blast.Delay, 0.2f);
        }

        [Test]
        public void ABlastCentredOnTheCap_GoesStraightUp()
        {
            Assert.IsTrue(_planner.TryBlast(Shipped, Centre, Vector2.zero, 0f, out var blast));

            AssertVector(Vector2.up, blast.Direction, "as the message has it");
        }

        [Test]
        public void SevenCapsBlownAtOnce_ShareTheBoardsBudget_TheFirstWhole_TheLastSmall_AndNoneNothing()
        {
            var specks = 0;
            var smoke = 0f;
            var least = int.MaxValue;
            var most = 0;
            for (var i = 0; i < 7; i++)
            {
                Assert.IsTrue(_planner.TryBlast(Shipped, Centre, Vector2.up, 0.02f * i, out var blast));
                specks += blast.Specks;
                smoke += blast.Smoke;
                least = Mathf.Min(least, blast.Specks);
                most = Mathf.Max(most, blast.Specks);
            }

            var whole = Mathf.RoundToInt(Shipped.Specks * Shipped.BlastScale);
            Assert.AreEqual(whole, most, "the first is a whole burst");
            Assert.Greater(least, 0, "and the last still shows");
            Assert.Less(least, whole / 2);
            Assert.Less(specks, 4.5f * whole, "seven caps let go well under seven bursts");
            Assert.Less(smoke, 2.5f);
        }

        [Test]
        public void ASpentBoard_LetsNoHitGo_UntilItsBudgetComesBack()
        {
            for (var i = 0; i < 7; i++)
            {
                _planner.TryBlast(Shipped, Centre, Vector2.up, 0f, out _);
            }

            Assert.IsFalse(Hit(Slot, Vector2.right, Shipped.FullSpeed, false, 0.01f, out _), "the board is spent");
            Assert.IsFalse(_planner.TryCut(Shipped, Centre, Vector2.right, 0.02f, out _));

            Assert.IsTrue(Hit(OtherSlot, Vector2.right, Shipped.FullSpeed, false, 2f, out var later));
            Assert.AreEqual(Shipped.Specks, later.Specks, "two seconds on, a hit is whole again");
        }

        [Test]
        public void AHitWithNoDirection_LetsNothingGo()
        {
            Assert.IsFalse(Hit(Slot, Vector2.zero, Shipped.FullSpeed, false, 0f, out _));
        }

        [Test]
        public void TheShippedTuning_IsAConeTheEyeCanFollow_LongerThanACap_AndThreeSteps()
        {
            Assert.That(Shipped.Length, Is.InRange(0.8f, 1.3f), "longer than a cap is wide, which is 0.6");
            Assert.AreEqual(Shipped.Length * 0.6f, Shipped.EndWidth, 0.1f, "a cap wide at its end");
            Assert.That(Shipped.ConeDegrees, Is.InRange(20f, 40f));
            Assert.That(Shipped.Refractory, Is.InRange(0.15f, 0.5f));
            Assert.That(Shipped.Specks, Is.InRange(8, 12));
            Assert.Less(1f, Shipped.CutScale);
            Assert.Less(2f * Shipped.CutScale, Shipped.BlastScale);
            Assert.Greater(Shipped.SpeckGreen.g, Shipped.SpeckGreen.r, "greenish");
            Assert.Greater(Shipped.SpeckBlue.b, Shipped.SpeckBlue.r, "to bluish");
        }

        private bool Hit(
            Vector2Int slot, Vector2 incoming, float speed, bool paid, float now, out MushroomSporeBurst burst)
        {
            return _planner.TryHit(Shipped, Radius, slot, Centre, incoming, speed, paid, now, out burst);
        }

        private static void AssertVector(Vector2 expected, Vector2 actual, string why)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, why);
            Assert.AreEqual(expected.y, actual.y, 1e-4f, why);
        }
    }
}

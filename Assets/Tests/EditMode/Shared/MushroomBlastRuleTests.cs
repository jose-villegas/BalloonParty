using BalloonParty.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     The sway a bomb's blast gives the mushrooms it does not take the caps off
    ///     (<see cref="MushroomBlastRule" />): how far it reaches, how hard it is with distance, and which way.
    /// </summary>
    [TestFixture]
    public class MushroomBlastRuleTests
    {
        private const float Tolerance = 1e-5f;

        // The shipped numbers: the bomb's kill radius, the mushroom's radius, the slots' spacing.
        private const float KillRadius = 1.25f;
        private const float CapRadius = 0.3f;
        private const float Spacing = 0.75f;

        // The reach is the blast's reach for a balloon: the kill radius, times the share, and the cap's radius.
        [TestCase(1f, 1.55f)]
        [TestCase(1.6f, 2.3f)]
        [TestCase(0.5f, 0.925f)]
        public void SwayReach_IsTheKillRadiusTimesTheShare_AndTheCapsOwnRadius(float share, float reach)
        {
            Assert.AreEqual(
                reach, MushroomBlastRule.SwayReach(KillRadius, CapRadius, new MushroomBlastTuning(share)), Tolerance);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void SwayReach_WithNoShare_IsNothing_SoTheCapsRadiusAloneSwaysNoMushroom(float share)
        {
            Assert.AreEqual(0f, MushroomBlastRule.SwayReach(KillRadius, CapRadius, new MushroomBlastTuning(share)));
            Assert.AreEqual(0f, MushroomBlastRule.SwayReach(KillRadius, CapRadius, default), "unauthored: off");
        }

        [Test]
        public void SwayReach_OfACapWithANegativeRadius_AddsNothing()
        {
            Assert.AreEqual(
                KillRadius, MushroomBlastRule.SwayReach(KillRadius, -0.5f, new MushroomBlastTuning(1f)), Tolerance);
        }

        // 1 at the distance of the slots next to the bomb, 0 at the reach, a straight line between.
        [TestCase(0.75f, 1f)]
        [TestCase(1.15f, 0.5f)]
        [TestCase(1.3f, 0.3125f)]
        [TestCase(1.5f, 0.0625f)]
        [TestCase(1.55f, 0f)]
        [TestCase(2.4f, 0f)]
        public void SwayStrength_FallsInAStraightLine_FromTheNearestSlotToTheReach(float distance, float strength)
        {
            Assert.AreEqual(strength, MushroomBlastRule.SwayStrength(distance, Spacing, 1.55f), 1e-4f);
        }

        // A mushroom nearer than the slots next to the bomb (a rainbow bomb's leftover, a board of another
        // spacing) sways no harder than at full.
        [Test]
        public void SwayStrength_NearerThanTheNearestSlot_IsStillOne()
        {
            Assert.AreEqual(1f, MushroomBlastRule.SwayStrength(0.2f, Spacing, 1.55f));
            Assert.AreEqual(1f, MushroomBlastRule.SwayStrength(0f, Spacing, 1.55f));
        }

        // A reach that ends before the nearest slot has no slope to fall along: at full inside it, nothing at
        // it and past it.
        [Test]
        public void SwayStrength_WithAReachInsideTheNearestSlot_IsAllOrNothing()
        {
            Assert.AreEqual(1f, MushroomBlastRule.SwayStrength(0.3f, Spacing, 0.5f));
            Assert.AreEqual(0f, MushroomBlastRule.SwayStrength(0.5f, Spacing, 0.5f));
            Assert.AreEqual(0f, MushroomBlastRule.SwayStrength(0.6f, Spacing, 0.5f));
        }

        [Test]
        public void SwayStrength_WithNoReach_IsNothingAnywhere()
        {
            Assert.AreEqual(0f, MushroomBlastRule.SwayStrength(0f, Spacing, 0f));
            Assert.AreEqual(0f, MushroomBlastRule.SwayStrength(1f, Spacing, 0f));
        }

        [Test]
        public void Away_IsTheUnitDirectionFromTheBlastToTheCap()
        {
            var away = MushroomBlastRule.Away(new Vector2(1f, 1f), new Vector2(4f, 5f));

            Assert.AreEqual(0.6f, away.x, Tolerance);
            Assert.AreEqual(0.8f, away.y, Tolerance);
        }

        [Test]
        public void Away_FromABlastOnTheCap_IsStraightUp()
        {
            Assert.AreEqual(Vector2.up, MushroomBlastRule.Away(new Vector2(2f, 3f), new Vector2(2f, 3f)));
        }

        [TestCase(1f, 1f)]
        [TestCase(0f, 0f)]
        [TestCase(-3f, 0f)]
        public void TheTuningsShare_IsNeverUnderZero(float authored, float read)
        {
            Assert.AreEqual(read, new MushroomBlastTuning(authored).SwayShare);
        }
    }
}

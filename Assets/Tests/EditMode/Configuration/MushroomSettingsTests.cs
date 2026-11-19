using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    /// <summary>
    ///     What <see cref="MushroomSettings.Validate" /> tells the author: nothing about the shipped values, and one
    ///     message for each number that cannot work with the others.
    /// </summary>
    [TestFixture]
    public class MushroomSettingsTests
    {
        [Test]
        public void Validate_Shipped_HasNothingToSay()
        {
            CollectionAssert.IsEmpty(Issues(new MushroomSettings()));
        }

        [Test]
        public void Validate_CapOnBouncesUnderWhatAChainTakes_SaysSo()
        {
            var settings = With("_rules", new MushroomChainRules(3, maxBouncesPerStretch: 2));

            Assert.That(Issues(settings), Has.Some.Contains("before it is paid"));
            CollectionAssert.IsEmpty(
                Issues(With("_rules", new MushroomChainRules(3, maxBouncesPerStretch: 3))),
                "a cap of as many bounces as the chain takes is enough: nothing is paid past the bounce that arms");
        }

        // How long a cut cap stays gone: five of the board's beats as shipped, and never under one, so a cap an
        // author set to regrow at once still waits for a beat.
        [TestCase(5, 5)]
        [TestCase(1, 1)]
        [TestCase(0, 1)]
        [TestCase(-3, 1)]
        public void RegrowBeats_IsWhatWasAuthored_AndNeverUnderOne(int authored, int read)
        {
            Assert.AreEqual(5, new MushroomSettings().RegrowBeats, "the owner's five beats");

            Assert.AreEqual(read, With("_regrowBeats", authored).RegrowBeats);
        }

        // How far a bomb's sway reaches, as a share of its kill radius: wider than the blast's own reach as
        // shipped (at 1 only the faint end of a sway is ever sent), and never under nothing, which turns the sway
        // off.
        [TestCase(1f, 1f)]
        [TestCase(2.4f, 2.4f)]
        [TestCase(0f, 0f)]
        [TestCase(-2f, 0f)]
        public void BlastSwayShare_IsWhatWasAuthored_AndNeverUnderZero(float authored, float read)
        {
            Assert.AreEqual(2.4f, new MushroomSettings().Blast.SwayShare, "wide enough for the sway to be seen");

            Assert.AreEqual(read, With("_blast", new MushroomBlastTuning(authored)).Blast.SwayShare);
        }

        // Decision J: nothing banks a charge. The shipped rules arm on the third hit and have no other reward.
        [Test]
        public void TheShippedRules_ArmOnTheThirdHit_AndBankNothing()
        {
            var rules = new MushroomSettings().Rules;
            var state = default(MushroomChainState);

            Assert.AreEqual(3, rules.ChainTarget);
            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, 0, rules));
            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, 1, rules));
            Assert.AreEqual(MushroomReward.Arm, MushroomChainRule.OnBounce(ref state, 0, rules));
            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, 1, rules), "the fourth pays nothing");
        }

        [Test]
        public void Validate_KickOffsetsCrossed_SaysSo()
        {
            var settings = With("_kick", new BumperKickTuning(coneDegrees: 30f, minOffset: 0.6f, maxOffset: 0.3f));

            Assert.That(Issues(settings), Has.Some.Contains("one turn to make"));
        }

        [Test]
        public void Validate_ConeTooNarrowToSendTheBallBack_SaysSo()
        {
            var settings = With("_kick", new BumperKickTuning(coneDegrees: 20f, minOffset: 0.2f, maxOffset: 0.9f));

            Assert.That(Issues(settings), Has.Some.Contains("cannot send the ball back"));
        }

        [Test]
        public void Validate_ALeanThatStandsTheCapBesideItsStem_SaysSo()
        {
            var settings = With("_lean", new MushroomLeanTuning(
                leanPerSpeed: 0.011f, maxLean: 0.2f, frequency: 3.6f, damping: 0.16f, rewardPulse: 1f,
                restLean: 0.0015f, swayLean: 0.13f));

            Assert.That(Issues(settings), Has.Some.Contains("stands the cap beside its stem"));
        }

        [Test]
        public void Validate_ARockingThatCarriesTheShippedLeanTooFar_SaysSo()
        {
            // The shipped lean alone is half the radius; the rocking comes on top of it.
            var settings = With("_idle", new MushroomIdleTuning(
                breath: 0.08f, breathSeconds: 2.6f, phaseSpread: 0.1f, waveAcross: 0.2f, shadowLift: 0.8f,
                glowSwing: 0.4f, wartSpread: 0.08f, rockPerDisplacement: 0.16f, rockMost: 0.1f, rockSeconds: 0.45f,
                rockSwing: 0.6f));

            Assert.That(Issues(settings), Has.Some.Contains("stands the cap beside its stem"));
        }

        [Test]
        public void Validate_SpacingUnderTwoCaps_SaysSo()
        {
            var settings = With("_placement", Placement(minSpacing: 0.5f));

            Assert.That(Issues(settings), Has.Some.Contains("no two walls close on a ball"));
        }

        [Test]
        public void Validate_FewestWallsOverMost_SaysSo()
        {
            var settings = With("_placement", Placement(wallRange: new Vector2Int(4, 2)));

            Assert.That(Issues(settings), Has.Some.Contains("the fewest walls (4) is over the most (2)"));
        }

        [Test]
        public void Validate_FewestCapsOverMost_SaysSo()
        {
            var settings = With("_placement", Placement(capRange: new Vector2Int(7, 5)));

            Assert.That(Issues(settings), Has.Some.Contains("the fewest caps (7) is over the most (5)"));
        }

        [Test]
        public void Validate_MostCapsUnderMostWalls_SaysSo()
        {
            var settings = With("_placement", Placement(capRange: new Vector2Int(2, 3)));

            Assert.That(Issues(settings), Has.Some.Contains("a wall is at least one cap"));
        }

        [Test]
        public void Validate_CapsThatLeaveTheBushesUnderTwoSlots_SaysSo()
        {
            var settings = With("_placement", Placement(capRange: new Vector2Int(4, 8)));

            Assert.That(Issues(settings), Has.Some.Contains("leaves the bushes fewer than 2 of the 9 slots"));
        }

        [Test]
        public void Validate_ABandWithItsEndsCrossed_SaysSo()
        {
            var settings = With("_placement", Placement(band: new MushroomLayoutBand(new Vector2(0.2f, 0.1f), 2f, 20)));

            Assert.That(Issues(settings), Has.Some.Contains("no layout can be accepted"));
        }

        [Test]
        public void Validate_ACeilingThatLetsNoAngleMeetACap_SaysSo()
        {
            var settings = With("_placement", Placement(cannon: new MushroomCannonClearance(3.25f, 4f, 0f)));

            Assert.That(Issues(settings), Has.Some.Contains("only the far end of the board"));
        }

        // The cannon's room as shipped: no cap nearer its pivot than 3.25, and a cap met within 4 of the muzzle on
        // no more than a fifth of the aim angles, which of 511 is 102.
        [TestCase(3.25f, 102, true)]
        [TestCase(5f, 0, true)]
        [TestCase(3.24f, 102, false)]
        [TestCase(3.25f, 103, false)]
        [TestCase(2.66f, 0, false)]
        public void TheCannonsRoom_AllowsALayoutByHowItStandsToTheCannon(float nearestCap, int shadowed, bool allowed)
        {
            var cannon = new MushroomSettings().Placement.Cannon;

            Assert.AreEqual(3.25f, cannon.MinDistance, 1e-5f);
            Assert.AreEqual(4f, cannon.NearRange, 1e-5f);
            Assert.AreEqual(0.2f, cannon.MaxShadowShare, 1e-5f);
            Assert.AreEqual(allowed, cannon.Allows(nearestCap, shadowed, 511));
        }

        [Test]
        public void Validate_ACountOfWallsTheCatalogueHasNoLayoutFor_SaysSo()
        {
            var settings = With("_placement", Placement(wallRange: new Vector2Int(2, 5)));

            Assert.That(Issues(settings), Has.Some.Contains("the baked catalogue has no layout of 5 walls"));
            Assert.That(Issues(settings), Has.None.Contains("no layout of 4 walls"));
        }

        [Test]
        public void TheShippedValues_AreWhatTheOwnerChose()
        {
            var shipped = new MushroomSettings();
            var limits = shipped.Placement;

            Assert.AreEqual(20f, shipped.Kick.ConeDegrees, "the cone, narrowed to 20 degrees");
            Assert.AreEqual(2, limits.MinCount, "two walls on a stretch's first level");
            Assert.AreEqual(4, limits.MaxCount, "four on its last");
            Assert.AreEqual(4, limits.MinCaps);
            Assert.AreEqual(7, limits.MaxCaps, "four to seven caps in all");
            Assert.AreEqual(1.9f, limits.MinSpacing, 1e-5f);
            Assert.GreaterOrEqual(shipped.PermanentSlotLimit - limits.MaxCaps, 2, "and the bushes keep two slots");
        }

        [Test]
        public void TheCone_IsWiderThanTwoWallsNeedToSendTheBallBack()
        {
            var kick = new MushroomSettings().Kick;

            Assert.Greater(kick.ConeDegrees, 2f * Mathf.Asin(kick.MinOffset) * Mathf.Rad2Deg);
        }

        [Test]
        public void MaxCapsFor_IsEveryWallAtItsLongest_ButNeverOverTheMostCaps()
        {
            var limits = new MushroomSettings().Placement;

            Assert.AreEqual(0, limits.MaxCapsFor(0));
            Assert.AreEqual(3, limits.MaxCapsFor(1));
            Assert.AreEqual(6, limits.MaxCapsFor(2));
            Assert.AreEqual(7, limits.MaxCapsFor(3));
            Assert.AreEqual(7, limits.MaxCapsFor(4));
        }

        [Test]
        public void MinSeparation_IsTwoContactRadiiAndTheMargin_AndTheSpacingIsNeverUnderIt()
        {
            var shipped = new MushroomSettings().Placement;
            var tight = Placement(minSpacing: 0.1f);

            Assert.AreEqual(0.9f, shipped.MinSeparation(0.425f), 1e-5f);
            Assert.AreEqual(1.9f, shipped.EffectiveMinSpacing(0.425f), 1e-5f);
            Assert.AreEqual(0.9f, tight.EffectiveMinSpacing(0.425f), 1e-5f, "The ball's room under a spacing of less");
        }

        // A limit saved before the spacing existed loads the field as 0: that is the shipped spacing, not none.
        [Test]
        public void TheSpacing_AuthoredAtNothing_ReadsAsTheShippedOne()
        {
            Assert.AreEqual(MushroomPlacementLimits.DefaultMinSpacing, Placement(minSpacing: 0f).MinSpacing);
            Assert.AreEqual(MushroomPlacementLimits.DefaultMinSpacing, default(MushroomPlacementLimits).MinSpacing);
        }

        [Test]
        public void Allows_OnlyTheSlotsOffTheColumnsAndRowsKeptClear()
        {
            var limits = new MushroomSettings().Placement;
            var grid = new Vector2Int(6, 11);

            Assert.IsTrue(limits.Allows(new Vector2Int(1, 1), grid), "the first slot in");
            Assert.IsTrue(limits.Allows(new Vector2Int(4, 8), grid), "the last slot in");
            Assert.IsFalse(limits.Allows(new Vector2Int(0, 4), grid), "the left column");
            Assert.IsFalse(limits.Allows(new Vector2Int(5, 4), grid), "the right column");
            Assert.IsFalse(limits.Allows(new Vector2Int(2, 0), grid), "the top row");
            Assert.IsFalse(limits.Allows(new Vector2Int(2, 9), grid), "the bottom two rows");
            Assert.IsFalse(limits.WithBottomRows(5).Allows(new Vector2Int(2, 6), grid), "and more of them when told");
        }

        [Test]
        public void WithBottomRows_ChangesTheBottomRows_AndNothingElse()
        {
            var shipped = new MushroomSettings().Placement;

            var changed = shipped.WithBottomRows(6);

            Assert.AreEqual(6, changed.BottomRowsKeptClear);
            Assert.AreEqual(shipped.MinSpacing, changed.MinSpacing);
            Assert.AreEqual(shipped.MinCount, changed.MinCount);
            Assert.AreEqual(shipped.MaxCount, changed.MaxCount);
            Assert.AreEqual(shipped.MinCaps, changed.MinCaps);
            Assert.AreEqual(shipped.MaxCaps, changed.MaxCaps);
            Assert.AreEqual(shipped.EdgeColumnsKeptClear, changed.EdgeColumnsKeptClear);
            Assert.AreEqual(shipped.TopRowsKeptClear, changed.TopRowsKeptClear);
            Assert.AreEqual(shipped.Band.MinArmingShare, changed.Band.MinArmingShare);
            Assert.AreEqual(shipped.Band.MaxArmingShare, changed.Band.MaxArmingShare);
            Assert.AreEqual(shipped.Cannon.MinDistance, changed.Cannon.MinDistance);
            Assert.AreEqual(shipped.Cannon.NearRange, changed.Cannon.NearRange);
            Assert.AreEqual(shipped.Cannon.MaxShadowShare, changed.Cannon.MaxShadowShare);
        }

        // The band as shipped: 7 to 16 in 100 aim angles arm, in a window of 2 degrees or more, on 20 angles or
        // more. Of 511 angles a third of a degree apart, that is 36 to 81 of them and a run of 6.
        [TestCase(36, 6, true)]
        [TestCase(81, 6, true)]
        [TestCase(35, 6, false)]
        [TestCase(82, 6, false)]
        [TestCase(50, 5, false)]
        [TestCase(50, 30, true)]
        public void TheBand_AcceptsALayoutByWhatItMeasures(int armingAngles, int widestRun, bool accepted)
        {
            var band = new MushroomSettings().Placement.Band;

            Assert.AreEqual(accepted, band.Accepts(armingAngles, widestRun, 511, 1f / 3f));
        }

        [Test]
        public void TheBand_HoldsAFloorOfArmingAngles_WhateverShareThatIs()
        {
            var band = new MushroomLayoutBand(new Vector2(0f, 1f), minWindowDegrees: 0f, minArmingAngles: 20);

            Assert.IsFalse(band.Accepts(19, 19, 511, 1f / 3f));
            Assert.IsTrue(band.Accepts(20, 20, 511, 1f / 3f));
            Assert.IsFalse(band.Accepts(20, 20, 0, 1f / 3f), "an aim grid of no angles accepts nothing");
        }

        private static List<string> Issues(MushroomSettings settings)
        {
            var issues = new List<string>();
            settings.Validate("Test", issues);
            return issues;
        }

        // The shipped settings with one of their blocks replaced.
        private static MushroomSettings With(string field, object value)
        {
            var settings = new MushroomSettings();
            typeof(MushroomSettings)
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(settings, value);
            return settings;
        }

        // The shipped placement limits with the given numbers changed.
        private static MushroomPlacementLimits Placement(
            Vector2Int? wallRange = null, Vector2Int? capRange = null, float? minSpacing = null,
            MushroomLayoutBand? band = null, MushroomCannonClearance? cannon = null)
        {
            var shipped = new MushroomSettings().Placement;
            return new MushroomPlacementLimits(
                wallRange ?? new Vector2Int(shipped.MinCount, shipped.MaxCount),
                capRange ?? new Vector2Int(shipped.MinCaps, shipped.MaxCaps),
                minSpacing ?? shipped.MinSpacing,
                new Vector3Int(shipped.EdgeColumnsKeptClear, shipped.TopRowsKeptClear, shipped.BottomRowsKeptClear),
                band ?? shipped.Band,
                cannon ?? shipped.Cannon);
        }
    }
}

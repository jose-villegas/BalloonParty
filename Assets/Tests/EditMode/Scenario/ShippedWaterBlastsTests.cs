using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Scenario.View;
using BalloonParty.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Tests.Scenario
{
    /// <summary>
    ///     The blasts as shipped, against the water they land in: their numbers sit in three config assets and two
    ///     materials, and the look depends on how they stand to each other. A bomb and the nuke dig past the depth the
    ///     bottom shows bare at. A piercing shot's wake is two furrows: its wave stays above the depth the water starts
    ///     to thin at, and its heart bares a sliver a small fraction of a bomb's crater across. Where its charge ends
    ///     at a wall it leaves a crater smaller than a bomb's every way.
    /// </summary>
    [TestFixture]
    public class ShippedWaterBlastsTests
    {
        private const float StepSeconds = 1f / 120f;

        private float _radiusScale;
        private float _depthPerStrength;
        private float _thinsFrom;
        private float _bareAt;
        private WaterCrater _bomb;
        private WaterCrater _nuke;
        private WaterCrater _wave;
        private WaterCrater _heart;
        private WaterCrater _wall;

        [SetUp]
        public void SetUp()
        {
            var ripple = Load<Material>("Assets/Materials/Scenario/WaterRipple.mat");
            var water = Load<Material>("Assets/Materials/Scenario/Water.mat");
            _radiusScale = ripple.GetFloat("_BlastRadius");
            _depthPerStrength = ripple.GetFloat("_BlastDepth");
            _thinsFrom = water.GetFloat("_BareFrom");
            _bareAt = water.GetFloat("_BareAt");

            var bomb = Load<ItemConfiguration>("Assets/Configuration/ItemConfiguration.asset")[ItemType.Bomb].Bomb;
            IBalloonsConfiguration balloons = Load<BalloonsConfiguration>("Assets/Configuration/BalloonsConfiguration.asset");
            var nuke = balloons.NukeBlast;
            IProjectileFlightConfig flight = Load<ProjectileFlightConfig>("Assets/Configuration/ProjectileFlightConfig.asset");
            _bomb = Crater(bomb.Radius, bomb.ImpactStrength, bomb.ImpactSeconds);
            _nuke = Crater(nuke.ImpactRadius, nuke.ImpactStrength, nuke.ImpactSeconds);
            // One fixed step of the fastest lance's flight: 93 world units a second, 50 steps a second.
            var stretch = new Vector2(0f, 1.86f);
            _wave = Crater(flight.PierceWakeWaveRadius, flight.PierceWakeWaveStrength, 0.02f, stretch);
            _heart = Crater(flight.PierceWakeHeartRadius, flight.PierceWakeHeartStrength, 0.02f, stretch);
            _wall = Crater(flight.PierceWallImpactRadius, flight.PierceWallImpactStrength, flight.PierceWallImpactSeconds);
        }

        [Test]
        public void TheWaterThinsBeforeItIsGone()
        {
            Assert.Greater(_thinsFrom, 0.5f, "ordinary impacts dip to about 0.45 rest depths: they must not thin the water");
            Assert.Greater(_bareAt, _thinsFrom);
        }

        [Test]
        public void ABombsCrater_IsAboutItsKillRadius_AndBaresTheFloor()
        {
            var bomb = Load<ItemConfiguration>("Assets/Configuration/ItemConfiguration.asset")[ItemType.Bomb].Bomb;

            Assert.That(_bomb.Radius, Is.InRange(bomb.Radius * 0.7f, bomb.Radius * 1.1f));
            Assert.Greater(_bomb.Depth, _bareAt);
        }

        [Test]
        public void TheNukesCrater_IsWiderAndDeeperThanABombs_AndOpenLonger()
        {
            Assert.Greater(_nuke.Radius, 2f * _bomb.Radius);
            Assert.Greater(_nuke.Depth, _bomb.Depth);
            Assert.Greater(_nuke.Seconds, _bomb.Seconds);
        }

        [Test]
        public void AWakesWave_NeverThinsTheWaterByItself()
        {
            Assert.Greater(_wave.Depth, 0f);
            Assert.LessOrEqual(_wave.Depth, _thinsFrom + 1e-4f);
            Assert.Greater(_wave.Radius, _bomb.Radius, "wide, so what it throws aside is a wave worth the name");
        }

        [Test]
        public void AWakesHeart_BaresTheFloor_OverFarLessThanABomb()
        {
            Assert.Greater(_heart.Depth, _bareAt);
            Assert.Less(_heart.Radius, 0.5f * _bomb.Radius);
        }

        // The heart's furrow floods back about a tenth of a second after the dig passes, so the dig must not fall far
        // behind the lance: a whole stretch of the fastest lance is dug within a tenth of a second.
        [Test]
        public void AWakesHeart_FollowsTheFastestLance_WithinATenthOfASecond()
        {
            Assert.LessOrEqual(_heart.Seconds, 0.1f);
        }

        // The wall throws back what the blast throws at it (the simulation mirrors a blast in its walls), which about
        // doubles what a blast at a wall digs: with that it bares the floor, and still digs less than a bomb.
        [Test]
        public void TheWallImpact_IsSmallerThanABomb_EveryWay()
        {
            Assert.Less(_wall.Radius, 0.75f * _bomb.Radius);
            Assert.Less(2f * _wall.Depth, _bomb.Depth);
            Assert.Greater(2f * _wall.Depth, _bareAt);
            Assert.Less(_wall.Seconds, _bomb.Seconds);
            Assert.IsFalse(_wall.Runs);
        }

        private WaterCrater Crater(float radius, float strength, float seconds, Vector2 line = default)
        {
            return WaterCrater.From(
                new ImpactEventBus.Blast(radius, strength, seconds, line), _radiusScale, _depthPerStrength, StepSeconds);
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"{path} should load");
            return asset;
        }
    }
}

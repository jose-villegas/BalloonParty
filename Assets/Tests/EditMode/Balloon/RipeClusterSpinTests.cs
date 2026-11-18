using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     A ripe cluster turns with the spiral arm it sheds. The arm's source turns
    ///     <see cref="IRipeSpin.TurnDirection" /> times <see cref="RipeBubbleRule.SpiralTurnRate" /> a second
    ///     (<c>RipeBubbleBurstView.AdvanceSource</c>), slow from the ripening and fast from the first fast throb. These
    ///     tests bind the cluster's own view binding to a real model, move its fuse, and read the spin it gives the
    ///     soap shader. The shader draws its bubbles turned back by the angle it is given
    ///     (<c>RipeBubbleRuleTests.ClusterSpin_TurnsTheDrawnClusterWithItsSpiralArm_SlowAndFast</c>), so the cluster
    ///     as drawn turns at minus that spin. Every strength and duration is 0, so nothing here tweens.
    /// </summary>
    [TestFixture]
    public class RipeClusterSpinTests
    {
        private const float TurnsPerSecond = 1f;
        private const float FuseSeconds = 6f;
        private const float FastSeconds = 2f;

        private GameObject _host;
        private SpriteRenderer _renderer;
        private SoapBubbleClusterVariant _variant;
        private BubbleClusterModel _cluster;
        private CompositeDisposable _binding;
        private MaterialPropertyBlock _block;

        [SetUp]
        public void SetUp()
        {
            var settings = Substitute.For<IRipeBubbleSettings>();
            settings.FuseSeconds.Returns(FuseSeconds);
            settings.ThrobFastSeconds.Returns(FastSeconds);
            settings.SpiralTurnsPerSecond.Returns(TurnsPerSecond);
            settings.SwellScale.Returns(1f);
            settings.FloatSpeedup.Returns(1f);
            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.BubbleMergeMax.Returns(6);
            balloons.RipeBubble.Returns(settings);

            _host = new GameObject("Cluster");
            _renderer = _host.AddComponent<SpriteRenderer>();
            _variant = _host.AddComponent<SoapBubbleClusterVariant>();
            typeof(SoapBubbleClusterVariant)
                .GetField("_balloons", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(_variant, balloons);

            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());
            _cluster = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: 3), palette);
            _cluster.SetBubbles(6);
            _binding = new CompositeDisposable();
            _block = new MaterialPropertyBlock();
            _variant.Bind(_cluster, _binding);
        }

        [TearDown]
        public void TearDown()
        {
            _binding.Dispose();
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void TheArmIsTold_TheWayTheClusterAlreadyDrifts()
        {
            var rest = ShaderSpin();

            Assert.AreNotEqual(0f, rest, "a bound cluster drifts round");
            Assert.AreEqual(Mathf.Sign(DrawnTurnRate()), ((IRipeSpin)_variant).TurnDirection);
        }

        [Test]
        public void ARipeCluster_TurnsAtItsArmsRate_SlowFromTheRipening_FastFromTheFirstFastThrob()
        {
            var direction = ((IRipeSpin)_variant).TurnDirection;

            _cluster.Ripen();
            _cluster.SetFuseLeft(FuseSeconds);
            Assert.AreEqual(
                direction * RipeBubbleRule.SpiralTurnRate(TurnsPerSecond, false), DrawnTurnRate(), 1e-4f,
                "slow, the arm's own pace, from the moment it ripens");

            _cluster.SetFuseLeft(4.9f);
            Assert.AreEqual(
                direction * RipeBubbleRule.SpiralTurnRate(TurnsPerSecond, false), DrawnTurnRate(), 1e-4f,
                "a slow throb leaves the pace");

            _cluster.SetFuseLeft(1.9f);
            Assert.AreEqual(
                direction * RipeBubbleRule.SpiralTurnRate(TurnsPerSecond, true), DrawnTurnRate(), 1e-4f,
                "the first fast throb steps it up, as the arm's beat does");

            _cluster.SetFuseLeft(1.7f);
            Assert.AreEqual(
                direction * RipeBubbleRule.SpiralTurnRate(TurnsPerSecond, true), DrawnTurnRate(), 1e-4f,
                "and it stays fast between throbs");
        }

        [Test]
        public void ACluster_GoesBackToItsOwnDrift_WhenItStopsBeingRipe()
        {
            var rest = ShaderSpin();
            _cluster.Ripen();
            _cluster.SetFuseLeft(1.9f);
            Assert.AreNotEqual(rest, ShaderSpin());

            _cluster.Unripen();

            Assert.AreEqual(rest, ShaderSpin(), 1e-6f);
        }

        [Test]
        public void DroppingTheBinding_PutsTheDriftBack()
        {
            var rest = ShaderSpin();
            _cluster.Ripen();
            _cluster.SetFuseLeft(FuseSeconds);

            _binding.Dispose();

            Assert.AreEqual(rest, ShaderSpin(), 1e-6f);
        }

        // The cluster as drawn turns against the spin the shader is given: counter-clockwise is positive, as the arm's
        // angle is.
        private float DrawnTurnRate()
        {
            return -ShaderSpin();
        }

        private float ShaderSpin()
        {
            _renderer.GetPropertyBlock(_block);
            return _block.GetFloat(SoapBubbleShader.RotationSpeedId);
        }
    }
}

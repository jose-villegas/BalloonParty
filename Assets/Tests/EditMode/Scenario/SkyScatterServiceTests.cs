using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Scenario;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Tests.Scenario
{
    // Only the two branch points inside SkyScatterService.Start() are covered here — the null-material
    // guard and the overscan clamp — mirroring SmokeFieldServiceTests' scope. GameObject/MeshRenderer
    // wiring itself is simple field assignment (too simple to break, per the Tests README) and is left
    // untested, matching BackgroundFieldService/DisturbanceFieldService precedent for this service family.
    [TestFixture]
    public class SkyScatterServiceTests
    {
        private ISkyScatterSettings _settings;
        private IGameDisplayConfiguration _display;
        private SkyScatterService _service;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<ISkyScatterSettings>();
            _display = Substitute.For<IGameDisplayConfiguration>();
            _service = new SkyScatterService(_settings, _display);
        }

        [TearDown]
        public void TearDown()
        {
            var quad = GetQuad();

            if (quad != null)
            {
                Object.DestroyImmediate(quad);
            }

            if (_material != null)
            {
                Object.DestroyImmediate(_material);
                _material = null;
            }
        }

        [Test]
        public void Start_NullMaterial_DoesNotThrow()
        {
            _settings.Material.Returns((Material)null);

            Assert.DoesNotThrow(() => ((IStartable)_service).Start());
        }

        [Test]
        public void Start_NullMaterial_DoesNotCreateQuad()
        {
            _settings.Material.Returns((Material)null);

            ((IStartable)_service).Start();

            Assert.IsNull(GetQuad());
        }

        [Test]
        public void Start_OverscanBelowOne_ClampsScaleToReferenceSize()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
            _settings.Material.Returns(_material);
            _settings.SortingLayerName.Returns("Default");
            _settings.OverscanMultiplier.Returns(0.3f);
            _display.ReferenceWorldWidth.Returns(10f);
            _display.ReferenceWorldHeight.Returns(6f);

            ((IStartable)_service).Start();

            // A misconfigured overscan below 1 must not shrink the quad below the reference viewport.
            var scale = GetQuad().transform.localScale;
            Assert.AreEqual(new Vector3(10f, 6f, 1f), scale);
        }

        [Test]
        public void Start_OverscanAboveOne_ScalesQuadByOverscan()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
            _settings.Material.Returns(_material);
            _settings.SortingLayerName.Returns("Default");
            _settings.OverscanMultiplier.Returns(2f);
            _display.ReferenceWorldWidth.Returns(10f);
            _display.ReferenceWorldHeight.Returns(6f);

            ((IStartable)_service).Start();

            var scale = GetQuad().transform.localScale;
            Assert.AreEqual(new Vector3(20f, 12f, 1f), scale);
        }

        private GameObject GetQuad()
        {
            var field = typeof(SkyScatterService).GetField("_quad", BindingFlags.NonPublic | BindingFlags.Instance);
            return (GameObject)field!.GetValue(_service);
        }
    }
}

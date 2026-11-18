using System.Collections.Generic;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The foam coat fits round the balloon's body in the balloon's own space: a balloon is held at zero scale
    ///     through its spawn flight while its model is already on the grid, so foam can coat it then, and a pooled view
    ///     keeps the fit it is given. The host's scale must not enter it.
    /// </summary>
    [TestFixture]
    public class FoamCoatOverlayTests
    {
        private const float PixelsPerUnit = 4f;

        private readonly List<Object> _created = new();

        private Transform _host;
        private Transform _pivot;
        private SpriteRenderer _body;

        [SetUp]
        public void SetUp()
        {
            // host -> pivot (offset, scaled) -> body (offset, scaled): a 4x4 sprite at 4 px a unit is 1 unit across.
            _host = NewObject("Balloon").transform;
            _pivot = NewObject("Pivot").transform;
            _pivot.SetParent(_host, false);
            _pivot.localPosition = new Vector3(0.1f, 0.2f, 0f);
            _pivot.localScale = new Vector3(0.8f, 0.8f, 1f);
            var bodyObject = NewObject("Body");
            bodyObject.transform.SetParent(_pivot, false);
            bodyObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            bodyObject.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            _body = bodyObject.AddComponent<SpriteRenderer>();
            _body.sprite = NewSprite();
        }

        [TearDown]
        public void TearDown()
        {
            // A child goes with its parent, so some are already gone.
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheBody_MeasuresFromTheLocalChain_AtRest()
        {
            FoamCoatOverlay.MeasureBody(_host, _body, out var centre, out var radius);

            // Body centre (0, 0.25) under the pivot's 0.8 scale and (0.1, 0.2) offset; radius 0.5 x 1.5 x 0.8.
            AssertNear(new Vector2(0.1f, 0.4f), centre);
            Assert.AreEqual(0.6f, radius, 1e-4f);
        }

        [Test]
        public void AHostAtZeroScale_MeasuresAsAtRest()
        {
            _host.localScale = Vector3.zero;

            FoamCoatOverlay.MeasureBody(_host, _body, out var centre, out var radius);

            AssertNear(new Vector2(0.1f, 0.4f), centre);
            Assert.AreEqual(0.6f, radius, 1e-4f);
        }

        [Test]
        public void AHostAtANonUnitScale_UnderAScaledParent_MeasuresAsAtRest()
        {
            var parent = NewObject("Board").transform;
            parent.localScale = new Vector3(3f, 3f, 1f);
            parent.position = new Vector3(5f, -2f, 0f);
            _host.SetParent(parent, false);
            _host.localScale = new Vector3(1.37f, 1.37f, 1f);

            FoamCoatOverlay.MeasureBody(_host, _body, out var centre, out var radius);

            AssertNear(new Vector2(0.1f, 0.4f), centre);
            Assert.AreEqual(0.6f, radius, 1e-4f);
        }

        [Test]
        public void ALinkAtZeroScale_CountsAsOne_AndGivesNoNaN()
        {
            _pivot.localScale = Vector3.zero;

            FoamCoatOverlay.MeasureBody(_host, _body, out var centre, out var radius);

            AssertNear(new Vector2(0.1f, 0.45f), centre);
            Assert.AreEqual(0.75f, radius, 1e-4f);
        }

        [Test]
        public void TheCoat_FitsTheSameAtZeroAndAtNonUnitHostScale()
        {
            var settings = Settings();
            _host.localScale = Vector3.zero;
            var overlay = FoamCoatOverlay.Create(_host, _body, settings);
            overlay.Show();
            var atZero = overlay.transform.localScale;
            var atZeroPosition = overlay.transform.localPosition;

            _host.localScale = new Vector3(2.2f, 2.2f, 1f);
            overlay.Hide();
            overlay.Show();

            Assert.IsTrue(float.IsFinite(atZero.x) && atZero.x > 0f, $"a usable scale, got {atZero.x}");
            AssertNear(atZero, overlay.transform.localScale);
            AssertNear(atZeroPosition, overlay.transform.localPosition);
            AssertNear(new Vector3(0.1f, 0.4f, 0f), atZeroPosition);
        }

        [Test]
        public void TheCoat_ReFitsOnEveryShow_ToTheBodysCurrentRestScale()
        {
            var settings = Settings();
            var overlay = FoamCoatOverlay.Create(_host, _body, settings);
            overlay.Show();
            var first = overlay.transform.localScale.x;

            _body.transform.localScale = new Vector3(3f, 3f, 1f);
            overlay.Show();

            Assert.AreEqual(first * 2f, overlay.transform.localScale.x, 1e-4f);
        }

        private IRipeBubbleSettings Settings()
        {
            var settings = Substitute.For<IRipeBubbleSettings>();
            var material = new Material(Shader.Find("Sprites/Default"));
            _created.Add(material);
            settings.CoatMaterial.Returns(material);
            settings.CoatSprite.Returns(NewSprite());
            settings.CoatScale.Returns(1.15f);
            return settings;
        }

        private GameObject NewObject(string name)
        {
            var created = new GameObject(name);
            _created.Add(created);
            return created;
        }

        private Sprite NewSprite()
        {
            var texture = new Texture2D(4, 4);
            _created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            _created.Add(sprite);
            return sprite;
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }
    }
}

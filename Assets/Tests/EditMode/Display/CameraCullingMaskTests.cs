using BalloonParty.Display;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Display
{
    /// <summary>
    ///     The main camera's mask has two writers, navigation and the effects map. Without one owner, a navigation
    ///     change while the map runs draws the effects twice, and the map going idle leaks them into the launch screen.
    /// </summary>
    [TestFixture]
    public class CameraCullingMaskTests
    {
        private const int Board = 1 << 10;
        private const int Effects = 1 << 17;
        private const int Launch = 1 << 12;

        private GameObject _go;
        private Camera _camera;
        private CameraCullingMask _mask;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CameraCullingMaskTests");
            _camera = _go.AddComponent<Camera>();
            _camera.cullingMask = Board | Effects;
            _mask = CameraCullingMask.Of(_camera);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Of_ReturnsTheSameOwner()
        {
            Assert.AreSame(_mask, CameraCullingMask.Of(_camera));
        }

        [Test]
        public void Claim_TakesTheLayersOutOfTheCamerasOwnMask()
        {
            _mask.Claim(Effects);

            Assert.AreEqual(Board, _camera.cullingMask);

            _mask.Release(Effects);

            Assert.AreEqual(Board | Effects, _camera.cullingMask);
        }

        [Test]
        public void SetBase_WhileClaimed_KeepsTheClaim()
        {
            _mask.Claim(Effects);

            _mask.SetBase(Board | Effects | Launch);

            Assert.AreEqual(Board | Launch, _camera.cullingMask);
        }

        [Test]
        public void Release_DoesNotBringBackALayerTheBaseDropped()
        {
            _mask.Claim(Effects);
            _mask.SetBase(Launch);

            _mask.Release(Effects);

            Assert.AreEqual(Launch, _camera.cullingMask);
        }
    }
}

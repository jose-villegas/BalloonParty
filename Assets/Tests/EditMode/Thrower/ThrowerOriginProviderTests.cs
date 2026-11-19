using BalloonParty.Shared;
using BalloonParty.Thrower;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Thrower
{
    /// <summary>
    ///     Where shots come from, for the code outside the thrower's scope (<see cref="ThrowerOriginProvider" />):
    ///     the cannon as it stands, and the point it rests at, which a board laid out for a whole level is planned
    ///     from.
    /// </summary>
    [TestFixture]
    public class ThrowerOriginProviderTests
    {
        private static readonly Vector2 Resting = new(0f, -4.6f);

        private GameObject _thrower;
        private GameObject _muzzle;
        private ThrowerOriginProvider _provider;

        [SetUp]
        public void SetUp()
        {
            // As the scene has it before the entrance: half a unit under where it comes to rest.
            _thrower = new GameObject("Thrower");
            _thrower.transform.position = new Vector3(0f, -5.1f, 0f);
            _muzzle = new GameObject("Muzzle");
            _muzzle.transform.SetParent(_thrower.transform, false);
            _muzzle.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            _provider = new ThrowerOriginProvider();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_thrower);
        }

        [Test]
        public void BeforeTheThrowerStarts_ThereIsNoOrigin()
        {
            IShotOrigin origin = _provider;

            Assert.IsFalse(origin.IsAvailable);
            Assert.AreEqual(Vector2.zero, origin.RestingPivot);
            Assert.AreEqual(0f, origin.ProjectileContactRadius);
        }

        [Test]
        public void TheRestingPivot_IsWhatItWasGiven_WhereverTheCannonStands()
        {
            _provider.Set(_muzzle.transform, _thrower.transform, 0.125f, Resting);
            IShotOrigin origin = _provider;

            Assert.IsTrue(origin.IsAvailable);
            Assert.AreEqual(Resting, origin.RestingPivot, "mid entrance, the cannon is still half a unit low");
            Assert.AreEqual(new Vector2(0f, -5.1f), _provider.Pivot, "and the live pivot says so");

            // Entered, then punched back by a shot's recoil.
            _thrower.transform.position = new Vector3(0.1f, -4.72f, 0f);

            Assert.AreEqual(Resting, origin.RestingPivot, "a layout is planned from where the cannon rests");
            Assert.AreEqual(new Vector2(0.1f, -4.72f), _provider.Pivot, "what follows the cannon still follows it");
            Assert.AreEqual(0.125f, origin.ProjectileContactRadius);
        }

        [Test]
        public void Clear_ForgetsTheRestingPivotToo()
        {
            _provider.Set(_muzzle.transform, _thrower.transform, 0.125f, Resting);

            _provider.Clear();

            Assert.IsFalse(_provider.IsAvailable);
            Assert.AreEqual(Vector2.zero, _provider.RestingPivot);
        }
    }
}

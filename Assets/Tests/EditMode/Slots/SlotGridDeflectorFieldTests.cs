using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class SlotGridDeflectorFieldTests
    {
        private ISlotGridConfig _config;
        private SlotGrid _grid;
        private SlotGridDeflectorField _field;
        private List<DeflectorCircle> _results;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<ISlotGridConfig>();
            _config.SlotsSize.Returns(new Vector2Int(6, 10));
            _config.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            _config.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(_config, new BalancePathHolder());
            _field = new SlotGridDeflectorField(_grid);
            _results = new List<DeflectorCircle>();
        }

        [Test]
        public void CollectDeflectors_StaticDeflector_IsCollected()
        {
            Place(new DeflectorActorModel(), 2, 3, ViewAt(new Vector3(1.5f, 2f, 0f), 0.4f));

            _field.CollectDeflectors(_results);

            Assert.AreEqual(1, _results.Count);
            Assert.AreEqual(new Vector2(1.5f, 2f), _results[0].Center);
            Assert.AreEqual(0.4f, _results[0].Radius, 1e-4f);
        }

        [Test]
        public void CollectDeflectors_UnbreakableBalloon_IsCollected()
        {
            Place(new UnbreakableBalloonModel(new BalloonModelConfig()), 0, 0, ViewAt(Vector3.zero, 0.325f));

            _field.CollectDeflectors(_results);

            Assert.AreEqual(1, _results.Count);
        }

        [Test]
        public void CollectDeflectors_OrdinaryBalloon_IsIgnored()
        {
            Place(new BalloonModel(), 1, 1, ViewAt(Vector3.zero, 0.3125f));

            _field.CollectDeflectors(_results);

            CollectionAssert.IsEmpty(_results);
        }

        // The last hit pops it rather than bouncing, so it must not telegraph a deflection.
        [Test]
        public void CollectDeflectors_ToughOnItsFinalHit_IsIgnored()
        {
            var tough = new ToughBalloonModel(new BalloonModelConfig());
            tough.HitsRemaining.Value = 1;
            Place(tough, 1, 1, ViewAt(Vector3.zero, 0.325f));

            _field.CollectDeflectors(_results);

            CollectionAssert.IsEmpty(_results);
        }

        [Test]
        public void CollectDeflectors_ViewMidDespawn_IsIgnored()
        {
            var view = ViewAt(Vector3.zero, 0.4f);
            view.HasActiveCollider.Returns(false);
            Place(new DeflectorActorModel(), 2, 3, view);

            _field.CollectDeflectors(_results);

            CollectionAssert.IsEmpty(_results);
        }

        // A static archetype with no collider authored on its prefab is collision-inert.
        [Test]
        public void CollectDeflectors_ZeroRadius_IsIgnored()
        {
            Place(new DeflectorActorModel(), 2, 3, ViewAt(Vector3.zero, 0f));

            _field.CollectDeflectors(_results);

            CollectionAssert.IsEmpty(_results);
        }

        [Test]
        public void CollectDeflectors_ActorWithoutAView_IsIgnored()
        {
            _grid.Place(new DeflectorActorModel(), null, new Vector2Int(2, 3));

            _field.CollectDeflectors(_results);

            CollectionAssert.IsEmpty(_results);
        }

        [Test]
        public void CollectDeflectors_ClearsWhatTheCallerHandedIn()
        {
            _results.Add(new DeflectorCircle(Vector2.one, 5f));

            _field.CollectDeflectors(_results);

            CollectionAssert.IsEmpty(_results);
        }

        private static ISlotActorView ViewAt(Vector3 center, float radius)
        {
            var view = Substitute.For<ISlotActorView>();
            view.ContactCenter.Returns(center);
            view.ContactRadius.Returns(radius);
            view.HasActiveCollider.Returns(true);
            return view;
        }

        private void Place(IWriteableSlotActor actor, int col, int row, ISlotActorView view)
        {
            _grid.Place(actor, view, new Vector2Int(col, row));
        }
    }
}

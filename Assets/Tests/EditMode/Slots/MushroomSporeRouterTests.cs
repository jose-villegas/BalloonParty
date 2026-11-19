using System;
using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     The spores' glue (<see cref="MushroomSporeRouter" />, @ref plan_meadow_mushrooms): what it hears
    ///     reaches the mushroom's own view as specks; a blast's burst waits its delay and is let go only to the
    ///     view it was for; and nothing waits across a new board. The scope here has no smoke field, so only the
    ///     specks are seen, counted on each mushroom's own particle system.
    /// </summary>
    [TestFixture]
    public class MushroomSporeRouterTests
    {
        private const float Now = 10f;

        private static readonly Vector2Int Slot = new(3, 5);

        private SlotGrid _grid;
        private MushroomSettings _settings;
        private ReactiveProperty<int> _level;
        private MushroomSporeRouter _router;
        private List<GameObject> _made;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<ISlotGridConfig>();
            config.SlotsSize.Returns(MushroomTestBoard.GridSize);
            config.SlotSeparation.Returns(new Vector2(0.375f, 0.65f));
            config.SlotsOffset.Returns(new Vector2(5f, 3.25f));
            _grid = new SlotGrid(config, new BalancePathHolder());
            _settings = new MushroomSettings();
            _made = new List<GameObject>();

            _level = new ReactiveProperty<int>(4);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);
            _router = new MushroomSporeRouter(
                _settings, _grid, null, levelProgress, Subscriber<MushroomBouncedMessage>(),
                Subscriber<MushroomCutMessage>());
            _router.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _router.Dispose();
            foreach (var made in _made)
            {
                Object.DestroyImmediate(made);
            }
        }

        [Test]
        public void AHit_LetsItsSpecksGoAtOnce_FromTheMushroomItHit()
        {
            var spores = Mushroom(Slot);
            var other = Mushroom(new Vector2Int(1, 6));

            _router.OnBounced(Bounce(Slot, Vector2.right, 9.8f), Now);

            Assert.AreEqual(_settings.Spores.Specks, spores.particleCount);
            Assert.AreEqual(0, other.particleCount, "the cap it did not hit lets nothing go");
            Assert.AreEqual(0, _router.WaitingCount);
        }

        [Test]
        public void ABlastsBurst_WaitsItsDelay_ThenIsLetGo()
        {
            var spores = Mushroom(Slot);
            var delay = _settings.Spores.BlastDelay;

            _router.OnCut(Blast(Slot), Now);

            Assert.AreEqual(1, _router.WaitingCount);
            Assert.AreEqual(0, spores.particleCount, "the bomb's own cloud is over it");

            _router.Tick(Now + (delay * 0.5f));
            Assert.AreEqual(0, spores.particleCount);
            Assert.AreEqual(1, _router.WaitingCount);

            _router.Tick(Now + delay + 0.01f);
            Assert.Greater(spores.particleCount, 2 * _settings.Spores.Specks, "the biggest of the three");
            Assert.AreEqual(0, _router.WaitingCount);

            var let = spores.particleCount;
            _router.Tick(Now + delay + 1f);
            Assert.AreEqual(let, spores.particleCount, "and it is let go once");
        }

        [Test]
        public void ALancesCut_LetsItsSpecksGoAtOnce()
        {
            var spores = Mushroom(Slot);

            _router.OnCut(new MushroomCutMessage(Slot, Centre(Slot), Vector2.up), Now);

            Assert.Greater(spores.particleCount, _settings.Spores.Specks);
            Assert.AreEqual(0, _router.WaitingCount);
        }

        [Test]
        public void ABurstWhoseMushroomLeftTheBoard_IsDropped()
        {
            var spores = Mushroom(Slot);
            _router.OnCut(Blast(Slot), Now);

            _grid.Remove(Slot);
            _router.Tick(Now + 1f);

            Assert.AreEqual(0, spores.particleCount);
            Assert.AreEqual(0, _router.WaitingCount, "and it is not kept waiting for another");
        }

        [Test]
        public void ABurst_IsNotLetGoToAnotherMushroomThatTookTheSlot()
        {
            Mushroom(Slot);
            _router.OnCut(Blast(Slot), Now);

            _grid.Remove(Slot);
            var newcomer = Mushroom(Slot);
            _router.Tick(Now + 1f);

            Assert.AreEqual(0, newcomer.particleCount, "the burst was the old mushroom's, not the slot's");
        }

        [Test]
        public void ANinthBurst_IsLetGoAtOnce_WhenEightAreWaiting()
        {
            var all = new List<ParticleSystem>();
            for (var i = 0; i <= MushroomSporeRouter.MostWaiting; i++)
            {
                var slot = new Vector2Int(i % 5, 2 + (i / 5));
                all.Add(Mushroom(slot));
                _router.OnCut(Blast(slot), Now);
            }

            Assert.AreEqual(MushroomSporeRouter.MostWaiting, _router.WaitingCount);
            Assert.Greater(all[MushroomSporeRouter.MostWaiting].particleCount, 0, "no room to wait: it goes now");
            for (var i = 0; i < MushroomSporeRouter.MostWaiting; i++)
            {
                Assert.AreEqual(0, all[i].particleCount, "the eight before it wait");
            }
        }

        [Test]
        public void ARunReset_DropsWhatIsWaiting()
        {
            var spores = Mushroom(Slot);
            _router.OnCut(Blast(Slot), Now);

            _router.ResetRun(1);
            _router.Tick(Now + 1f);

            Assert.AreEqual(0, _router.WaitingCount);
            Assert.AreEqual(0, spores.particleCount, "no burst lands on the board the reset deals");
        }

        [Test]
        public void ANewLevel_DropsWhatIsWaiting_AndTheLevelTheScopeCameUpOnDoesNot()
        {
            var spores = Mushroom(Slot);
            _router.OnCut(Blast(Slot), Now);
            Assert.AreEqual(1, _router.WaitingCount, "the level it started on dropped nothing");

            _level.Value = 5;
            _router.Tick(Now + 1f);

            Assert.AreEqual(0, _router.WaitingCount);
            Assert.AreEqual(0, spores.particleCount);
        }

        [Test]
        public void WhatHappensWhereNoMushroomStands_IsNothing()
        {
            Assert.DoesNotThrow(() => _router.OnBounced(Bounce(Slot, Vector2.right, 9.8f), Now));
            Assert.DoesNotThrow(() => _router.OnCut(Blast(new Vector2Int(-3, 99)), Now));

            Assert.AreEqual(0, _router.WaitingCount);
        }

        private ParticleSystem Mushroom(Vector2Int slot)
        {
            var mushroom = new GameObject("Mushroom");
            _made.Add(mushroom);
            mushroom.transform.position = Centre(slot);
            var view = mushroom.AddComponent<MushroomView>();
            var holder = new GameObject("Spores");
            holder.transform.SetParent(mushroom.transform, false);
            var spores = holder.AddComponent<ParticleSystem>();
            spores.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spores.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            var emission = spores.emission;
            emission.enabled = false;
            typeof(MushroomView)
                .GetField("_spores", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(view, spores);
            view.Construct(
                _settings, Subscriber<MushroomBouncedMessage>(), Subscriber<MushroomCutMessage>(),
                Subscriber<MushroomRegrownMessage>(), Subscriber<MushroomSwayedMessage>(), _grid);
            _grid.Place(new MushroomBumperModel(_made.Count), view, slot);
            return spores;
        }

        private Vector2 Centre(Vector2Int slot)
        {
            return _grid.IndexToWorldPosition(slot);
        }

        private MushroomCutMessage Blast(Vector2Int slot)
        {
            var centre = _grid.InBounds(slot) ? Centre(slot) : Vector2.zero;
            return new MushroomCutMessage(slot, centre, Vector2.up, MushroomCutCause.Blast);
        }

        private static MushroomBouncedMessage Bounce(Vector2Int slot, Vector2 incoming, float speed)
        {
            return new MushroomBouncedMessage(null, slot, 0, Vector3.zero, incoming, speed, 1, default);
        }

        private static ISubscriber<T> Subscriber<T>()
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(Arg.Any<IMessageHandler<T>>(), Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}

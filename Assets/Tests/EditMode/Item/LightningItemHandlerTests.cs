using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item.Lightning;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class LightningItemHandlerTests
    {
        // Stands in for the chain-lightning view: records the per-jump callback so tests can
        // fire jumps after later activations have run, mimicking a chain resolving over time.
        private sealed class FakeChainEffect : EffectView, IChainEffect
        {
            internal static readonly List<FakeChainEffect> Prepared = new();

            internal Action<int> OnTargetHit;

            public void PrepareDisplay(
                IReadOnlyList<Vector3> targetPositions, ItemSettings settings, Action<int> onTargetHit)
            {
                OnTargetHit = onTargetHit;
                Prepared.Add(this);
            }

            public override void Play(Vector3 position, Color tint, Action onComplete = null) { }
        }

        private SlotGrid _grid;
        private IHitDispatcher _hitDispatcher;
        private LightningItemHandler _handler;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<IGameConfiguration>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(gameConfig, new BalancePathHolder());

            var itemConfig = Substitute.For<IItemConfiguration>();
            var lightningSettings = CreateItemSettings(ItemType.Lightning, damage: 1);
            itemConfig[ItemType.Lightning].Returns(lightningSettings);
            itemConfig.Items.Returns(new List<ItemSettings> { lightningSettings });

            _hitDispatcher = Substitute.For<IHitDispatcher>();

            _handler = new LightningItemHandler(
                itemConfig,
                _hitDispatcher,
                _grid,
                new PoolManager());
        }

        [TearDown]
        public void TearDown()
        {
            FakeChainEffect.Prepared.Clear();
            foreach (var fake in UnityEngine.Object.FindObjectsByType<FakeChainEffect>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(fake.gameObject);
            }

            var poolRoot = GameObject.Find("[Pool]");
            if (poolRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(poolRoot);
            }
        }

        [Test]
        public void Activate_NoSameColorBalloons_PublishesNoHits()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Blue");

            _handler.Activate(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));

            _hitDispatcher.DidNotReceive().Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_SameColorBalloons_PublishesHitForEach()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");
            PlaceBalloon(2, 0, "Red");

            _handler.Activate(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));

            _hitDispatcher.Received(2).Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_ExcludesSelfFromTargets()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");

            _handler.Activate(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));

            _hitDispatcher.Received(1).Dispatch(Arg.Any<ActorHitMessage>());
            _hitDispatcher.DidNotReceive().Dispatch(
                Arg.Is<ActorHitMessage>(m => ReferenceEquals(m.Actor, source)));
        }

        [Test]
        public void Activate_AppliesConfiguredDamage()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");

            _handler.Activate(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));

            _hitDispatcher.Received(1).Dispatch(
                Arg.Is<ActorHitMessage>(m => m.Context.Damage == 1));
        }

        [Test]
        public void Activate_OverlappingChains_KeepIndependentTargets()
        {
            var prefab = new GameObject("FakeChain").AddComponent<FakeChainEffect>();
            var settings = CreateItemSettings(ItemType.Lightning, damage: 1);
            SetField(settings, "_activationEffectPrefab", (EffectView)prefab);
            var itemConfig = Substitute.For<IItemConfiguration>();
            itemConfig[ItemType.Lightning].Returns(settings);

            var published = new List<ActorHitMessage>();
            var dispatcher = Substitute.For<IHitDispatcher>();
            dispatcher.When(d => d.Dispatch(Arg.Any<ActorHitMessage>()))
                .Do(ci => published.Add(ci.Arg<ActorHitMessage>()));

            var handler = new LightningItemHandler(itemConfig, dispatcher, _grid, new PoolManager());

            var sourceA = PlaceBalloon(0, 0, "Red");
            var target1 = PlaceBalloon(1, 0, "Red");
            var target2 = PlaceBalloon(2, 0, "Red");
            var sourceB = PlaceBalloon(5, 9, "Red");

            handler.Activate(sourceA, _grid.IndexToWorldPosition(new Vector2Int(0, 0)));
            handler.Activate(sourceB, _grid.IndexToWorldPosition(new Vector2Int(5, 9)));

            // Chain A's jumps land after B's activation collected its own targets — they must
            // still hit A's targets (t1, t2, B), never A itself via B's refreshed list.
            var chainA = FakeChainEffect.Prepared[0];
            for (var i = 0; i < 3; i++)
            {
                chainA.OnTargetHit(i);
            }

            var hitActors = published.Select(m => m.Actor).ToList();
            CollectionAssert.AreEquivalent(new object[] { target1, target2, sourceB }, hitActors);
        }

        private BalloonModel PlaceBalloon(int col, int row, string color)
        {
            var model = new BalloonModel();
            model.Color.Value = color;
            _grid.Place(model, null, new Vector2Int(col, row));
            return model;
        }

        private static ItemSettings CreateItemSettings(ItemType type, int damage)
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", type);
            SetField(settings, "_damage", damage);
            SetField(settings, "_turnCheckEvery", 0);
            SetField(settings, "_weight", 0f);
            SetField(settings, "_maximumAllowed", 0);
            return settings;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}

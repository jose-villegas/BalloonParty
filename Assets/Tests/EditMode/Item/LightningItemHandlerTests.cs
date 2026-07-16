using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Item.Lightning;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Grid;
using BalloonParty.Configuration.Effects;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

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

            public void SetGlowColors(IReadOnlyList<Color> colors, float cycles) { }

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

            var palette = Substitute.For<IGamePalette>();
            palette.IsRainbow(GamePalette.RainbowColorId).Returns(true);

            // A real service (RegisterLight only lists + subscribes — no RT before Start), so the
            // per-jump light path exercises without a full render setup.
            var lightField = new SceneLightFieldService(
                Substitute.For<IGameDisplayConfiguration>(), palette,
                Substitute.For<ISceneLightFieldSettings>(), Substitute.For<ISceneLightSettings>());

            _handler = new LightningItemHandler(
                itemConfig,
                _hitDispatcher,
                palette,
                Substitute.For<ISubscriber<ProjectileLoadedMessage>>(),
                _grid,
                new PoolManager(),
                lightField);
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

            _handler.Activate(new ItemActivationContext(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)), Vector3.zero));

            _hitDispatcher.DidNotReceive().Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_SameColorBalloons_PublishesHitForEach()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");
            PlaceBalloon(2, 0, "Red");

            _handler.Activate(new ItemActivationContext(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)), Vector3.zero));

            _hitDispatcher.Received(2).Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_RainbowHolder_ConvertsProjectileColorGroupToRainbow()
        {
            var projectile = Substitute.For<IProjectileModel>();
            projectile.ColorName.Returns(new ReactiveProperty<string>("Red"));
            SetField(_handler, "_activeProjectile", projectile);

            var source = PlaceBalloon(0, 0, GamePalette.RainbowColorId);
            var red1 = PlaceBalloon(1, 0, "Red");
            var red2 = PlaceBalloon(2, 0, "Red");
            var blue = PlaceBalloon(3, 0, "Blue");

            _handler.Activate(new ItemActivationContext(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)), Vector3.zero));

            // The last-projectile colour group turns rainbow; other colours are untouched.
            Assert.AreEqual(GamePalette.RainbowColorId, red1.Color.Value);
            Assert.AreEqual(GamePalette.RainbowColorId, red2.Color.Value);
            Assert.AreEqual("Blue", blue.Color.Value);
            // It converts, never destroys.
            _hitDispatcher.DidNotReceive().Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_RainbowHolder_NoProjectileColor_ConvertsNearestColorGroup()
        {
            var source = PlaceBalloon(0, 0, GamePalette.RainbowColorId);
            var red = PlaceBalloon(1, 0, "Red");
            var blue = PlaceBalloon(4, 0, "Blue");

            _handler.Activate(new ItemActivationContext(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)), Vector3.zero));

            // No projectile colour to key on, so the fallback picks the nearest concrete colour.
            Assert.AreEqual(GamePalette.RainbowColorId, red.Color.Value);
            Assert.AreEqual("Blue", blue.Color.Value);
            _hitDispatcher.DidNotReceive().Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_RainbowHolder_NoConcreteBalloons_DoesNothing()
        {
            var source = PlaceBalloon(0, 0, GamePalette.RainbowColorId);

            _handler.Activate(new ItemActivationContext(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)), Vector3.zero));

            _hitDispatcher.DidNotReceive().Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Activate_ExcludesSelfFromTargets()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");

            _handler.Activate(new ItemActivationContext(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)), Vector3.zero));

            _hitDispatcher.Received(1).Dispatch(Arg.Any<ActorHitMessage>());
            _hitDispatcher.DidNotReceive().Dispatch(
                Arg.Is<ActorHitMessage>(m => ReferenceEquals(m.Actor, source)));
        }

        [Test]
        public void Activate_AppliesConfiguredDamage()
        {
            var source = PlaceBalloon(0, 0, "Red");
            PlaceBalloon(1, 0, "Red");

            _handler.Activate(new ItemActivationContext(source, _grid.IndexToWorldPosition(new Vector2Int(0, 0)), Vector3.zero));

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

            var lightField = new SceneLightFieldService(
                Substitute.For<IGameDisplayConfiguration>(),
                Substitute.For<IGamePalette>(),
                Substitute.For<ISceneLightFieldSettings>(),
                Substitute.For<ISceneLightSettings>());
            var handler = new LightningItemHandler(
                itemConfig, dispatcher, Substitute.For<IGamePalette>(),
                Substitute.For<ISubscriber<ProjectileLoadedMessage>>(), _grid, new PoolManager(), lightField);

            var sourceA = PlaceBalloon(0, 0, "Red");
            var target1 = PlaceBalloon(1, 0, "Red");
            var target2 = PlaceBalloon(2, 0, "Red");
            var sourceB = PlaceBalloon(5, 9, "Red");

            handler.Activate(new ItemActivationContext(sourceA, _grid.IndexToWorldPosition(new Vector2Int(0, 0)), Vector3.zero));
            handler.Activate(new ItemActivationContext(sourceB, _grid.IndexToWorldPosition(new Vector2Int(5, 9)), Vector3.zero));

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

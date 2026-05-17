#if UNITY_EDITOR || DEVELOPMENT_BUILD
using BalloonParty.Cheats;
#endif

using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Display;
using BalloonParty.Game.Cinematics;
using BalloonParty.Game.Score;
using BalloonParty.Item;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Laser;
using BalloonParty.Item.Lightning;
using BalloonParty.Item.Paint;
using BalloonParty.Item.Shield;
using BalloonParty.Nudge;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using BalloonParty.Thrower;
using BalloonParty.UI.Score;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    [DefaultExecutionOrder(-5001)]
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfiguration _gameConfiguration;
        [SerializeField] private GameDisplayConfiguration _displayConfiguration;
        [SerializeField] private ItemConfiguration _itemConfiguration;
        [SerializeField] private GamePalette _gamePalette;
        [SerializeField] private BalloonsConfiguration _balloonsConfiguration;
        [SerializeField] private ProjectileView _projectilePrefab;
        [SerializeField] private ScorePointTrail _scoreTrailPrefab;

        protected override void Awake()
        {
            DOTween.SetTweensCapacity(200, 50);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<BalanceBalloonsMessage>(options);
            builder.RegisterMessageBroker<SpawnBalloonLineMessage>(options);
            builder.RegisterMessageBroker<ProjectileDestroyedMessage>(options);
            builder.RegisterMessageBroker<BalloonHitMessage>(options);
            builder.RegisterMessageBroker<BalloonDeflectedMessage>(options);
            builder.RegisterMessageBroker<BalloonNudgeMessage>(options);
            builder.RegisterMessageBroker<BalloonScoredMessage>(options);
            builder.RegisterMessageBroker<ScoreLevelUpMessage>(options);
            builder.RegisterMessageBroker<ProjectileLoadedMessage>(options);
            builder.RegisterMessageBroker<ItemCheckMessage>(options);
            builder.RegisterMessageBroker<ItemActivatedMessage>(options);
            builder.RegisterMessageBroker<TransformCapturedMessage>(options);
            builder.RegisterMessageBroker<ShieldGainedMessage>(options);
            builder.RegisterMessageBroker<ScoreTrailArrivedMessage>(options);
            builder.RegisterMessageBroker<LevelUpDismissedMessage>(options);

            builder.RegisterInstance<IGameConfiguration>(_gameConfiguration);
            builder.RegisterInstance(_displayConfiguration);
            builder.RegisterInstance(_itemConfiguration);
            builder.RegisterInstance(_gamePalette);
            builder.RegisterInstance(_balloonsConfiguration);
            builder.RegisterInstance(new ThrowerSettings(_projectilePrefab));
            builder.RegisterInstance(_scoreTrailPrefab);

            builder.Register<SlotGrid>(Lifetime.Singleton);
            builder.Register<PoolManager>(Lifetime.Singleton);
            builder.Register<NudgeOverrideResolver>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<SlotGridView>();

            builder.RegisterEntryPoint<BalloonBalancer>();
            builder.RegisterEntryPoint<NudgeService>();
            builder.RegisterEntryPoint<BalloonSpawner>().AsSelf();
            builder.RegisterEntryPoint<ScoreController>().AsSelf();
            builder.RegisterEntryPoint<ScoreTrailService>().AsSelf();
            builder.RegisterEntryPoint<ItemAssigner>();
            builder.RegisterEntryPoint<ItemActivator>();

            builder.Register<ShieldItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<BombItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LaserItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LightningItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<PaintItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<OrthogonalSizeCameraController>();

            builder.RegisterEntryPoint<CinematicDirector>().AsSelf();
            builder.RegisterComponentInHierarchy<LevelUpTrailEffect>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<SpawnBalloonLineCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<FireProjectileCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<TriggerLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NearLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentOnNewGameObject<BalloonRemoverCheat>(Lifetime.Singleton, "BalloonRemoverCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<BalloonRemoverCheat>());

            builder.RegisterComponentOnNewGameObject<CheatConsoleView>(Lifetime.Singleton, "CheatConsole");
            builder.RegisterBuildCallback(resolver => resolver.Resolve<CheatConsoleView>());
#endif
        }
    }
}

using BalloonParty.Configuration;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Thrower;
using BalloonParty.UI.Score;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Buffs;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;

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
        [SerializeField] private OverflowSettings _overflowSettings;
        [SerializeField] private CinematicsSettings _cinematicsSettings;
        [SerializeField] private GridActorConfiguration _gridActorConfiguration;
        [SerializeField] private PuffCloudSettings _puffCloudSettings;
        [SerializeField] private BushSettings _bushSettings;
        [SerializeField] private DisturbanceFieldSettings _disturbanceFieldSettings;
        [SerializeField] private SceneLightFieldSettings _sceneLightFieldSettings;
        [SerializeField] private BackgroundFieldSettings _backgroundFieldSettings;
        [SerializeField] private PaintingFieldSettings _paintingFieldSettings;
        [SerializeField] private SpeckFieldSettings _speckFieldSettings;
        [SerializeField] private ShieldFieldSettings _shieldFieldSettings;
        [SerializeField] private LevelPacingConfiguration _levelPacingConfiguration;
        [SerializeField] private BuffConfiguration _buffConfiguration;
        [SerializeField] private ScoreTrailBehaviourConfiguration _scoreTrailBehaviourConfiguration;
        [SerializeField] private ProjectileView _projectilePrefab;
        [SerializeField] private FlyingTrail _scoreTrailPrefab;

        protected override void Awake()
        {
            DOTween.SetTweensCapacity(2048, 256);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessages(options);
            RegisterConfiguration(builder);

            // Call order is load-bearing — entry points start in registration order.
            builder.RegisterCoreServices();
            builder.RegisterGameplaySystems();
            builder.RegisterItems();
            builder.RegisterPresentation();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterCheats();
#endif
        }

        // Kept here (not GameScopeRegistration) because it needs this component's serialized fields.
        private void RegisterConfiguration(IContainerBuilder builder)
        {
            builder.RegisterInstance<IGameConfiguration>(_gameConfiguration);
            builder.RegisterInstance<IProjectileFlightConfig>(_gameConfiguration);
            builder.RegisterInstance<IGameDisplayConfiguration>(_displayConfiguration);
            builder.RegisterInstance<IItemConfiguration>(_itemConfiguration);
            builder.RegisterInstance<IGamePalette>(_gamePalette);
            builder.RegisterInstance<IBalloonsConfiguration>(_balloonsConfiguration);
            builder.RegisterInstance<IOverflowSettings>(_overflowSettings);
            builder.RegisterInstance<ICinematicsSettings>(_cinematicsSettings);
            builder.RegisterInstance<IGridActorConfiguration>(_gridActorConfiguration);
            builder.RegisterInstance<IPuffCloudSettings>(_puffCloudSettings);
            builder.RegisterInstance<IBushSettings>(_bushSettings);
            builder.RegisterInstance<IDisturbanceFieldSettings>(_disturbanceFieldSettings);
            builder.RegisterInstance<ISceneLightFieldSettings>(_sceneLightFieldSettings);
            builder.RegisterInstance<IScreenSpaceLightSettings>(_sceneLightFieldSettings);
            builder.RegisterInstance<ISceneLightSettings>(_sceneLightFieldSettings);
            builder.RegisterInstance<IBackgroundFieldSettings>(_backgroundFieldSettings);
            builder.RegisterInstance<IPaintingFieldSettings>(_paintingFieldSettings);
            builder.RegisterInstance<ISpeckFieldSettings>(_speckFieldSettings);
            builder.RegisterInstance<IShieldFieldSettings>(_shieldFieldSettings);
            builder.RegisterInstance<ILevelPacingConfiguration>(_levelPacingConfiguration);
            builder.RegisterInstance<IBuffConfiguration>(_buffConfiguration);

            // An unassigned asset degrades to an empty table — the resolver then falls back to DefaultScore
            // (a one-time dev warning), so correctness never depends on the field being wired in the scene.
            builder.RegisterInstance<IScoreTrailBehaviourConfiguration>(
                _scoreTrailBehaviourConfiguration != null
                    ? _scoreTrailBehaviourConfiguration
                    : ScriptableObject.CreateInstance<ScoreTrailBehaviourConfiguration>());
            builder.RegisterInstance(new ThrowerSettings(_projectilePrefab));
            builder.RegisterInstance(_scoreTrailPrefab);
        }
    }
}

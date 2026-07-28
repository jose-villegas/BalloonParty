using BalloonParty.Configuration;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Thrower;
using BalloonParty.UI.Score;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using UnityEngine.Serialization;
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
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.View;

namespace BalloonParty.Game
{
    [DefaultExecutionOrder(-5001)]
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private ProjectileFlightConfig _projectileFlightConfig;
        [SerializeField] private SlotGridConfig _slotGridConfig;
        [SerializeField] private PredictionTraceConfig _predictionTraceConfig;
        [SerializeField] private RunConfig _runConfig;
        [SerializeField] private ItemConfiguration _itemConfiguration;
        [SerializeField] private GamePalette _gamePalette;
        [SerializeField] private BalloonsConfiguration _balloonsConfiguration;
        [SerializeField] private OverflowSettings _overflowSettings;
        [SerializeField] private CinematicsSettings _cinematicsSettings;
        [SerializeField] private GridActorConfiguration _gridActorConfiguration;
        [SerializeField] private PuffCloudSettings _puffCloudSettings;
        [SerializeField] private BushSettings _bushSettings;
        [SerializeField] private SceneLightFieldSettings _sceneLightFieldSettings;
        [FormerlySerializedAs("_paintingFieldSettings")]
        [SerializeField] private SmokeFieldSettings _smokeFieldSettings;
        [SerializeField] private SpeckFieldSettings _speckFieldSettings;
        [SerializeField] private ShieldFieldSettings _shieldFieldSettings;
        [SerializeField] private ProjectileVisualConfig _projectileVisualConfig;
        [SerializeField] private LevelPacingConfiguration _levelPacingConfiguration;
        [SerializeField] private BuffConfiguration _buffConfiguration;
        [SerializeField] private ScoreTrailBehaviourConfiguration _scoreTrailBehaviourConfiguration;
        [SerializeField] private ThermalGovernorSettings _thermalGovernorSettings;
        [SerializeField] private SoundBankConfiguration _soundBank;
        [SerializeField] private AudioMixerSettings _audioMixerSettings;
        [SerializeField] private ProjectileView _projectilePrefab;
        [SerializeField] private FlyingTrail _scoreTrailPrefab;
        [SerializeField] private AudioSourceVoice _sfxVoicePrefab;

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
            builder.RegisterAudioRouters();

#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            builder.RegisterCheats();
#endif
        }

        // Kept here (not GameScopeRegistration) because it needs this component's serialized fields.
        private void RegisterConfiguration(IContainerBuilder builder)
        {
            builder.RegisterInstance<ISlotGridConfig>(_slotGridConfig);
            builder.RegisterInstance<IPredictionTraceConfig>(_predictionTraceConfig);
            builder.RegisterInstance<IRunConfig>(_runConfig);
            builder.RegisterInstance<IItemConfiguration>(_itemConfiguration);
            builder.RegisterInstance<IGamePalette>(_gamePalette);
            builder.RegisterInstance<IBalloonsConfiguration>(_balloonsConfiguration);
            builder.RegisterInstance<IOverflowSettings>(_overflowSettings);
            builder.RegisterInstance<ICinematicsSettings>(_cinematicsSettings);
            builder.RegisterInstance<IGridActorConfiguration>(_gridActorConfiguration);
            builder.RegisterInstance<IPuffCloudSettings>(_puffCloudSettings);
            builder.RegisterInstance<IBushSettings>(_bushSettings);

            // Display config, the ambient (scene-light / time-of-day), screen-space light, disturbance, and
            // background-field settings are owned by AppLifetimeScope now; this scope resolves them from the
            // parent. Only the LOCAL light field's settings stay here, feeding the Game-scope
            // SceneLightFieldService.
            builder.RegisterInstance<ISceneLightFieldSettings>(_sceneLightFieldSettings);
            builder.RegisterInstance<ISmokeFieldSettings>(_smokeFieldSettings);
            builder.RegisterInstance<ISpeckFieldSettings>(_speckFieldSettings);
            builder.RegisterInstance<IShieldFieldSettings>(_shieldFieldSettings);
            builder.RegisterInstance<IProjectileVisualConfig>(_projectileVisualConfig);
            builder.RegisterInstance<ILevelPacingConfiguration>(_levelPacingConfiguration);
            builder.RegisterInstance<IBuffConfiguration>(_buffConfiguration);

            // An unassigned asset degrades to an empty table — the resolver then falls back to DefaultScore
            // (a one-time dev warning), so correctness never depends on the field being wired in the scene.
            // The same asset also backs IScoreTrailConfig (the former umbrella carried the timing fields).
            var scoreTrailConfig = _scoreTrailBehaviourConfiguration != null
                ? _scoreTrailBehaviourConfiguration
                : ScriptableObject.CreateInstance<ScoreTrailBehaviourConfiguration>();
            builder.RegisterInstance<IScoreTrailBehaviourConfiguration>(scoreTrailConfig);
            builder.RegisterInstance<IScoreTrailConfig>(scoreTrailConfig);
            builder.RegisterInstance(new ThrowerSettings(_projectilePrefab));
            builder.RegisterInstance(_scoreTrailPrefab);

            // An unassigned asset degrades to a default instance carrying the sensible defaults, so the
            // governor works out of the box; wire an authored asset in the scene only to tune it.
            builder.RegisterInstance<IThermalGovernorSettings>(
                _thermalGovernorSettings != null
                    ? _thermalGovernorSettings
                    : ScriptableObject.CreateInstance<ThermalGovernorSettings>());
        }
    }
}

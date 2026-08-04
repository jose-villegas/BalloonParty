using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Display;
using BalloonParty.Game.Cinematics;
using BalloonParty.Scenario;
using BalloonParty.Shared;
using BalloonParty.Shared.Cadence;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Actor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    /// <summary>
    ///     The persistent app root: the single VContainer scope that lives for the whole session
    ///     (DontDestroyOnLoad) and parents both the Launcher and Game scopes. It owns everything the ONE
    ///     shared camera needs to render the same look in every navigation state — so Launch and Game render
    ///     through the same camera + rendering pipeline instead of swapping cameras (which caused the
    ///     backdrop/lighting "jump" at Play). Set this prefab as the VContainer project root
    ///     (<c>VContainerSettings.RootLifetimeScope</c>); it is instantiated once, before any scene scope
    ///     builds, so scene scopes resolve its registrations as a parent.
    ///
    ///     Owns: the display config; the ambient time-of-day owner
    ///     (<see cref="TimeOfDayService" />/<see cref="TimeOfDayClock" />); the camera pipeline (ortho sizing,
    ///     scene capture, screen-space light, background tint, cinematic view, camera-shake view); and the
    ///     camera-independent, interactive backdrop fields (disturbance + background cloud, plus the launch
    ///     finger-poke), so the launch begin-screen's live clouds no longer depend on the Game scene's
    ///     preload. Per-run gameplay — the local light field, smoke, and everything under RegisterGameplaySystems
    ///     — stays in the Game scope, which resolves these singletons from this parent.
    /// </summary>
    public class AppLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameDisplayConfiguration _displayConfiguration;
        [SerializeField] private SceneLightFieldSettings _sceneLightFieldSettings;
        [SerializeField] private DisturbanceFieldSettings _disturbanceFieldSettings;
        [SerializeField] private BackgroundFieldSettings _backgroundFieldSettings;
        [SerializeField] private Audio.Configuration.SoundBankConfiguration _soundBank;
        [SerializeField] private Audio.View.AudioSourceVoice _sfxVoicePrefab;
        [SerializeField] private Audio.Configuration.AudioMixerSettings _audioMixerSettings;
        [SerializeField] private ProjectileFlightConfig _projectileFlightConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance<IGameDisplayConfiguration>(_displayConfiguration);

            // The ambient (direction/colour/intensity) globals every camera and the backdrop read. A single
            // owner here replaces the two independent instances the Launch and Game roots each built — those
            // both wrote the same globals every frame (last-writer-wins), masked only because one scene's
            // camera was always disabled.
            builder.RegisterInstance<ISceneLightSettings>(_sceneLightFieldSettings);
            builder.RegisterInstance<ITimeOfDaySettings>(_sceneLightFieldSettings);
            builder.RegisterInstance<IScreenSpaceLightSettings>(_sceneLightFieldSettings);

            // Registered AsImplementedInterfaces + AsSelf (not a bare entry point) so child-scope consumers
            // resolve it as ISceneLightRuntime and as the concrete type from this parent — matching how the
            // Game scope registered it. TimeOfDayCycle (Game) drives THIS single instance via SetDirection,
            // and the camera's tint/GI read it, so the camera follows the level sweep. It runs as an
            // IStartable via the dispatcher the TimeOfDayClock entry point sets up below.
            builder.Register<TimeOfDayService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<TimeOfDayClock>().AsSelf();

            // The single shared camera pipeline lives on the Main Camera nested in this prefab, so these
            // resolve here (found in the prefab hierarchy) and persist because this scope does. Scene scopes
            // resolve them from this parent instead of each owning a camera.
            builder.RegisterComponentInHierarchy<OrthogonalSizeCameraController>();
            builder.RegisterComponentInHierarchy<SceneCaptureService>().AsSelf().As<ICadencedEffect>();
            builder.RegisterComponentInHierarchy<ScreenSpaceLightService>();
            builder.RegisterComponentInHierarchy<CameraBackgroundTint>();
            builder.RegisterComponentInHierarchy<CinematicCameraView>();
            builder.RegisterComponentInHierarchy<CameraShakeView>();

            // The interactive backdrop fields — camera-independent, global-shader-driven. Hoisted here (out of
            // the Game scope) so the launcher's live/pokeable clouds are a first-class app-root guarantee
            // rather than a side effect of the Game scene being preloaded. Gameplay consumers of these
            // (balloons, projectile, items) resolve them from this parent. ScenarioContentRoot moves too since
            // BackgroundFieldService reads its transform; ImpactEventBus, since the field reports impacts to it
            // and the bush rustle (Game) listens — one bus, resolved from this parent.
            builder.RegisterInstance<IDisturbanceFieldSettings>(_disturbanceFieldSettings);
            builder.RegisterInstance<IBackgroundFieldSettings>(_backgroundFieldSettings);
            builder.Register<ImpactEventBus>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<ScenarioContentRoot>(Lifetime.Singleton);
            builder.Register<DisturbanceFieldService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<BackgroundFieldService>(Lifetime.Singleton).AsImplementedInterfaces();
            // App scope, not Game: a global shader uniform has no scene lifetime, and a material
            // opted into the unscaled clock would freeze at 0 anywhere this did not tick.
            builder.Register<ShaderTimeService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterEntryPoint<LaunchDisturbanceStamp>();

            // Object pooling — app-wide so both Launch and Game consumers resolve from the same pool root.
            builder.Register<PoolManager>(Lifetime.Singleton);

            // Session identity for telemetry (R22 of @ref plan_gameplay_telemetry). Here, not in the Game
            // scope: a restart rebuilds that scope, and a per-scene session id would shatter one play
            // session into several in the warehouse. The id is a per-launch Guid, never persisted.
            builder.Register<Telemetry.SessionTelemetryContext>(Lifetime.Singleton);

            // IProjectileFlightConfig is needed by SfxService for stereo pan boundaries. Registered here
            // so audio can resolve it; the game scope also uses it (resolved from this parent).
            builder.RegisterInstance<IProjectileFlightConfig>(_projectileFlightConfig);

            // Core audio (SfxService, voice pool, bank config) — app-wide so any scene can play sounds.
            builder.RegisterAudioCore(_soundBank, _sfxVoicePrefab, _audioMixerSettings);

            // Inject ISoundPlayer into any UiSfxEmitter instances already in the scene.
            builder.RegisterBuildCallback(InjectUiSfxEmitters);
        }

        private static void InjectUiSfxEmitters(IObjectResolver resolver)
        {
            foreach (var emitter in FindObjectsByType<Audio.View.UiSfxEmitter>(FindObjectsSortMode.None))
            {
                resolver.Inject(emitter);
            }
        }
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using BalloonParty.Thrower;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Sweeps the shot solver across the aim arc and fires the best-scoring angle through
    ///     <c>ThrowerController.FireAt</c> (one-shot) or sets <c>ThrowerController.DirectionOverride</c>
    ///     (auto toggle) so the player's next tap fires the optimal angle. Usable in development builds
    ///     on device.
    /// </summary>
    internal class FireBestShotCheat : ICheat, ICheatControls, IStartable, IDisposable
    {
        private const int SampleCount = 1024;
        private const float ArcMinDegrees = 10f;
        private const float ArcMaxDegrees = 170f;

        private readonly SlotGrid _grid;
        private readonly IProjectileFlightConfig _config;
        private readonly ISlotGridConfig _gridConfig;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IItemConfiguration _itemConfig;
        private readonly ThrowerSettings _throwerSettings;
        private readonly IGamePalette _palette;
        private readonly IActiveLevelParameters _levelParams;
        private readonly IRunConfig _runConfig;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;

        private IDisposable _subscription;
        private ThrowerLifetimeScope _cachedScope;
        private ThrowerView _cachedView;
        private bool _autoFire;

        public string Name => "Fire Best Shot";
        public string Section => "Projectile";
        public IReadOnlyList<string> Tags => new[] { "projectile", "solver", "aim" };
        public bool Compact => false;

        public FireBestShotCheat(
            SlotGrid grid,
            IProjectileFlightConfig config,
            ISlotGridConfig gridConfig,
            IBalloonsConfiguration balloonsConfig,
            IItemConfiguration itemConfig,
            ThrowerSettings throwerSettings,
            IGamePalette palette,
            IActiveLevelParameters levelParams,
            IRunConfig runConfig,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _grid = grid;
            _config = config;
            _gridConfig = gridConfig;
            _balloonsConfig = balloonsConfig;
            _itemConfig = itemConfig;
            _throwerSettings = throwerSettings;
            _palette = palette;
            _levelParams = levelParams;
            _runConfig = runConfig;
            _loadedSubscriber = loadedSubscriber;
        }

        public void Start()
        {
            _subscription = _loadedSubscriber.Subscribe(_ => OnProjectileLoaded());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void Execute()
        {
            FireBest();
        }

        public void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fire Once"))
            {
                FireBest();
            }

            var newAutoFire = GUILayout.Toggle(_autoFire, "Auto");
            if (newAutoFire != _autoFire)
            {
                _autoFire = newAutoFire;
                Log.Info("FireBestShotCheat", _autoFire ? "auto-fire ON" : "auto-fire OFF");
                if (!_autoFire)
                {
                    ClearDirectionOverride();
                }
            }

            GUILayout.EndHorizontal();
        }

        private void OnProjectileLoaded()
        {
            if (!_autoFire)
            {
                return;
            }

            // Precompute best direction and set the override so the player's next tap fires it.
            // ProjectileLoadedMessage is always published on a later frame than FireAt, so this
            // cannot re-enter.
            SetDirectionOverride();
        }

        private void SetDirectionOverride()
        {
            if (_cachedScope == null || _cachedView == null)
            {
                RefreshCachedRefs();
            }

            if (_cachedScope == null || _cachedView == null)
            {
                return;
            }

            var pulseDelay = Mathf.Clamp(1.5f * Time.smoothDeltaTime, 0f, 0.1f);
            var context = ShotBoardGather.Gather(
                _grid, _config, _gridConfig, _balloonsConfig, _itemConfig, _cachedView, _throwerSettings, _palette,
                _levelParams, _runConfig, pulseDelay);
            if (context.Board.Count == 0)
            {
                return;
            }

            var workingSet = new ShotBalloonState[context.Board.Count];
            var bestAngle = ArcMinDegrees;
            var bestScore = int.MinValue;
            for (var i = 0; i < SampleCount; i++)
            {
                var angle = Mathf.Lerp(ArcMinDegrees, ArcMaxDegrees, i / (float)(SampleCount - 1));
                var score = ShotBoardGather.SimulateAt(angle, context, workingSet).RawScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAngle = angle;
                }
            }

            _cachedScope.Container.Resolve<ThrowerController>().DirectionOverride =
                ShotBoardGather.DirectionFromDegrees(bestAngle);
            Log.Info("FireBestShotCheat", $"override set: {bestAngle:F2}° (predicted score {bestScore}).");
        }

        private void FireBest()
        {
            RefreshCachedRefs();

            if (_cachedScope == null || _cachedView == null)
            {
                Log.Warn("FireBestShotCheat", "no thrower in the scene.");
                return;
            }

            var pulseDelay = Mathf.Clamp(1.5f * Time.smoothDeltaTime, 0f, 0.1f);
            var context = ShotBoardGather.Gather(
                _grid, _config, _gridConfig, _balloonsConfig, _itemConfig, _cachedView, _throwerSettings, _palette,
                _levelParams, _runConfig, pulseDelay);
            if (context.Board.Count == 0)
            {
                Log.Warn("FireBestShotCheat", "no targets on the board.");
                return;
            }

            var workingSet = new ShotBalloonState[context.Board.Count];
            var bestAngle = ArcMinDegrees;
            var bestScore = int.MinValue;
            for (var i = 0; i < SampleCount; i++)
            {
                var angle = Mathf.Lerp(ArcMinDegrees, ArcMaxDegrees, i / (float)(SampleCount - 1));
                var score = ShotBoardGather.SimulateAt(angle, context, workingSet).RawScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAngle = angle;
                }
            }

            _cachedScope.Container.Resolve<ThrowerController>()
                .FireAt(ShotBoardGather.DirectionFromDegrees(bestAngle));
            Log.Info("FireBestShotCheat", $"fired {bestAngle:F2}° (predicted score {bestScore}).");
        }

        private void RefreshCachedRefs()
        {
            if (_cachedScope == null || _cachedView == null)
            {
                _cachedScope = UnityEngine.Object.FindFirstObjectByType<ThrowerLifetimeScope>();
                _cachedView = UnityEngine.Object.FindFirstObjectByType<ThrowerView>();
            }
        }

        private void ClearDirectionOverride()
        {
            if (_cachedScope == null)
            {
                return;
            }

            _cachedScope.Container.Resolve<ThrowerController>().DirectionOverride = null;
        }
    }
}
#endif

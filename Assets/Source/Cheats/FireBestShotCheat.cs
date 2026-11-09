#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE

using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Game.Upgrades;
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
        // Only used for continuous aim (AimAngleStepDegrees <= 0) — a quantized aim instead samples
        // exactly the reachable angles (see AimAngleGrid.ResolveSweepSampleCount).
        private const int ContinuousSampleCount = 1024;

        private readonly SlotGrid _grid;
        private readonly IProjectileFlightConfig _config;
        private readonly ISlotGridConfig _gridConfig;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IItemConfiguration _itemConfig;
        private readonly ThrowerSettings _throwerSettings;
        private readonly IGamePalette _palette;
        private readonly IActiveLevelParameters _levelParams;
        private readonly IRunConfig _runConfig;
        private readonly IWeaponStats _weapon;
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
            IWeaponStats weapon,
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
            _weapon = weapon;
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

            // Precompute best direction and set the override so the player's next tap fires it. The
            // next ball loads in the same frame a shot fires, so this solves against the board before
            // that shot lands: with rapid fire the override is a fair guess, not the exact best. It
            // only sets the override and never fires, so the load inside FireAt cannot re-enter it.
            SetDirectionOverride();
        }

        private void SetDirectionOverride()
        {
            if (!TrySolve(out var bestAngle, out var bestScore))
            {
                return;
            }

            _cachedScope.Container.Resolve<ThrowerController>().DirectionOverride =
                ShotBoardGather.DirectionFromDegrees(bestAngle);
            Log.Info("FireBestShotCheat", $"override set: {bestAngle:F2}° (predicted score {bestScore}).");
        }

        /// <summary>
        ///     The angle "Fire Once" would fire, without firing: false with no thrower in the scene or nothing on
        ///     the board. The autoplay aims with it.
        /// </summary>
        internal bool TryFindBestAngle(out float bestAngle)
        {
            return TrySolve(out bestAngle, out _);
        }

        private void FireBest()
        {
            if (!TrySolve(out var bestAngle, out var bestScore))
            {
                Log.Warn("FireBestShotCheat", "no thrower in the scene, or no targets on the board.");
                return;
            }

            _cachedScope.Container.Resolve<ThrowerController>()
                .FireAt(ShotBoardGather.DirectionFromDegrees(bestAngle));
            Log.Info("FireBestShotCheat", $"fired {bestAngle:F2}° (predicted score {bestScore}).");
        }

        private bool TrySolve(out float bestAngle, out int bestScore)
        {
            bestAngle = 0f;
            bestScore = int.MinValue;
            RefreshCachedRefs();
            if (_cachedScope == null || _cachedView == null)
            {
                return false;
            }

            var pulseDelay = Mathf.Clamp(1.5f * Time.smoothDeltaTime, 0f, 0.1f);
            var context = ShotBoardGather.Gather(
                _grid, _config, _weapon.StartingShields, _gridConfig, _balloonsConfig, _itemConfig, _cachedView, _throwerSettings, _palette,
                _levelParams, _runConfig, pulseDelay,
                _cachedScope.Container.Resolve<BalloonBalancer>().SecondsUntilNextPulse);
            if (context.Board.Count == 0)
            {
                return false;
            }

            var workingSet = new ShotBalloonState[context.Board.Count];
            FindBestAngle(context, workingSet, out bestAngle, out bestScore);
            return true;
        }

        // Shared by the auto-fire override and the one-shot fire — both must pick the exact same
        // angle the same way, or the auto-set override could silently disagree with what "Fire Once"
        // would have chosen for the identical board.
        private void FindBestAngle(
            in ShotSolveContext context, ShotBalloonState[] workingSet, out float bestAngle, out int bestScore)
        {
            var sampleCount = AimAngleGrid.ResolveSweepSampleCount(
                _config.AimAngleMinDegrees, _config.AimAngleMaxDegrees, _config.AimAngleStepDegrees,
                ContinuousSampleCount);
            bestAngle = _config.AimAngleMinDegrees;
            bestScore = int.MinValue;
            for (var i = 0; i < sampleCount; i++)
            {
                var angle = AimAngleGrid.ResolveSweepAngle(
                    i, _config.AimAngleMinDegrees, _config.AimAngleMaxDegrees, _config.AimAngleStepDegrees,
                    ContinuousSampleCount);
                var score = ShotBoardGather.SimulateAt(angle, context, workingSet).RawScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAngle = angle;
                }
            }
        }

        private void RefreshCachedRefs()
        {
            if (_cachedScope == null || _cachedView == null)
            {
                _cachedScope = UnityEngine.Object.FindAnyObjectByType<ThrowerLifetimeScope>();
                _cachedView = UnityEngine.Object.FindAnyObjectByType<ThrowerView>();
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

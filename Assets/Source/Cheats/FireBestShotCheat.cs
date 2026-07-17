#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using BalloonParty.Thrower;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Sweeps the shot solver across the aim arc and fires the best-scoring angle through
    ///     <c>ThrowerController.FireAt</c> — the editor Shot Solver window's "Fire Best" as a one-tap
    ///     cheat, usable in development builds on device. The thrower lives in its own child scope,
    ///     so it is resolved at execute time rather than injected.
    /// </summary>
    internal class FireBestShotCheat : ICheat
    {
        private const int SampleCount = 1024;
        private const float ArcMinDegrees = 10f;
        private const float ArcMaxDegrees = 170f;

        private readonly SlotGrid _grid;
        private readonly IGameConfiguration _config;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly ThrowerSettings _throwerSettings;

        public string Name => "Fire Best Shot";
        public string Section => "Projectile";
        public IReadOnlyList<string> Tags => new[] { "projectile", "solver", "aim" };

        public FireBestShotCheat(
            SlotGrid grid,
            IGameConfiguration config,
            IBalloonsConfiguration balloonsConfig,
            ThrowerSettings throwerSettings)
        {
            _grid = grid;
            _config = config;
            _balloonsConfig = balloonsConfig;
            _throwerSettings = throwerSettings;
        }

        public void Execute()
        {
            var throwerScope = Object.FindFirstObjectByType<ThrowerLifetimeScope>();
            var throwerView = Object.FindFirstObjectByType<ThrowerView>();
            if (throwerScope == null || throwerView == null)
            {
                Debug.LogWarning("FireBestShotCheat: no thrower in the scene.");
                return;
            }

            var pulseDelay = Mathf.Clamp(1.5f * Time.smoothDeltaTime, 0f, 0.1f);
            var context = ShotBoardGather.Gather(
                _grid, _config, _balloonsConfig, throwerView, _throwerSettings, pulseDelay);
            if (context.Board.Count == 0)
            {
                Debug.LogWarning("FireBestShotCheat: no targets on the board.");
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

            throwerScope.Container.Resolve<ThrowerController>()
                .FireAt(ShotBoardGather.DirectionFromDegrees(bestAngle));
            Debug.Log($"FireBestShotCheat: fired {bestAngle:F2}° (predicted score {bestScore}).");
        }
    }
}
#endif

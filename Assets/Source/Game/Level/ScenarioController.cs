using System;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Run;
using BalloonParty.Scenario;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer.Unity;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Keeps <see cref="ActiveScenario" /> on the current level's scenario (sky or ground, per
    ///     <see cref="ILevelPacingConfiguration.ScenarioForLevel" />). Snaps on run start and run reset, so the
    ///     start-level cheat and a restart land on the right backdrop, and switches as a level-up Ascent starts.
    ///     The switch is a cut for now; the edge that rides the scenario root in is
    ///     <c>PLAN-TerrainBiomes.md</c> phase 2.
    /// </summary>
    internal sealed class ScenarioController : IStartable, IRunResettable, IDisposable
    {
        private readonly ILevelPacingConfiguration _pacing;
        private readonly IRunSeed _runSeed;
        private readonly ILevelProgress _levelProgress;
        private readonly ActiveScenario _scenario;
        private readonly ISubscriber<LevelAscendStartedMessage> _ascendStarted;

        private IDisposable _subscription;

        // After LevelController (Score) so a run's start level is already in place when we snap.
        public int ResetOrder => RunResetOrder.Respawn;

        internal ScenarioController(
            ILevelPacingConfiguration pacing, IRunSeed runSeed, ILevelProgress levelProgress,
            ActiveScenario scenario, ISubscriber<LevelAscendStartedMessage> ascendStarted)
        {
            _pacing = pacing;
            _runSeed = runSeed;
            _levelProgress = levelProgress;
            _scenario = scenario;
            _ascendStarted = ascendStarted;
        }

        void IStartable.Start()
        {
            // The level has already advanced when the Ascent starts (LevelController sets it before the phase
            // flips to Transitioning), and the root lifts the new scenery that same frame. The old board still
            // floats out over the switched backdrop; the edge in phase 2 carries each backdrop with its board.
            _subscription = _ascendStarted.Subscribe(_ => SnapToLevel());
            SnapToLevel();
        }

        public void ResetRun(int generation)
        {
            SnapToLevel();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void SnapToLevel()
        {
            _scenario.Set(_pacing.ScenarioForLevel(_levelProgress.Level.Value, _runSeed.Value));
        }
    }
}

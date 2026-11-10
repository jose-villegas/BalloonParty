#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE

using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Diagnostics;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Restarts the run at the start of the next stretch of a chosen scenario (sky, meadow or water), looked up
    ///     in this run's own schedule, so everything that reads the schedule (the backdrop, the spawner's bubbles,
    ///     the pacing) agrees with it. Keeps the run's seed through the restart (<see cref="CheatState.KeepRunSeed" />):
    ///     a new seed lays out a new schedule, where the level found could be another scenario.
    /// </summary>
    internal class JumpToScenarioCheat : ICheat, ICheatControls
    {
        // How far ahead to look. Water comes up on about half the surface stretches, so it is always well inside.
        private const int SearchLevels = 500;

        private static readonly ScenarioKind[] Kinds = { ScenarioKind.Sky, ScenarioKind.Ground, ScenarioKind.Water };
        private static readonly string[] Labels = { "Sky", "Meadow", "Water" };

        private readonly RunController _runController;
        private readonly ILevelPacingConfiguration _pacing;
        private readonly IRunSeed _runSeed;
        private readonly ILevelProgress _levelProgress;

        private ScenarioKind _kind = ScenarioKind.Water;

        public string Name => "Jump To Scenario";
        public string Section => "Run";
        public IReadOnlyList<string> Tags => new[] { "run", "level", "scenario" };
        public bool Compact => false;

        public JumpToScenarioCheat(RunController runController, ILevelPacingConfiguration pacing, IRunSeed runSeed,
            ILevelProgress levelProgress)
        {
            _runController = runController;
            _pacing = pacing;
            _runSeed = runSeed;
            _levelProgress = levelProgress;
        }

        public void Execute()
        {
            var seed = _runSeed.Value;
            var level = NextStretchStart(l => _pacing.ScenarioForLevel(l, seed), _levelProgress.Level.Value, _kind,
                SearchLevels);
            if (level < 1)
            {
                Log.Warn("Cheats", $"No {_kind} stretch in the {SearchLevels} levels after this one");
                return;
            }

            Log.Info("Cheats", $"Jumping to level {level}, the next {_kind} stretch");
            CheatState.StartLevel = level;
            CheatState.KeepRunSeed = true;
            _runController.RestartRun();
        }

        public void DrawControls()
        {
            var picked = CheatLayout.ButtonGrid(Labels);
            if (picked < 0)
            {
                return;
            }

            _kind = Kinds[picked];
            Execute();
        }

        /// <summary>
        ///     The first level after <paramref name="fromLevel" /> that starts a stretch of <paramref name="kind" />,
        ///     looking at most <paramref name="searchLevels" /> ahead; -1 when there is none that close.
        /// </summary>
        internal static int NextStretchStart(Func<int, ScenarioKind> kindAt, int fromLevel, ScenarioKind kind,
            int searchLevels)
        {
            var previous = kindAt(fromLevel);
            for (var level = fromLevel + 1; level <= fromLevel + searchLevels; level++)
            {
                var current = kindAt(level);
                if (current == kind && previous != kind)
                {
                    return level;
                }

                previous = current;
            }

            return -1;
        }
    }
}
#endif

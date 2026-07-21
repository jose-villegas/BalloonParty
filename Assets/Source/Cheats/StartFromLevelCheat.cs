#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Game.Run;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Restarts the run so it BEGINS at a chosen level — sets the dev start-level override, then
    ///     kicks a run restart, which re-resolves the difficulty mix and respawns the board for that
    ///     level. The Level Pacing window's "play from here" carries the same override across the
    ///     enter-play reload; this is the in-play equivalent.
    /// </summary>
    internal class StartFromLevelCheat : ICheat, ICheatControls
    {
        private readonly RunController _runController;

        private int _level = 1;

        public string Name => "Start From Level";
        public string Section => "Run";
        public IReadOnlyList<string> Tags => new[] { "run", "level", "pacing" };
        public bool Compact => false;

        public StartFromLevelCheat(RunController runController)
        {
            _runController = runController;
        }

        public void Execute()
        {
            CheatState.StartLevel = _level;
            _runController.RestartRun();
        }

        public void DrawControls()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Level", GUILayout.Width(44));
            _level = CheatLayout.IntField("startlevel.level", _level);

            if (GUILayout.Button("Start From Level"))
            {
                Execute();
            }

            GUILayout.EndHorizontal();
        }
    }
}
#endif

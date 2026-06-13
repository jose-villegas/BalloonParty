#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Game.Run;

namespace BalloonParty.Cheats
{
    internal class ForceGameOverCheat : ICheat
    {
        private readonly RunController _runController;

        public string Name => "Force Game Over";
        public string Section => "Run";
        public IReadOnlyList<string> Tags => new[] { "run", "gameover", "loss" };

        public ForceGameOverCheat(RunController runController)
        {
            _runController = runController;
        }

        public void Execute()
        {
            _runController.EndRun();
        }
    }
}
#endif

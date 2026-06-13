#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Game.Run;

namespace BalloonParty.Cheats
{
    internal class ForceRestartCheat : ICheat
    {
        private readonly RunController _runController;

        public string Name => "Restart Run";
        public string Section => "Run";
        public IReadOnlyList<string> Tags => new[] { "run", "restart" };

        public ForceRestartCheat(RunController runController)
        {
            _runController = runController;
        }

        public void Execute()
        {
            _runController.RestartRun();
        }
    }
}
#endif

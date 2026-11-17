#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE

using System.Collections.Generic;
using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>Switches <see cref="CheatState.SceneCaptureInFrame" />: the scene capture drawn inside the main
    /// camera's frame or by its own camera, against what the config says, to compare the two in play.</summary>
    internal class SceneCaptureInFrameCheat : ICheat, ICheatControls
    {
        private readonly IGameDisplayConfiguration _displayConfig;

        public string Name => "Scene Capture In Frame";
        public string Section => "Rendering";
        public IReadOnlyList<string> Tags => new[] { "capture", "light", "bounce", "shadow", "perf" };
        public bool Compact => true;

        private bool InFrame => CheatState.SceneCaptureInFrame ?? _displayConfig.SceneCaptureInFrame;

        public SceneCaptureInFrameCheat(IGameDisplayConfiguration displayConfig)
        {
            _displayConfig = displayConfig;
        }

        public void Execute()
        {
            CheatState.SceneCaptureInFrame = !InFrame;
        }

        public void DrawControls()
        {
            var inFrame = GUILayout.Toggle(InFrame, " Scene capture in the main camera's frame");
            if (inFrame != InFrame)
            {
                CheatState.SceneCaptureInFrame = inFrame;
            }
        }
    }
}
#endif

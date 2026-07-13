#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>Toggles <see cref="CheatState.BlockLevelUp" /> — a level lock. While on: progress is frozen (no
    /// scoring toward the threshold), no level-up cinematic or ceremony can start, and the run can't end (no
    /// loss). Sit on a level indefinitely to test it; toggle off to resume normal play.</summary>
    internal class BlockLevelUpCheat : ICheat, ICheatControls
    {
        public string Name => "Level Lock";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup", "lock", "loss" };

        public void Execute()
        {
            CheatState.BlockLevelUp = !CheatState.BlockLevelUp;
        }

        public void DrawControls()
        {
            CheatState.BlockLevelUp = GUILayout.Toggle(
                CheatState.BlockLevelUp,
                CheatState.BlockLevelUp ? " Level LOCKED — no progress, no loss" : " Lock level (no win/loss)");
        }
    }
}
#endif

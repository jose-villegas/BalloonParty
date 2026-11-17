#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE

using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>Toggles <see cref="CheatState.FlipToughSnakeSprings" />: the tough snake's springs, knot to knot,
    /// shown or hidden against what the config says, to compare the two in play.</summary>
    internal class ToughSnakeSpringsCheat : ICheat, ICheatControls
    {
        public string Name => "Tough Snake Springs";
        public string Section => "Grid";
        public IReadOnlyList<string> Tags => new[] { "tough", "snake", "spring", "look" };
        public bool Compact => true;

        public void Execute()
        {
            CheatState.FlipToughSnakeSprings = !CheatState.FlipToughSnakeSprings;
        }

        public void DrawControls()
        {
            CheatState.FlipToughSnakeSprings = GUILayout.Toggle(
                CheatState.FlipToughSnakeSprings,
                CheatState.FlipToughSnakeSprings ? " Springs flipped from the config" : " Flip tough snake springs");
        }
    }
}
#endif

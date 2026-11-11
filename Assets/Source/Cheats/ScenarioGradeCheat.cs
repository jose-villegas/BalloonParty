#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE

using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>Toggles <see cref="CheatState.ScenarioGradeOff" />: the scenario colour grade off and on, to compare
    /// the scenes with and without it. A second toggle swaps the tonemapper back to URP's own Neutral curve
    /// (<see cref="CheatState.UrpNeutralTonemap" />), to compare it with the PBR Neutral the game builds.</summary>
    internal class ScenarioGradeCheat : ICheat, ICheatControls
    {
        public string Name => "Scenario Grade";
        public string Section => "Lighting";
        public IReadOnlyList<string> Tags => new[] { "grade", "color", "post", "scenario" };
        public bool Compact => true;

        public void Execute()
        {
            CheatState.ScenarioGradeOff = !CheatState.ScenarioGradeOff;
        }

        public void DrawControls()
        {
            CheatState.ScenarioGradeOff = !GUILayout.Toggle(
                !CheatState.ScenarioGradeOff,
                CheatState.ScenarioGradeOff ? " Scenario grade OFF" : " Scenario grade on");

            var urpNeutral = GUILayout.Toggle(
                CheatState.UrpNeutralTonemap,
                CheatState.UrpNeutralTonemap ? " Tonemapper: URP Neutral (old)" : " Tonemapper: PBR Neutral");
            if (urpNeutral != CheatState.UrpNeutralTonemap)
            {
                CheatState.SetUrpNeutralTonemap(urpNeutral);
            }
        }
    }
}
#endif

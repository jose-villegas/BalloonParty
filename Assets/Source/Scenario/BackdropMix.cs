using BalloonParty.Configuration.Level;
using UnityEngine;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     How much of the screen the surface backdrop covers this frame, written by <c>GroundView</c> as its reveal
    ///     runs: 0 is all sky, 1 all <see cref="Surface" /> (the meadow or the water). The sky always sits between
    ///     two surfaces in the schedule, so one surface and one amount describe every frame. Polled each frame by
    ///     whatever follows the backdrop (the colour grade), so it is plain state, not reactive.
    /// </summary>
    internal sealed class BackdropMix
    {
        public ScenarioKind Surface { get; private set; } = ScenarioKind.Ground;
        public float Coverage { get; private set; }

        public void Set(ScenarioKind surface, float coverage)
        {
            Surface = surface;
            Coverage = Mathf.Clamp01(coverage);
        }
    }
}

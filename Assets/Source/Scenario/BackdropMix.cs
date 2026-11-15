using BalloonParty.Configuration.Level;
using UnityEngine;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     How much of the screen a surface backdrop covers this frame, written by <c>GroundView</c> as its reveal
    ///     runs: <see cref="Surface" /> (the meadow, the water or the paper) over <see cref="Under" />, the layer it
    ///     opens out of or closes into. 0 is all <see cref="Under" />, 1 all <see cref="Surface" />. A mist reveal
    ///     always has the sky under its surface; paper opening over the meadow or the water has that surface under
    ///     it. Polled each frame by whatever follows the backdrop (the colour grade), so it is plain state, not
    ///     reactive.
    /// </summary>
    internal sealed class BackdropMix
    {
        public ScenarioKind Surface { get; private set; } = ScenarioKind.Ground;
        public ScenarioKind Under { get; private set; } = ScenarioKind.Sky;
        public float Coverage { get; private set; }
        // How much of what the surfaces cover is paper: all of it when paper opens out of the sky, the hole's share
        // when it opens over the meadow or the water (which cover the rest).
        public float PaperShareOfSurfaces => Surface != ScenarioKind.Paper ? 0f
            : Under == ScenarioKind.Sky ? 1f
            : Coverage;

        public void Set(ScenarioKind surface, float coverage)
        {
            Set(surface, ScenarioKind.Sky, coverage);
        }

        public void Set(ScenarioKind surface, ScenarioKind under, float coverage)
        {
            Surface = surface;
            Under = under;
            Coverage = Mathf.Clamp01(coverage);
        }
    }
}

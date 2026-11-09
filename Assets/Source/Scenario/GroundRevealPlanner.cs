using BalloonParty.Configuration.Level;
using BalloonParty.Shared.GameState;

namespace BalloonParty.Scenario
{
    internal enum GroundRevealAction
    {
        None,
        Cut,
        Reveal,
    }

    /// <summary>What the backdrop does with a scenario change: nothing, a cut, or a mist reveal into <see cref="Target" />.</summary>
    internal readonly struct GroundRevealDecision
    {
        public readonly GroundRevealAction Action;
        public readonly ScenarioKind Target;
        public readonly float Seconds;

        public GroundRevealDecision(GroundRevealAction action, ScenarioKind target, float seconds)
        {
            Action = action;
            Target = target;
            Seconds = seconds;
        }
    }

    /// <summary>
    ///     Decides how <c>GroundView</c> answers the active scenario and the navigation state. The Launch
    ///     begin-screen is always sky, and entering or leaving it cuts. Other navigation leaves a running reveal
    ///     alone, since the reveal rides a beat of its own that navigation doesn't end.
    /// </summary>
    internal static class GroundRevealPlanner
    {
        /// <param name="current">The scenario showing, or the one a running reveal is heading for.</param>
        /// <param name="canReveal">False without a mist to reveal through, so every change cuts.</param>
        public static GroundRevealDecision Decide(
            ScenarioShift shift, NavigationState navigation, NavigationState? previousNavigation,
            ScenarioKind current, bool canReveal)
        {
            var launch = navigation == NavigationState.Launch;
            var crossedLaunch = previousNavigation == null
                || (previousNavigation == NavigationState.Launch) != launch;
            var target = launch ? ScenarioKind.Sky : shift.Kind;

            if (crossedLaunch || shift.IsCut || !canReveal)
            {
                return new GroundRevealDecision(GroundRevealAction.Cut, target, 0f);
            }

            return target == current
                ? new GroundRevealDecision(GroundRevealAction.None, target, 0f)
                : new GroundRevealDecision(GroundRevealAction.Reveal, target, shift.Seconds);
        }
    }
}

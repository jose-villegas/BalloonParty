using BalloonParty.Configuration.Level;
using UniRx;

namespace BalloonParty.Scenario
{
    /// <summary>Model behind <see cref="IActiveScenario" />; written only by <c>ScenarioController</c>.</summary>
    internal sealed class ActiveScenario : IActiveScenario
    {
        private readonly ReactiveProperty<ScenarioShift> _current = new(new ScenarioShift(ScenarioKind.Sky, 0f));

        public IReadOnlyReactiveProperty<ScenarioShift> Current => _current;

        public void Cut(ScenarioKind kind)
        {
            _current.Value = new ScenarioShift(kind, 0f);
        }

        // Only a real change animates: the same scenario on both sides of a beat leaves the backdrop alone.
        public void Reveal(ScenarioKind kind, float seconds)
        {
            if (kind == _current.Value.Kind)
            {
                return;
            }

            _current.Value = new ScenarioShift(kind, seconds);
        }
    }
}

using BalloonParty.Configuration.Level;
using UniRx;

namespace BalloonParty.Scenario
{
    /// <summary>Model behind <see cref="IActiveScenario" />; written only by <c>ScenarioController</c>.</summary>
    internal sealed class ActiveScenario : IActiveScenario
    {
        private readonly ReactiveProperty<ScenarioKind> _kind = new(ScenarioKind.Sky);

        public IReadOnlyReactiveProperty<ScenarioKind> Kind => _kind;

        public void Set(ScenarioKind kind)
        {
            _kind.Value = kind;
        }
    }
}

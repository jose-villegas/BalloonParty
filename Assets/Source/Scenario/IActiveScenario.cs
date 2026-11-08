using BalloonParty.Configuration.Level;
using UniRx;

namespace BalloonParty.Scenario
{
    /// <summary>The scenario the backdrop is showing now: sky or ground.</summary>
    internal interface IActiveScenario
    {
        IReadOnlyReactiveProperty<ScenarioKind> Kind { get; }
    }
}

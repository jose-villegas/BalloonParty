using UniRx;

namespace BalloonParty.Scenario
{
    /// <summary>The scenario the backdrop is on (sky or ground), and whether it got there by a cut or a mist reveal.</summary>
    internal interface IActiveScenario
    {
        IReadOnlyReactiveProperty<ScenarioShift> Current { get; }
    }
}

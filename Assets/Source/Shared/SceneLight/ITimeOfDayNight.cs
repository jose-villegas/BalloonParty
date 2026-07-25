namespace BalloonParty.Shared.SceneLight
{
    /// <summary>Whether the current time of day counts as night (5 PM–4 AM), for gameplay rules that key
    /// off it. Always false when night mode is off. Derived from the same angle
    /// <see cref="ISceneLightRuntime.CurrentDirection"/> exposes.</summary>
    internal interface ITimeOfDayNight
    {
        bool IsNight { get; }
    }
}

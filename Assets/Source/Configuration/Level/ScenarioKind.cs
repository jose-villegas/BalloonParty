namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Where a level is set: open sky, or one of the surfaces the board rests over (a meadow, water, or the paper
    ///     page of a bonus level). The schedule (<see cref="ScenarioSchedule" />) only deals sky, meadow and water;
    ///     paper is a backdrop a bonus level shows over its scheduled scenario (<see cref="LevelBackdrop" />).
    /// </summary>
    internal enum ScenarioKind
    {
        Sky = 0,
        Ground = 1,
        Water = 2,
        Paper = 3
    }
}

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     The scenario a level's backdrop shows. A bonus (extra-colour) level plays on paper; every other level shows
    ///     its scheduled scenario. Only the look changes: the level's content (puff clouds, bushes, bubbles) still
    ///     follows <see cref="ILevelPacingConfiguration.ScenarioForLevel" />.
    /// </summary>
    internal static class LevelBackdrop
    {
        public static ScenarioKind BackdropForLevel(this ILevelPacingConfiguration pacing, int level, int runSeed)
        {
            return pacing.IsExtraColorLevel(level, runSeed) ? ScenarioKind.Paper : pacing.ScenarioForLevel(level, runSeed);
        }
    }
}

using BalloonParty.Configuration.Level;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the whole-frame colour grade per scenario (see <c>ScenarioGradeView</c>).</summary>
    internal interface IScenarioGradeSettings
    {
        /// <summary>Off leaves the frame with only the base look of the default volume profile.</summary>
        bool Enabled { get; }

        ScenarioGrade For(ScenarioKind kind);
    }
}

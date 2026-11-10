using BalloonParty.Configuration.Level;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the whole-frame colour grade per scenario (see <c>ScenarioGradeView</c>).</summary>
    internal interface IScenarioGradeSettings
    {
        /// <summary>Off turns the camera's post-processing back off, so the grade costs nothing.</summary>
        bool Enabled { get; }

        ScenarioGrade For(ScenarioKind kind);
    }
}
